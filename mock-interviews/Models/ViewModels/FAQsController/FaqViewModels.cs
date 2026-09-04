using MockInterviews.Models.Entities;

namespace MockInterviews.Models.ViewModels.FAQsController;

public sealed record FaqIndexViewModel(
    IReadOnlyList<Question> Questions,
    Question Editor,
    string? ActiveDialog = null);

public sealed record ResourcesViewModel(
    IReadOnlyList<Question> Questions,
    string? ManualUrl,
    string? ParkingPassUrl);
