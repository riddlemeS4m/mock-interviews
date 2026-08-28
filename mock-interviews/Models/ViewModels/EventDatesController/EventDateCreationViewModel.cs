using MockInterviews.Models.Entities;

namespace MockInterviews.Models.ViewModels.EventDatesController
{
    public class EventDateCreationViewModel
    {
        public Event EventDate { get; set; } = null!; // Assigned when the controller composes the creation form.
        public int MaxSignUps { get; set; }
    }
}
