using Microsoft.EntityFrameworkCore;
using MockInterviews.Data.Constants;
using MockInterviews.Data.Contexts;
using MockInterviews.Models.Entities;
using MockInterviews.Models.Identity;
using MockInterviews.Models.ViewModels.HomeController;

namespace MockInterviews.Services;

public sealed class DashboardService(
    MockInterviewsDbContext context,
    SettingsService settingsService)
{
    public async Task<PublicHomeViewModel> BuildPublicHomeAsync()
        => new(await IsEnabledAsync(
            SettingsConstants.DisruptionBanner.Name,
            SettingsConstants.DisruptionBanner.DefaultValue));

    public async Task<DashboardViewModel> BuildDashboardAsync(
        ApplicationUser user,
        bool includeStudentSchedule,
        bool includeInterviewerSchedule)
    {
        var showServiceDisruption = await IsEnabledAsync(
            SettingsConstants.DisruptionBanner.Name,
            SettingsConstants.DisruptionBanner.DefaultValue);
        var showMeeting = await IsEnabledAsync(
            SettingsConstants.ZoomLinkVisible.Name,
            SettingsConstants.ZoomLinkVisible.DefaultValue);
        DashboardMeetingViewModel? meeting = null;

        if (showMeeting)
        {
            var zoomLink = await settingsService.GetSettingByName(
                SettingsConstants.ZoomLink.Name,
                SettingsConstants.ZoomLink.DefaultValue);
            if (Uri.TryCreate(zoomLink.Value, UriKind.Absolute, out var meetingUri) &&
                (meetingUri.Scheme == Uri.UriSchemeHttps || meetingUri.Scheme == Uri.UriSchemeHttp))
            {
                meeting = new DashboardMeetingViewModel(meetingUri.AbsoluteUri);
            }
        }

        var student = includeStudentSchedule
            ? await BuildStudentDashboardAsync(user)
            : null;
        var interviewer = includeInterviewerSchedule
            ? await BuildInterviewerDashboardAsync(user.Id)
            : null;

        return new DashboardViewModel(
            GetDisplayName(user),
            showServiceDisruption,
            meeting,
            student,
            interviewer);
    }

    private async Task<StudentDashboardViewModel> BuildStudentDashboardAsync(ApplicationUser user)
    {
        var interviews = await context.Interviews
            .AsNoTracking()
            .Include(interview => interview.InterviewerTimeslot)
                .ThenInclude(timeslot => timeslot!.InterviewerSignup)
            .Include(interview => interview.Location)
            .Include(interview => interview.Timeslot)
                .ThenInclude(timeslot => timeslot.Event)
            .Where(interview => interview.StudentId == user.Id && interview.Timeslot.Event.IsActive)
            .OrderBy(interview => interview.Timeslot.Event.Date)
            .ThenBy(interview => interview.Timeslot.Time)
            .ToListAsync();

        var interviewerIds = interviews
            .Where(interview => interview.InterviewerTimeslot is not null)
            .Select(interview => interview.InterviewerTimeslot!.InterviewerSignup.InterviewerId)
            .Distinct()
            .ToList();
        var interviewerNames = await GetUserNamesAsync(interviewerIds);

        var scheduleItems = interviews.Select(interview =>
        {
            var interviewerId = interview.InterviewerTimeslot?.InterviewerSignup.InterviewerId;
            var interviewerName = interviewerId is not null && interviewerNames.TryGetValue(interviewerId, out var name)
                ? name
                : "Not assigned";

            return new StudentInterviewScheduleItemViewModel(
                interview.Id,
                interview.Timeslot.Event.Date,
                interview.Timeslot.Time,
                interview.Type ?? "Not assigned",
                interview.Status,
                interview.Location?.Room ?? "Not assigned",
                interviewerName);
        }).ToList();

        var volunteerTimeslots = await context.VolunteerTimeslots
            .AsNoTracking()
            .Include(volunteerTimeslot => volunteerTimeslot.Timeslot)
                .ThenInclude(timeslot => timeslot.Event)
            .Where(volunteerTimeslot =>
                volunteerTimeslot.StudentId == user.Id && volunteerTimeslot.Timeslot.Event.IsActive)
            .OrderBy(volunteerTimeslot => volunteerTimeslot.Timeslot.Event.Date)
            .ThenBy(volunteerTimeslot => volunteerTimeslot.Timeslot.Time)
            .ToListAsync();

        return new StudentDashboardViewModel(scheduleItems, BuildVolunteerRanges(volunteerTimeslots));
    }

    private async Task<InterviewerDashboardViewModel> BuildInterviewerDashboardAsync(string userId)
    {
        var assignedInterviews = await context.Interviews
            .AsNoTracking()
            .Include(interview => interview.InterviewerTimeslot)
                .ThenInclude(timeslot => timeslot!.InterviewerSignup)
            .Include(interview => interview.Timeslot)
                .ThenInclude(timeslot => timeslot.Event)
            .Where(interview =>
                interview.InterviewerTimeslot != null &&
                interview.InterviewerTimeslot.InterviewerSignup.InterviewerId == userId &&
                interview.Timeslot.Event.IsActive &&
                (interview.Status == StatusConstants.Ongoing || interview.Status == StatusConstants.CheckedIn))
            .OrderBy(interview => interview.Timeslot.Event.Date)
            .ThenBy(interview => interview.Timeslot.Time)
            .ToListAsync();

        var completedInterviews = await context.Interviews
            .AsNoTracking()
            .Include(interview => interview.InterviewerTimeslot)
                .ThenInclude(timeslot => timeslot!.InterviewerSignup)
            .Include(interview => interview.Timeslot)
                .ThenInclude(timeslot => timeslot.Event)
            .Where(interview =>
                interview.InterviewerTimeslot != null &&
                interview.InterviewerTimeslot.InterviewerSignup.InterviewerId == userId &&
                interview.Timeslot.Event.IsActive &&
                interview.Status == StatusConstants.Completed)
            .OrderByDescending(interview => interview.Timeslot.Event.Date)
            .ThenByDescending(interview => interview.Timeslot.Time)
            .ToListAsync();

        var studentIds = assignedInterviews
            .Concat(completedInterviews)
            .Select(interview => interview.StudentId)
            .Distinct()
            .ToList();
        var students = await context.Users
            .AsNoTracking()
            .Where(user => studentIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id);

        var activeItems = assignedInterviews.Select(interview =>
        {
            students.TryGetValue(interview.StudentId, out var student);
            return new ActiveInterviewViewModel(
                interview.Id,
                interview.StudentId,
                GetDisplayName(student),
                student is null ? string.Empty : ClassConstants.GetClassText(student.Class),
                interview.Type ?? "Not assigned",
                interview.Status,
                interview.StartedAt);
        }).ToList();

        var completedItems = completedInterviews.Select(interview =>
        {
            students.TryGetValue(interview.StudentId, out var student);
            return new CompletedInterviewViewModel(
                interview.Id,
                interview.StudentId,
                GetDisplayName(student),
                interview.Timeslot.Event.Date,
                interview.Timeslot.Time,
                interview.Type ?? "Not assigned");
        }).ToList();

        var availability = await context.InterviewerTimeslots
            .AsNoTracking()
            .Include(interviewerTimeslot => interviewerTimeslot.InterviewerSignup)
            .Include(interviewerTimeslot => interviewerTimeslot.Timeslot)
                .ThenInclude(timeslot => timeslot.Event)
            .Where(interviewerTimeslot =>
                interviewerTimeslot.InterviewerSignup.InterviewerId == userId &&
                interviewerTimeslot.Timeslot.Event.IsActive)
            .OrderBy(interviewerTimeslot => interviewerTimeslot.InterviewerSignupId)
            .ThenBy(interviewerTimeslot => interviewerTimeslot.Timeslot.Event.Date)
            .ThenBy(interviewerTimeslot => interviewerTimeslot.Timeslot.Time)
            .ToListAsync();

        return new InterviewerDashboardViewModel(
            activeItems,
            BuildInterviewerScheduleGroups(availability),
            completedItems);
    }

    private async Task<Dictionary<string, string>> GetUserNamesAsync(List<string> userIds)
        => await context.Users
            .AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, GetDisplayName);

    private async Task<bool> IsEnabledAsync(string name, string defaultValue)
        => await settingsService.GetIntegerSettingByName(name, defaultValue) != 0;

    private static List<VolunteerScheduleItemViewModel> BuildVolunteerRanges(
        IReadOnlyList<VolunteerTimeslot> timeslots)
    {
        var ranges = new List<VolunteerScheduleItemViewModel>();
        if (timeslots.Count == 0)
        {
            return ranges;
        }

        var current = new List<VolunteerTimeslot> { timeslots[0] };
        foreach (var timeslot in timeslots.Skip(1))
        {
            var previous = current[^1];
            if (IsContiguous(previous.Timeslot, timeslot.Timeslot))
            {
                current.Add(timeslot);
                continue;
            }

            ranges.Add(ToVolunteerRange(current));
            current = [timeslot];
        }

        ranges.Add(ToVolunteerRange(current));
        return ranges;
    }

    private static List<InterviewerScheduleGroupViewModel> BuildInterviewerScheduleGroups(
        IReadOnlyList<InterviewerTimeslot> timeslots)
        => timeslots
            .GroupBy(timeslot => timeslot.InterviewerSignupId)
            .Select(group =>
            {
                var signup = group.First().InterviewerSignup;
                return new InterviewerScheduleGroupViewModel(
                    group.Key,
                    signup.InPerson ? InterviewLocationConstants.InPerson : InterviewLocationConstants.IsVirtual,
                    signup.Type ?? "Not assigned",
                    BuildScheduleRanges(group.Select(item => item.Timeslot).ToList()));
            })
            .ToList();

    private static List<ScheduleRangeViewModel> BuildScheduleRanges(IReadOnlyList<Timeslot> timeslots)
    {
        var ranges = new List<ScheduleRangeViewModel>();
        if (timeslots.Count == 0)
        {
            return ranges;
        }

        var rangeStart = timeslots[0];
        var rangeEnd = timeslots[0];
        foreach (var timeslot in timeslots.Skip(1))
        {
            if (IsContiguous(rangeEnd, timeslot))
            {
                rangeEnd = timeslot;
                continue;
            }

            ranges.Add(ToScheduleRange(rangeStart, rangeEnd));
            rangeStart = timeslot;
            rangeEnd = timeslot;
        }

        ranges.Add(ToScheduleRange(rangeStart, rangeEnd));
        return ranges;
    }

    private static bool IsContiguous(Timeslot previous, Timeslot next)
        => previous.Event.Date.Date == next.Event.Date.Date &&
            previous.Time.AddMinutes(30).TimeOfDay == next.Time.TimeOfDay;

    private static VolunteerScheduleItemViewModel ToVolunteerRange(IReadOnlyList<VolunteerTimeslot> timeslots)
        => new(
            timeslots[0].Timeslot.Event.Date,
            FormatTime(timeslots[0].Timeslot.Time),
            FormatTime(timeslots[^1].Timeslot.Time.AddMinutes(30)),
            timeslots.Select(timeslot => timeslot.Id).ToList());

    private static ScheduleRangeViewModel ToScheduleRange(Timeslot start, Timeslot end)
        => new(start.Event.Date, FormatTime(start.Time), FormatTime(end.Time.AddMinutes(30)));

    private static string FormatTime(DateTime time) => time.ToString("h:mm tt");

    private static string GetDisplayName(ApplicationUser? user)
    {
        if (user is null)
        {
            return "Deleted user";
        }

        var displayName = $"{user.FirstName} {user.LastName}".Trim();
        return string.IsNullOrWhiteSpace(displayName) ? user.Email ?? "User" : displayName;
    }
}
