using System.ComponentModel.DataAnnotations;

namespace MockInterviews.Models.ViewModels.InterviewEventsController;

public sealed record FeedbackListViewModel(IReadOnlyList<FeedbackListItemViewModel> Interviews);

public sealed record FeedbackListItemViewModel(
    int InterviewId,
    DateTime Date,
    DateTime Time,
    string InterviewerName,
    string InterviewType,
    string? InterviewerRating,
    string? InterviewerFeedback,
    string? ProcessFeedback);

public sealed class FeedbackFormViewModel
{
    [Required]
    public int Id { get; set; }

    public DateTime Date { get; init; }

    public DateTime Time { get; init; }

    public string InterviewerName { get; init; } = string.Empty;

    public string InterviewType { get; init; } = string.Empty;

    [Required(ErrorMessage = "Choose a rating from 1 to 5.")]
    [RegularExpression("^[1-5]$", ErrorMessage = "Choose a rating from 1 to 5.")]
    [Display(Name = "How would you rate your interviewer?")]
    public string? InterviewerRating { get; set; }

    [Display(Name = "What feedback do you have for your interviewer?")]
    public string? InterviewerFeedback { get; set; }

    [Display(Name = "What feedback do you have about the interview process?")]
    public string? ProcessFeedback { get; set; }
}
