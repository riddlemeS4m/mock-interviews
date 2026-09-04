namespace MockInterviews.Models.ViewModels.InterviewEventsController;

/// <summary>
/// Server-owned pre-assignment state. Each group is one exact event timeslot,
/// so submitting a group can be validated and applied as a single unit.
/// </summary>
public sealed record PreAssignmentPageViewModel(
    IReadOnlyList<PreAssignmentTimeslotViewModel> Timeslots);

public sealed record PreAssignmentTimeslotViewModel(
    int TimeslotId,
    string EventName,
    DateTime EventDate,
    DateTime ScheduledTime,
    IReadOnlyList<PreAssignmentInterviewViewModel> Interviews);

public sealed record PreAssignmentInterviewViewModel(
    int InterviewId,
    string StudentName,
    string StudentClass,
    string InterviewType,
    string? AssignedInterviewerId,
    int? AssignedInterviewerTimeslotId,
    IReadOnlyList<PreAssignmentCandidateViewModel> Candidates);

public sealed record PreAssignmentCandidateViewModel(
    string InterviewerId,
    string Name,
    string Room);

public sealed class PreAssignmentTimeslotRequest
{
    public int TimeslotId { get; set; }
    public List<PreAssignmentInterviewRequest> Assignments { get; set; } = [];
}

public sealed class PreAssignmentInterviewRequest
{
    public int InterviewId { get; set; }
    public int? ExpectedInterviewerTimeslotId { get; set; }
    public string? InterviewerId { get; set; }
}
