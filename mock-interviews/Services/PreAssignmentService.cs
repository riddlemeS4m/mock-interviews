using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MockInterviews.Data.Constants;
using MockInterviews.Data.Contexts;
using MockInterviews.Models.Entities;
using MockInterviews.Models.ViewModels.InterviewEventsController;
using MockInterviews.Services.SignalR;

namespace MockInterviews.Services;

/// <summary>
/// Builds and applies planned assignments before students arrive. The page is
/// divided by exact timeslot, and each submitted group is all-or-nothing.
/// </summary>
public sealed class PreAssignmentService(
    MockInterviewsDbContext context,
    IHubContext<AssignInterviewsHub> assignmentHub,
    ILogger<PreAssignmentService> logger)
{
    public async Task<PreAssignmentPageViewModel> BuildAsync()
    {
        var interviews = await context.Interviews
            .AsNoTracking()
            .Include(interview => interview.InterviewerTimeslot)
            .ThenInclude(availability => availability!.InterviewerSignup)
            .Include(interview => interview.Timeslot)
            .ThenInclude(timeslot => timeslot.Event)
            .Where(interview => interview.Status == StatusConstants.Default &&
                interview.Timeslot.IsActive && interview.Timeslot.Event.IsActive)
            .OrderBy(interview => interview.Timeslot.Event.Date)
            .ThenBy(interview => interview.Timeslot.Time)
            .ThenBy(interview => interview.Id)
            .ToListAsync();

        var timeslotIds = interviews.Select(interview => interview.TimeslotId).Distinct().ToArray();
        var availability = await context.InterviewerTimeslots
            .AsNoTracking()
            .Include(item => item.InterviewerSignup)
            .Include(item => item.Timeslot)
            .ThenInclude(timeslot => timeslot.Event)
            .Where(item => timeslotIds.Contains(item.TimeslotId))
            .ToListAsync();
        var busyInterviewerIds = await context.Interviews
            .AsNoTracking()
            .Where(interview => interview.Status == StatusConstants.Ongoing && interview.InterviewerTimeslot != null)
            .Select(interview => interview.InterviewerTimeslot!.InterviewerSignup.InterviewerId)
            .Distinct()
            .ToListAsync();
        var busySet = busyInterviewerIds.ToHashSet(StringComparer.Ordinal);

        var userIds = interviews.Select(interview => interview.StudentId)
            .Concat(availability.Select(item => item.InterviewerSignup.InterviewerId))
            .Concat(interviews.Where(interview => interview.InterviewerTimeslot is not null)
                .Select(interview => interview.InterviewerTimeslot!.InterviewerSignup.InterviewerId))
            .Distinct()
            .ToArray();
        var users = await context.Users
            .AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .Select(user => new PreAssignmentUserSummary(user.Id, user.FirstName + " " + user.LastName, user.Class))
            .ToDictionaryAsync(user => user.Id);
        var eventIds = interviews.Select(interview => interview.Timeslot.EventId).Distinct().ToArray();
        var rooms = await BuildRoomIndexAsync(eventIds);

        var groups = interviews
            .GroupBy(interview => interview.TimeslotId)
            .Select(group =>
            {
                var first = group.First();
                var rows = group.Select(interview => new PreAssignmentInterviewViewModel(
                    interview.Id,
                    NameFor(interview.StudentId, users),
                    ClassFor(interview.StudentId, users),
                    interview.Type ?? "Not specified",
                    interview.InterviewerTimeslot?.InterviewerSignup.InterviewerId,
                    availability
                        .Where(item => item.TimeslotId == interview.TimeslotId &&
                            item.InterviewerSignup.CheckedIn &&
                            !busySet.Contains(item.InterviewerSignup.InterviewerId) &&
                            SupportsInterviewType(item.InterviewerSignup, interview.Type))
                        .GroupBy(item => item.InterviewerSignup.InterviewerId)
                        .Select(candidate => new PreAssignmentCandidateViewModel(
                            candidate.Key,
                            NameFor(candidate.Key, users),
                            rooms.GetValueOrDefault((candidate.Key, interview.Timeslot.EventId), "Not assigned")))
                        .OrderBy(candidate => candidate.Name)
                        .ToList()))
                    .ToList();
                return new PreAssignmentTimeslotViewModel(
                    first.TimeslotId,
                    first.Timeslot.Event.Name,
                    first.Timeslot.Event.Date,
                    first.Timeslot.Time,
                    rows);
            })
            .ToList();

        return new PreAssignmentPageViewModel(groups);
    }

    public async Task<PreAssignmentCommandResult> ApplyAsync(PreAssignmentTimeslotRequest request)
    {
        if (request.TimeslotId <= 0 || request.Assignments.Count == 0)
        {
            return PreAssignmentCommandResult.Validation("Choose at least one interview to update.");
        }

        if (request.Assignments.Any(item => item.InterviewId <= 0) ||
            request.Assignments.GroupBy(item => item.InterviewId).Any(group => group.Count() > 1))
        {
            return PreAssignmentCommandResult.Validation("The submitted interview selections are invalid.");
        }

        await using var transaction = await context.Database.BeginTransactionAsync();
        var interviewIds = request.Assignments.Select(item => item.InterviewId).ToArray();
        var interviews = await context.Interviews
            .Include(interview => interview.Timeslot)
            .ThenInclude(timeslot => timeslot.Event)
            .Where(interview => interviewIds.Contains(interview.Id) &&
                interview.TimeslotId == request.TimeslotId)
            .ToListAsync();
        if (interviews.Count != interviewIds.Length || interviews.Any(interview =>
                interview.Status != StatusConstants.Default ||
                !interview.Timeslot.IsActive ||
                !interview.Timeslot.Event.IsActive))
        {
            return PreAssignmentCommandResult.Conflict("One or more interviews changed and can no longer be pre-assigned. Refresh the page and try again.");
        }

        var requestedInterviewerIds = request.Assignments
            .Select(item => item.InterviewerId?.Trim())
            .Where(id => !string.IsNullOrWhiteSpace(id) && id != "0")
            .Cast<string>()
            .ToArray();
        if (requestedInterviewerIds.GroupBy(id => id, StringComparer.Ordinal).Any(group => group.Count() > 1))
        {
            return PreAssignmentCommandResult.Validation("An interviewer can only be planned for one interview in this timeslot.");
        }

        var availability = await context.InterviewerTimeslots
            .Include(item => item.InterviewerSignup)
            .Include(item => item.Timeslot)
            .Where(item => item.TimeslotId == request.TimeslotId &&
                requestedInterviewerIds.Contains(item.InterviewerSignup.InterviewerId))
            .OrderBy(item => item.Id)
            .ToListAsync();
        var availabilityByInterviewer = availability
            .GroupBy(item => item.InterviewerSignup.InterviewerId)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        if (availabilityByInterviewer.Count != requestedInterviewerIds.Distinct(StringComparer.Ordinal).Count())
        {
            return PreAssignmentCommandResult.Validation("Each selected interviewer must have availability for this exact timeslot.");
        }

        var selectedAvailabilityIds = availabilityByInterviewer.Values.Select(item => item.Id).ToArray();
        var busyInterviewerIds = await context.Interviews
            .Where(interview => interview.Status == StatusConstants.Ongoing &&
                interview.InterviewerTimeslot != null &&
                requestedInterviewerIds.Contains(interview.InterviewerTimeslot.InterviewerSignup.InterviewerId))
            .Select(interview => interview.InterviewerTimeslot!.InterviewerSignup.InterviewerId)
            .Distinct()
            .ToListAsync();
        var occupiedAvailabilityIds = await context.Interviews
            .Where(interview => interview.InterviewerTimeslotId != null &&
                selectedAvailabilityIds.Contains(interview.InterviewerTimeslotId.Value) &&
                !interviewIds.Contains(interview.Id) &&
                interview.Status != StatusConstants.Completed &&
                interview.Status != StatusConstants.NoShow &&
                interview.Status != StatusConstants.Excused)
            .Select(interview => interview.InterviewerTimeslotId!.Value)
            .Distinct()
            .ToListAsync();

        foreach (var requestItem in request.Assignments)
        {
            var interviewerId = requestItem.InterviewerId?.Trim();
            if (string.IsNullOrWhiteSpace(interviewerId) || interviewerId == "0")
            {
                continue;
            }

            var interview = interviews.Single(item => item.Id == requestItem.InterviewId);
            var candidate = availabilityByInterviewer[interviewerId];
            if (!candidate.InterviewerSignup.CheckedIn)
            {
                return PreAssignmentCommandResult.Validation("A selected interviewer has not checked in.");
            }

            if (!SupportsInterviewType(candidate.InterviewerSignup, interview.Type))
            {
                return PreAssignmentCommandResult.Validation("A selected interviewer does not support this interview type.");
            }

            if (busyInterviewerIds.Contains(interviewerId, StringComparer.Ordinal))
            {
                return PreAssignmentCommandResult.Conflict("A selected interviewer is already conducting an interview.");
            }

            if (occupiedAvailabilityIds.Contains(candidate.Id))
            {
                return PreAssignmentCommandResult.Conflict("A selected interviewer already has a planned interview for this timeslot.");
            }
        }

        foreach (var requestItem in request.Assignments)
        {
            var interview = interviews.Single(item => item.Id == requestItem.InterviewId);
            var interviewerId = requestItem.InterviewerId?.Trim();
            if (string.IsNullOrWhiteSpace(interviewerId) || interviewerId == "0")
            {
                interview.InterviewerTimeslotId = null;
                interview.LocationId = null;
                continue;
            }

            var candidate = availabilityByInterviewer[interviewerId];
            interview.InterviewerTimeslotId = candidate.Id;
            interview.LocationId = await FindLocationIdAsync(candidate);
        }

        try
        {
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return PreAssignmentCommandResult.Conflict("These interviews changed in another session. Refresh and try again.");
        }

        await assignmentHub.Clients.All.SendAsync("BoardChanged");
        logger.LogInformation("Pre-assigned {InterviewCount} interviews for timeslot {TimeslotId}.", interviews.Count, request.TimeslotId);
        return PreAssignmentCommandResult.Success();
    }

    private async Task<Dictionary<(string InterviewerId, int EventId), string>> BuildRoomIndexAsync(int[] eventIds)
    {
        var locations = await context.InterviewerLocations
            .AsNoTracking()
            .Include(location => location.Location)
            .Where(location => location.EventId != null && eventIds.Contains(location.EventId.Value))
            .OrderBy(location => location.Id)
            .ToListAsync();
        return locations
            .GroupBy(location => (location.InterviewerId, location.EventId!.Value))
            .ToDictionary(
                group => group.Key,
                group => group.Select(location => location.Location?.Room).FirstOrDefault(room => !string.IsNullOrWhiteSpace(room)) ?? "Not assigned");
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

    private static bool SupportsInterviewType(InterviewerSignup signup, string? interviewType) => interviewType switch
    {
        InterviewTypeConstants.Behavioral => signup.IsBehavioral || signup.Type == InterviewTypeConstants.Behavioral,
        InterviewTypeConstants.Technical => signup.IsTechnical || signup.Type == InterviewTypeConstants.Technical,
        InterviewTypeConstants.Case => signup.IsCase || signup.Type == InterviewTypeConstants.Case,
        _ => false
    };

    private static string NameFor(string userId, IReadOnlyDictionary<string, PreAssignmentUserSummary> users)
        => users.TryGetValue(userId, out var user) ? user.Name : "Deleted user";

    private static string ClassFor(string userId, IReadOnlyDictionary<string, PreAssignmentUserSummary> users)
        => users.TryGetValue(userId, out var user)
            ? ClassConstants.GetClassText(user.Class)
            : string.Empty;

    private sealed record PreAssignmentUserSummary(string Id, string Name, Classes Class);
}

public sealed record PreAssignmentCommandResult(PreAssignmentCommandStatus Status, string? Message = null)
{
    public static PreAssignmentCommandResult Success() => new(PreAssignmentCommandStatus.Success);
    public static PreAssignmentCommandResult Validation(string message) => new(PreAssignmentCommandStatus.Validation, message);
    public static PreAssignmentCommandResult Conflict(string message) => new(PreAssignmentCommandStatus.Conflict, message);
}

public enum PreAssignmentCommandStatus
{
    Success,
    Validation,
    Conflict
}
