namespace MockInterviews.Models.ViewModels.Shared;

public sealed record EventDaySelectionViewModel(
    int EventId,
    string Name,
    DateTime Date,
    IReadOnlyList<TimeslotSelectionViewModel> Options);

public sealed record TimeslotSelectionViewModel(
    int Id,
    DateTime Start,
    DateTime End,
    bool Selected,
    bool Disabled = false,
    string? DisabledReason = null);

public sealed record TimeslotSelectorViewModel(
    IReadOnlyList<EventDaySelectionViewModel> EventDays,
    string InputName,
    bool AllowsMultipleSelection,
    bool ShowSelectAll,
    string IdPrefix);
