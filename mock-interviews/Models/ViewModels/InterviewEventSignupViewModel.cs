using MockInterviews.Models.Entities;
using MockInterviews.Models.Identity;

namespace MockInterviews.Models.ViewModels
{
    public class InterviewEventSignupViewModel
    {
        public List<Timeslot> Timeslots { get; set; } = [];
        public int SelectedEventIds { get; set; }
        public ApplicationUser ApplicationUser { get; set; } = null!; // Assigned when the controller composes the signup page.
        public bool SignedUp { get; set; }
        public bool For221 { get; set; }
    }
}
