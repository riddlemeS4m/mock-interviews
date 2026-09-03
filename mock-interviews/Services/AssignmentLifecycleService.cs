using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MockInterviews.Data.Constants;
using MockInterviews.Data.Contexts;
using MockInterviews.Models.Entities;
using MockInterviews.Services.SignalR;

namespace MockInterviews.Services;

/// <summary>
/// Owns the state changes made from the live assignment workspace.  The browser
/// submits an interview and interviewer identifier only; all current state and
/// eligibility are loaded here immediately before a change is saved.
/// </summary>
public sealed class AssignmentLifecycleService(
    MockInterviewsDbContext context,
    IHubContext<AssignInterviewsHub> assignmentHub,
    ILogger<AssignmentLifecycleService> logger)
{
    public async Task<AssignmentCommandResult> CheckInAsync(int interviewId)
    {
        var interview = await FindInterviewAsync(interviewId);
        if (interview is null)
        {
            return AssignmentCommandResult.NotFound();
        }

        if (interview.Status != StatusConstants.Default)
        {
            return AssignmentCommandResult.Conflict("This interview is no longer waiting for check-in.");
        }

        interview.Status = StatusConstants.CheckedIn;
        interview.CheckedInAt = DateTime.UtcNow;
        return await SaveAndPublishAsync(interview, "checked in");
    }

    public async Task<AssignmentCommandResult> AssignAsync(int interviewId, string? interviewerId)
    {
        if (string.IsNullOrWhiteSpace(interviewerId) || interviewerId == "0")
        {
            return await UnassignAsync(interviewId);
        }

        var interview = await FindInterviewAsync(interviewId);
        if (interview is null)
        {
            return AssignmentCommandResult.NotFound();
        }

        if (interview.Status != StatusConstants.CheckedIn)
        {
            return AssignmentCommandResult.Conflict("Only a checked-in interview can be assigned and started.");
        }

        var availability = await context.InterviewerTimeslots
            .Include(item => item.InterviewerSignup)
            .Include(item => item.Timeslot)
            .ThenInclude(slot => slot.Event)
            .SingleOrDefaultAsync(item => item.TimeslotId == interview.TimeslotId &&
                item.InterviewerSignup.InterviewerId == interviewerId);
        if (availability is null)
        {
            return AssignmentCommandResult.Validation("The selected interviewer is not available for this exact timeslot.");
        }

        var eligibilityFailure = await GetNormalEligibilityFailureAsync(interview, availability);
        if (eligibilityFailure is not null)
        {
            return AssignmentCommandResult.Validation(eligibilityFailure);
        }

        await ApplyAssignmentAsync(interview, availability);
        interview.Status = StatusConstants.Ongoing;
        interview.StartedAt ??= DateTime.UtcNow;
        return await SaveAndPublishAsync(interview, "assigned");
    }

    public async Task<AssignmentCommandResult> UnassignAsync(int interviewId)
    {
        var interview = await FindInterviewAsync(interviewId);
        if (interview is null)
        {
            return AssignmentCommandResult.NotFound();
        }

        if (interview.Status != StatusConstants.CheckedIn)
        {
            return AssignmentCommandResult.Conflict("Only a checked-in interview can be unassigned.");
        }

        interview.InterviewerTimeslotId = null;
        interview.LocationId = null;
        return await SaveAndPublishAsync(interview, "unassigned");
    }

    public async Task<AssignmentCommandResult> OverrideAsync(int interviewId, string? interviewerId, string actorId)
    {
        if (string.IsNullOrWhiteSpace(interviewerId) || interviewerId == "0")
        {
            return await UnassignAsync(interviewId);
        }

        var interview = await FindInterviewAsync(interviewId);
        if (interview is null)
        {
            return AssignmentCommandResult.NotFound();
        }

        if (interview.Status is not (StatusConstants.CheckedIn or StatusConstants.Ongoing))
        {
            return AssignmentCommandResult.Conflict("Only a checked-in or ongoing interview can be overridden.");
        }

        // The availability relationship is currently also the assigned-interviewer
        // identity. Pick a real row from this event; never create a fictional slot.
        var overrideCandidates = await context.InterviewerTimeslots
            .Include(item => item.InterviewerSignup)
            .Include(item => item.Timeslot)
            .ThenInclude(slot => slot.Event)
            .Where(item => item.InterviewerSignup.InterviewerId == interviewerId &&
                item.Timeslot.EventId == interview.Timeslot.EventId)
            .ToListAsync();
        var carrier = overrideCandidates
            .OrderBy(item => Math.Abs((item.Timeslot.Time - interview.Timeslot.Time).Ticks))
            .ThenBy(item => item.Id)
            .FirstOrDefault();
        if (carrier is null)
        {
            return AssignmentCommandResult.Validation("The selected interviewer has no volunteered availability for this event and cannot be overridden safely.");
        }

        var bypasses = await GetOverrideBypassesAsync(interview, carrier);
        await ApplyAssignmentAsync(interview, carrier);
        if (interview.Status == StatusConstants.CheckedIn)
        {
            interview.Status = StatusConstants.Ongoing;
            interview.StartedAt ??= DateTime.UtcNow;
        }

        logger.LogInformation(
            "Assignment override by {ActorId}: interview {InterviewId}, interviewer {InterviewerId}, bypasses {Bypasses}",
            actorId,
            interview.Id,
            interviewerId,
            bypasses.Count == 0 ? "none" : string.Join(", ", bypasses));

        return await SaveAndPublishAsync(interview, "overridden", bypasses);
    }

    public async Task<AssignmentCommandResult> CompleteAsync(int interviewId, string actorId, bool isAdministrator)
    {
        var interview = await FindInterviewAsync(interviewId);
        if (interview is null)
        {
            return AssignmentCommandResult.NotFound();
        }

        if (!isAdministrator && interview.InterviewerTimeslot?.InterviewerSignup.InterviewerId != actorId)
        {
            return AssignmentCommandResult.Forbidden();
        }

        if (interview.Status != StatusConstants.Ongoing)
        {
            return AssignmentCommandResult.Conflict("Only an ongoing interview can be completed.");
        }

        interview.Status = StatusConstants.Completed;
        interview.EndedAt = DateTime.UtcNow;
        return await SaveAndPublishAsync(interview, "completed");
    }

    public async Task<AssignmentCommandResult> MarkNoShowAsync(int interviewId)
    {
        var interview = await FindInterviewAsync(interviewId);
        if (interview is null)
        {
            return AssignmentCommandResult.NotFound();
        }

        if (interview.Status is not (StatusConstants.Default or StatusConstants.CheckedIn))
        {
            return AssignmentCommandResult.Conflict("Only a waiting interview can be marked as a no-show.");
        }

        interview.Status = StatusConstants.NoShow;
        interview.EndedAt = DateTime.UtcNow;
        return await SaveAndPublishAsync(interview, "marked no-show");
    }

    private async Task<Interview?> FindInterviewAsync(int interviewId) => await context.Interviews
        .Include(item => item.InterviewerTimeslot)
        .ThenInclude(item => item!.InterviewerSignup)
        .Include(item => item.Timeslot)
        .ThenInclude(item => item.Event)
        .SingleOrDefaultAsync(item => item.Id == interviewId);

    private async Task<string?> GetNormalEligibilityFailureAsync(Interview interview, InterviewerTimeslot availability)
    {
        if (!availability.InterviewerSignup.CheckedIn)
        {
            return "The selected interviewer has not checked in.";
        }

        if (!SupportsInterviewType(availability.InterviewerSignup, interview.Type))
        {
            return "The selected interviewer does not support this interview type.";
        }

        var isBusy = await context.Interviews.AnyAsync(item => item.Id != interview.Id &&
            item.Status == StatusConstants.Ongoing &&
            item.InterviewerTimeslot != null &&
            item.InterviewerTimeslot.InterviewerSignup.InterviewerId == availability.InterviewerSignup.InterviewerId);
        return isBusy ? "The selected interviewer is already conducting an interview." : null;
    }

    private async Task<List<string>> GetOverrideBypassesAsync(Interview interview, InterviewerTimeslot carrier)
    {
        var bypasses = new List<string>();
        if (carrier.TimeslotId != interview.TimeslotId)
        {
            bypasses.Add("exact-timeslot availability");
        }

        if (!carrier.InterviewerSignup.CheckedIn)
        {
            bypasses.Add("interviewer check-in");
        }

        if (!SupportsInterviewType(carrier.InterviewerSignup, interview.Type))
        {
            bypasses.Add("interview type");
        }

        var isBusy = await context.Interviews.AnyAsync(item => item.Id != interview.Id &&
            item.Status == StatusConstants.Ongoing &&
            item.InterviewerTimeslot != null &&
            item.InterviewerTimeslot.InterviewerSignup.InterviewerId == carrier.InterviewerSignup.InterviewerId);
        if (isBusy)
        {
            bypasses.Add("current assignment");
        }

        return bypasses;
    }

    private static bool SupportsInterviewType(InterviewerSignup signup, string? interviewType) => interviewType switch
    {
        InterviewTypeConstants.Behavioral => signup.IsBehavioral || signup.Type == InterviewTypeConstants.Behavioral,
        InterviewTypeConstants.Technical => signup.IsTechnical || signup.Type == InterviewTypeConstants.Technical,
        InterviewTypeConstants.Case => signup.IsCase || signup.Type == InterviewTypeConstants.Case,
        _ => false
    };

    private async Task ApplyAssignmentAsync(Interview interview, InterviewerTimeslot availability)
    {
        interview.InterviewerTimeslotId = availability.Id;
        interview.LocationId = await FindLocationIdAsync(availability);
    }

    private async Task<int?> FindLocationIdAsync(InterviewerTimeslot availability)
    {
        var preference = availability.InterviewerSignup switch
        {
            { IsVirtual: true, InPerson: true } => InterviewLocationConstants.InPerson + "/" + InterviewLocationConstants.IsVirtual,
            { IsVirtual: true } => InterviewLocationConstants.IsVirtual,
            { InPerson: true } => InterviewLocationConstants.InPerson,
            _ => string.Empty
        };

        return await context.InterviewerLocations
            .Where(item => item.InterviewerId == availability.InterviewerSignup.InterviewerId &&
                item.EventId == availability.Timeslot.EventId &&
                item.Preference == preference &&
                item.LocationId != null)
            .Select(item => item.LocationId)
            .FirstOrDefaultAsync();
    }

    private async Task<AssignmentCommandResult> SaveAndPublishAsync(
        Interview interview,
        string action,
        IReadOnlyList<string>? bypasses = null)
    {
        try
        {
            await context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return AssignmentCommandResult.Conflict("This interview changed in another session. Refresh and try again.");
        }

        await assignmentHub.Clients.All.SendAsync("BoardChanged", interview.Id);
        logger.LogInformation("Interview {InterviewId} {Action}.", interview.Id, action);
        return AssignmentCommandResult.Success(bypasses);
    }
}

public sealed record AssignmentCommandResult(
    AssignmentCommandStatus Status,
    string? Message = null,
    IReadOnlyList<string>? Bypasses = null)
{
    public static AssignmentCommandResult Success(IReadOnlyList<string>? bypasses = null) => new(AssignmentCommandStatus.Success, Bypasses: bypasses);
    public static AssignmentCommandResult Validation(string message) => new(AssignmentCommandStatus.Validation, message);
    public static AssignmentCommandResult Conflict(string message) => new(AssignmentCommandStatus.Conflict, message);
    public static AssignmentCommandResult NotFound() => new(AssignmentCommandStatus.NotFound);
    public static AssignmentCommandResult Forbidden() => new(AssignmentCommandStatus.Forbidden);
}

public enum AssignmentCommandStatus
{
    Success,
    Validation,
    Conflict,
    NotFound,
    Forbidden
}
