using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using MockInterviews.Models.Entities;

namespace MockInterviews.Models.ViewModels.TimeslotsController;

public sealed record TimeslotIndexViewModel(IReadOnlyList<EventTimeslotGroupViewModel> EventGroups);

public sealed record EventTimeslotGroupViewModel(Event Event, IReadOnlyList<Timeslot> Timeslots);

public sealed class TimeslotCreateViewModel
{
    [Required]
    [DataType(DataType.Time)]
    [DisplayFormat(DataFormatString = "{0:hh:mm tt}")]
    public DateTime Time { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Choose a current active event.")]
    public int EventId { get; set; }

    [Display(Name = "Active?")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "For Volunteers?")]
    public bool IsVolunteer { get; set; } = true;

    [Display(Name = "For Interviewers?")]
    public bool IsInterviewer { get; set; }

    [Display(Name = "For Students?")]
    public bool IsStudent { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Maximum signups must be zero or greater.")]
    [Display(Name = "Max Sign Ups")]
    public int MaxSignUps { get; set; }

    public IReadOnlyList<SelectListItem> EventOptions { get; set; } = [];

    public Timeslot ToTimeslot() => new()
    {
        Time = DateTime.SpecifyKind(Time, DateTimeKind.Utc),
        EventId = EventId,
        IsActive = IsActive,
        IsVolunteer = IsVolunteer,
        IsInterviewer = IsInterviewer,
        IsStudent = IsStudent,
        MaxSignUps = MaxSignUps
    };
}

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
