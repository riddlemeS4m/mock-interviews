namespace MockInterviews.Models.ViewModels.Shared;

public sealed record UiPageHeaderViewModel(string Title, string? Description = null);

public enum UiFeedbackKind
{
    Success,
    Warning,
    Error
}

public sealed record UiFeedbackViewModel(UiFeedbackKind Kind, string Title, string Message);

public sealed record UiEmptyStateViewModel(string Icon, string Title, string Description);
