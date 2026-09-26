using MockInterviews.Models.ViewModels.Shared;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace MockInterviews.Models.ViewModels.VolunteerEventsController
{
    public class VolunteerEventSignupViewModel
    {
        public IReadOnlyList<EventDaySelectionViewModel> EventDays { get; set; } = [];
        public int[] SelectedTimeslotIds { get; set; } = [];
        public bool SignedUp { get; set; }
    }

    public class VolunteerEventEditViewModel
    {
        public int Id { get; set; }
        [Required]
        public string StudentId { get; set; } = string.Empty;
        [Range(1, int.MaxValue)]
        public int TimeslotId { get; set; }
        public IReadOnlyList<SelectListItem> TimeslotOptions { get; set; } = [];
    }
}
