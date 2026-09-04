using System.ComponentModel.DataAnnotations;
using MockInterviews.Data.Constants;
using MockInterviews.Models.Entities;

namespace MockInterviews.Models.ViewModels.EventDatesController;

public sealed record EventDateIndexViewModel(
    IReadOnlyList<Event> Events,
    EventDateCreationViewModel Editor,
    string? ActiveDialog = null);

public sealed class EventDateEditViewModel
{
    public int Id { get; set; }

    [Required, DataType(DataType.Date)]
    public DateTime Date { get; set; }

    [Required, Display(Name = "Event name")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Active")]
    public bool IsActive { get; set; }

    public bool For221True { get; set; }
    public bool For221False { get; set; }

    public static EventDateEditViewModel FromEvent(Event @event) => new()
    {
        Id = @event.Id,
        Date = @event.Date,
        Name = @event.Name,
        IsActive = @event.IsActive,
        For221True = @event.For221 is For221.y or For221.b,
        For221False = @event.For221 is For221.n or For221.b
    };
}
