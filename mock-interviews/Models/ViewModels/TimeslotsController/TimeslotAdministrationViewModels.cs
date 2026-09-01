using System.ComponentModel.DataAnnotations;
using MockInterviews.Models.Entities;

namespace MockInterviews.Models.ViewModels.TimeslotsController;

public sealed record TimeslotIndexViewModel(IReadOnlyList<EventTimeslotGroupViewModel> EventGroups);

public sealed record EventTimeslotGroupViewModel(Event Event, IReadOnlyList<Timeslot> Timeslots);

public sealed class TimeslotEditViewModel
{
    public int Id { get; set; }
    public string EventName { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public DateTime Time { get; set; }
    [Range(0, int.MaxValue, ErrorMessage = "Maximum signups must be zero or greater.")]
    [Display(Name = "Maximum signups")]
    public int MaxSignUps { get; set; }

    public static TimeslotEditViewModel FromTimeslot(Timeslot timeslot) => new()
    {
        Id = timeslot.Id,
        EventName = timeslot.Event.Name,
        EventDate = timeslot.Event.Date,
        Time = timeslot.Time,
        MaxSignUps = timeslot.MaxSignUps
    };
}

public sealed class UpdateMaximumSignupsViewModel
{
    [Range(0, int.MaxValue, ErrorMessage = "Maximum signups must be zero or greater.")]
    [Display(Name = "New maximum signups")]
    public int MaxSignUps { get; set; }
}
