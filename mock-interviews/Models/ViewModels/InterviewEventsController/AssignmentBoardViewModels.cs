namespace MockInterviews.Models.ViewModels.InterviewEventsController;

/// <summary>
/// Immutable, server-composed state for the live assignment workspace. Scheduled
/// time always comes from the interview's own timeslot, never its availability
/// relationship (which can differ for a super override).
/// </summary>
public sealed record AssignmentBoardViewModel(
    IReadOnlyList<AssignmentBoardInterviewViewModel> CheckedIn,
    IReadOnlyList<AssignmentBoardInterviewViewModel> Ongoing,
    IReadOnlyList<AssignmentBoardInterviewViewModel> Upcoming,
    IReadOnlyList<AssignmentBoardAvailableInterviewerViewModel> AvailableInterviewers);

public sealed record AssignmentBoardInterviewViewModel(
    int InterviewId,
    string StudentName,
    string StudentClass,
    DateTime EventDate,
    DateTime ScheduledTime,
    string InterviewType,
    string Status,
    DateTime? CheckedInAt,
    DateTime? StartedAt,
    string InterviewerName,
    string Room,
    bool IsOverride,
    IReadOnlyList<AssignmentBoardCandidateViewModel> NormalCandidates,
    IReadOnlyList<AssignmentBoardOverrideCandidateViewModel> OverrideCandidates);

public sealed record AssignmentBoardCandidateViewModel(
    string InterviewerId,
    string Name,
    string Room);

public sealed record AssignmentBoardOverrideCandidateViewModel(
    string InterviewerId,
    string Name,
    IReadOnlyList<string> Bypasses);

public sealed record AssignmentBoardAvailableInterviewerViewModel(
    string InterviewerId,
    string Name,
    string InterviewTypes);
