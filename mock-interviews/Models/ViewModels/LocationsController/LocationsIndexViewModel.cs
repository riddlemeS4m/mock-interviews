using MockInterviews.Models.Entities;

namespace MockInterviews.Models.ViewModels.LocationsController;

public sealed record LocationsIndexViewModel(
    IReadOnlyList<Location> Locations,
    Location Editor,
    string? ActiveDialog = null);
