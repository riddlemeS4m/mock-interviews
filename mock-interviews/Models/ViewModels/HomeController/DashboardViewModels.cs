namespace MockInterviews.Models.ViewModels.HomeController;

public sealed record PublicHomeViewModel(bool ShowServiceDisruption);

public sealed record DashboardViewModel(
    string DisplayName,
    bool ShowServiceDisruption,
    DashboardMeetingViewModel? Meeting,
    StudentDashboardViewModel? Student,
    InterviewerDashboardViewModel? Interviewer);

public sealed record DashboardMeetingViewModel(string Url);

public sealed record StudentDashboardViewModel(
    IReadOnlyList<StudentInterviewScheduleItemViewModel> Interviews,
    IReadOnlyList<VolunteerScheduleItemViewModel> VolunteerShifts);

public sealed record StudentInterviewScheduleItemViewModel(
    int InterviewId,
    DateTime Date,
    DateTime Time,
    string InterviewType,
    string Status,
    string Location,
    string InterviewerName);

public sealed record VolunteerScheduleItemViewModel(
    DateTime Date,
    string StartTime,
    string EndTime,
    IReadOnlyList<int> VolunteerTimeslotIds);

public sealed record InterviewerDashboardViewModel(
    IReadOnlyList<ActiveInterviewViewModel> ActiveInterviews,
    IReadOnlyList<InterviewerScheduleGroupViewModel> ScheduleGroups,
    IReadOnlyList<CompletedInterviewViewModel> CompletedInterviews);

public sealed record ActiveInterviewViewModel(
    int InterviewId,
    string StudentId,
    string StudentName,
    string ClassName,
    string InterviewType,
    string Status,
    DateTime? StartedAt);

public sealed record InterviewerScheduleGroupViewModel(
    int SignupId,
    string Location,
    string InterviewType,
    IReadOnlyList<ScheduleRangeViewModel> Ranges);

public sealed record ScheduleRangeViewModel(
    DateTime Date,
    string StartTime,
    string EndTime);

public sealed record CompletedInterviewViewModel(
    int InterviewId,
    string StudentId,
    string StudentName,
    DateTime Date,
    DateTime Time,
    string InterviewType);
