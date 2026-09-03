using Microsoft.EntityFrameworkCore;
using MockInterviews.Data.Constants;
using MockInterviews.Data.Contexts;
using MockInterviews.Models.Entities;
using MockInterviews.Models.ViewModels.InterviewEventsController;

namespace MockInterviews.Services;

/// <summary>
/// Reads the assignment board in a bounded number of queries. This is intentionally
/// separate from command handling so every browser receives the same server-owned
/// eligibility and display state.
/// </summary>
public sealed class AssignmentBoardQueryService(MockInterviewsDbContext context)
{
    public async Task<AssignmentBoardViewModel> BuildAsync()
    {
        var horizon = await GetBoardHorizonAsync();
        var interviews = await context.Interviews
            .AsNoTracking()
            .Include(interview => interview.Location)
            .Include(interview => interview.InterviewerTimeslot)
            .ThenInclude(availability => availability!.InterviewerSignup)
            .Include(interview => interview.Timeslot)
            .ThenInclude(timeslot => timeslot.Event)
            .Where(interview => interview.Timeslot.Event.IsActive &&
                interview.Status != StatusConstants.Completed &&
                interview.Status != StatusConstants.NoShow &&
                interview.Status != StatusConstants.Excused)
            .OrderBy(interview => interview.Timeslot.Event.Date)
            .ThenBy(interview => interview.Timeslot.Time)
            .ThenBy(interview => interview.Id)
            .Take(horizon)
            .ToListAsync();

        var studentIds = interviews.Select(interview => interview.StudentId).Distinct().ToArray();
        var interviewerIds = interviews
            .Where(interview => interview.InterviewerTimeslot is not null)
            .Select(interview => interview.InterviewerTimeslot!.InterviewerSignup.InterviewerId)
            .Distinct()
            .ToArray();
        var userIds = studentIds.Concat(interviewerIds).Distinct().ToArray();
        var users = await context.Users
            .AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .Select(user => new UserSummary(user.Id, user.FirstName + " " + user.LastName, user.Class))
            .ToDictionaryAsync(user => user.Id);

        var boardTimeslotIds = interviews.Select(interview => interview.TimeslotId).Distinct().ToArray();
        var busyInterviewerIds = await context.Interviews
            .AsNoTracking()
            .Where(interview => interview.Status == StatusConstants.Ongoing &&
                interview.InterviewerTimeslot != null)
            .Select(interview => interview.InterviewerTimeslot!.InterviewerSignup.InterviewerId)
            .Distinct()
            .ToListAsync();
        var busySet = busyInterviewerIds.ToHashSet(StringComparer.Ordinal);

        var exactAvailability = await context.InterviewerTimeslots
            .AsNoTracking()
            .Include(availability => availability.InterviewerSignup)
            .Include(availability => availability.Timeslot)
            .ThenInclude(timeslot => timeslot.Event)
            .Where(availability => boardTimeslotIds.Contains(availability.TimeslotId))
            .ToListAsync();
        var eventIds = interviews.Select(interview => interview.Timeslot.EventId).Distinct().ToArray();
        var eventAvailability = await context.InterviewerTimeslots
            .AsNoTracking()
            .Include(availability => availability.InterviewerSignup)
            .Include(availability => availability.Timeslot)
            .ThenInclude(timeslot => timeslot.Event)
            .Where(availability => eventIds.Contains(availability.Timeslot.EventId))
            .ToListAsync();
        var availabilityInterviewerIds = eventAvailability
            .Select(availability => availability.InterviewerSignup.InterviewerId)
            .Concat(await context.InterviewerSignups
                .AsNoTracking()
                .Where(signup => signup.CheckedIn && !busySet.Contains(signup.InterviewerId))
                .Select(signup => signup.InterviewerId)
                .ToListAsync())
            .Distinct()
            .ToArray();
        var missingUserIds = availabilityInterviewerIds.Except(users.Keys).ToArray();
        if (missingUserIds.Length > 0)
        {
            var additionalUsers = await context.Users
                .AsNoTracking()
                .Where(user => missingUserIds.Contains(user.Id))
                .Select(user => new UserSummary(user.Id, user.FirstName + " " + user.LastName, user.Class))
                .ToListAsync();
            foreach (var user in additionalUsers)
            {
                users[user.Id] = user;
            }
        }

        var roomByInterviewerAndEvent = await BuildRoomIndexAsync(eventIds);
        var normalCandidatesByInterview = BuildNormalCandidateIndex(
            interviews,
            exactAvailability,
            busySet,
            users,
            roomByInterviewerAndEvent);
        var overrideCandidatesByInterview = BuildOverrideCandidateIndex(
            interviews,
            eventAvailability,
            busySet,
            users);

        var rows = interviews.Select(interview => ToRow(
            interview,
            users,
            normalCandidatesByInterview.GetValueOrDefault(interview.Id, []),
            overrideCandidatesByInterview.GetValueOrDefault(interview.Id, []))).ToList();

        return new AssignmentBoardViewModel(
            rows.Where(row => row.Status == StatusConstants.CheckedIn).ToList(),
            rows.Where(row => row.Status == StatusConstants.Ongoing).ToList(),
            rows.Where(row => row.Status == StatusConstants.Default).ToList(),
            await BuildAvailableInterviewersAsync(busySet, users));
    }

    private async Task<int> GetBoardHorizonAsync()
    {
        var configuredHours = await context.Settings
            .Where(setting => setting.Name == "interview_index_hours")
            .Select(setting => setting.Value)
            .FirstOrDefaultAsync();
        if (!int.TryParse(configuredHours, out var hours) || hours < 1)
        {
            throw new InvalidOperationException("Setting 'interview_index_hours' must be a positive integer.");
        }

        var capacity = await context.Timeslots
            .OrderByDescending(timeslot => timeslot.MaxSignUps)
            .Select(timeslot => timeslot.MaxSignUps)
            .FirstOrDefaultAsync();
        return capacity * hours * 2; // Existing operational horizon; retained for Step 8.
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

    private static Dictionary<int, IReadOnlyList<AssignmentBoardCandidateViewModel>> BuildNormalCandidateIndex(
        IReadOnlyList<Interview> interviews,
        IReadOnlyList<InterviewerTimeslot> availability,
        IReadOnlySet<string> busyInterviewerIds,
        IReadOnlyDictionary<string, UserSummary> users,
        IReadOnlyDictionary<(string InterviewerId, int EventId), string> rooms)
    {
        var result = new Dictionary<int, IReadOnlyList<AssignmentBoardCandidateViewModel>>();
        foreach (var interview in interviews)
        {
            var candidates = availability
                .Where(item => item.TimeslotId == interview.TimeslotId &&
                    item.InterviewerSignup.CheckedIn &&
                    !busyInterviewerIds.Contains(item.InterviewerSignup.InterviewerId) &&
                    SupportsInterviewType(item.InterviewerSignup, interview.Type))
                .GroupBy(item => item.InterviewerSignup.InterviewerId)
                .Select(group =>
                {
                    var interviewerId = group.Key;
                    return new AssignmentBoardCandidateViewModel(
                        interviewerId,
                        NameFor(interviewerId, users),
                        rooms.GetValueOrDefault((interviewerId, interview.Timeslot.EventId), "Not assigned"));
                })
                .OrderBy(candidate => candidate.Name)
                .ToList();
            result[interview.Id] = candidates;
        }

        return result;
    }

    private static Dictionary<int, IReadOnlyList<AssignmentBoardOverrideCandidateViewModel>> BuildOverrideCandidateIndex(
        IReadOnlyList<Interview> interviews,
        IReadOnlyList<InterviewerTimeslot> availability,
        IReadOnlySet<string> busyInterviewerIds,
        IReadOnlyDictionary<string, UserSummary> users)
    {
        var result = new Dictionary<int, IReadOnlyList<AssignmentBoardOverrideCandidateViewModel>>();
        foreach (var interview in interviews)
        {
            var candidates = availability
                .Where(item => item.Timeslot.EventId == interview.Timeslot.EventId)
                .GroupBy(item => item.InterviewerSignup.InterviewerId)
                .Select(group =>
                {
                    var carrier = group
                        .OrderBy(item => Math.Abs((item.Timeslot.Time - interview.Timeslot.Time).Ticks))
                        .ThenBy(item => item.Id)
                        .First();
                    var bypasses = new List<string>();
                    if (carrier.TimeslotId != interview.TimeslotId) bypasses.Add("exact-timeslot availability");
                    if (!carrier.InterviewerSignup.CheckedIn) bypasses.Add("interviewer check-in");
                    if (!SupportsInterviewType(carrier.InterviewerSignup, interview.Type)) bypasses.Add("interview type");
                    if (busyInterviewerIds.Contains(carrier.InterviewerSignup.InterviewerId)) bypasses.Add("current assignment");
                    return new AssignmentBoardOverrideCandidateViewModel(group.Key, NameFor(group.Key, users), bypasses);
                })
                .OrderBy(candidate => candidate.Name)
                .ToList();
            result[interview.Id] = candidates;
        }

        return result;
    }

    private async Task<IReadOnlyList<AssignmentBoardAvailableInterviewerViewModel>> BuildAvailableInterviewersAsync(
        IReadOnlySet<string> busyInterviewerIds,
        IReadOnlyDictionary<string, UserSummary> users)
    {
        var signups = await context.InterviewerSignups
            .AsNoTracking()
            .Where(signup => signup.CheckedIn && !busyInterviewerIds.Contains(signup.InterviewerId))
            .OrderBy(signup => signup.InterviewerId)
            .ToListAsync();
        return signups
            .GroupBy(signup => signup.InterviewerId)
            .Select(group =>
            {
                var signup = group.First();
                return new AssignmentBoardAvailableInterviewerViewModel(
                    signup.InterviewerId,
                    NameFor(signup.InterviewerId, users),
                    DescribeInterviewTypes(signup));
            })
            .OrderBy(interviewer => interviewer.Name)
            .ToList();
    }

    private static AssignmentBoardInterviewViewModel ToRow(
        Interview interview,
        IReadOnlyDictionary<string, UserSummary> users,
        IReadOnlyList<AssignmentBoardCandidateViewModel> normalCandidates,
        IReadOnlyList<AssignmentBoardOverrideCandidateViewModel> overrideCandidates)
    {
        var assignedInterviewerId = interview.InterviewerTimeslot?.InterviewerSignup.InterviewerId;
        var isOverride = interview.InterviewerTimeslot is not null && interview.InterviewerTimeslot.TimeslotId != interview.TimeslotId;
        return new AssignmentBoardInterviewViewModel(
            interview.Id,
            NameFor(interview.StudentId, users),
            ClassFor(interview.StudentId, users),
            interview.Timeslot.Event.Date,
            interview.Timeslot.Time,
            interview.Type ?? "Not specified",
            interview.Status,
            interview.CheckedInAt,
            interview.StartedAt,
            assignedInterviewerId is null ? "Not assigned" : NameFor(assignedInterviewerId, users),
            interview.Location?.Room ?? "Not assigned",
            isOverride,
            normalCandidates,
            overrideCandidates);
    }

    private static bool SupportsInterviewType(InterviewerSignup signup, string? interviewType) => interviewType switch
    {
        InterviewTypeConstants.Behavioral => signup.IsBehavioral || signup.Type == InterviewTypeConstants.Behavioral,
        InterviewTypeConstants.Technical => signup.IsTechnical || signup.Type == InterviewTypeConstants.Technical,
        InterviewTypeConstants.Case => signup.IsCase || signup.Type == InterviewTypeConstants.Case,
        _ => false
    };

    private static string DescribeInterviewTypes(InterviewerSignup signup)
    {
        var types = new List<string>();
        if (signup.IsBehavioral || signup.Type == InterviewTypeConstants.Behavioral) types.Add(InterviewTypeConstants.Behavioral);
        if (signup.IsTechnical || signup.Type == InterviewTypeConstants.Technical) types.Add(InterviewTypeConstants.Technical);
        if (signup.IsCase || signup.Type == InterviewTypeConstants.Case) types.Add(InterviewTypeConstants.Case);
        return types.Count == 0 ? "No type selected" : string.Join(", ", types);
    }

    private static string NameFor(string userId, IReadOnlyDictionary<string, UserSummary> users)
        => users.TryGetValue(userId, out var user) ? user.Name : "Deleted user";

    private static string ClassFor(string userId, IReadOnlyDictionary<string, UserSummary> users)
        => users.TryGetValue(userId, out var user) ? ClassConstants.GetClassText(user.Class) : string.Empty;

    private sealed record UserSummary(string Id, string Name, Classes Class);
}
