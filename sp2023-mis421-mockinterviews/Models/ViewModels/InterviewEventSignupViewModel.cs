using sp2023_mis421_mockinterviews.Models.Entities;
using sp2023_mis421_mockinterviews.Models.Identity;

namespace sp2023_mis421_mockinterviews.Models.ViewModels
{
    public class InterviewEventSignupViewModel
    {
        public List<Timeslot> Timeslots { get; set; }
        public int SelectedEventIds { get; set; }
        public ApplicationUser ApplicationUser { get; set; }
        public bool SignedUp { get; set; }
        public bool For221 { get; set; }
    }
}
