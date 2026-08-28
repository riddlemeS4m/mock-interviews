using MockInterviews.Models.Entities;

namespace MockInterviews.Models.ViewModels
{
    public class VolunteerEventViewModel
    {
        public VolunteerTimeslot VolunteerEvent { get; set; } = null!; // Assigned when the controller composes an event row.
        public string StudentName { get; set; } = string.Empty;
    }
}
