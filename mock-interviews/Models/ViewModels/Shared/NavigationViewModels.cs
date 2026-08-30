namespace MockInterviews.Models.ViewModels.Shared;

public sealed record NavigationItemViewModel(
    string Label,
    string Controller,
    string Action,
    string Area = "",
    bool MatchAllActions = false)
{
    public bool IsActive { get; init; }
}

public sealed record NavigationGroupViewModel(
    string Id,
    string Label,
    string Icon,
    IReadOnlyList<NavigationItemViewModel> Items)
{
    public bool IsActive => Items.Any(item => item.IsActive);
}
