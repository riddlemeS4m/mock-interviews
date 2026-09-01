using System.ComponentModel.DataAnnotations;
using MockInterviews.Models.Entities;

namespace MockInterviews.Models.ViewModels.EventDatesController
{
    public class EventDateCreationViewModel
    {
        public Event EventDate { get; set; } = new() { IsActive = true }; // Assigned when the controller composes the creation form.
        [Range(0, int.MaxValue, ErrorMessage = "Maximum signups must be zero or greater.")]
        public int MaxSignUps { get; set; }
        public string? For221True { get; set; }
        public string? For221False { get; set; }
    }
}
