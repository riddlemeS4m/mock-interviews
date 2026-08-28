using MockInterviews.Models.Entities;

namespace MockInterviews.Models.ViewModels
{
    public class VolunteerEventSignupViewModel
    {
        //public List<VolunteerEvent> VolunteerEvent { get; set; }
        public List<Timeslot> Timeslots { get; set; } = [];
        public List<Event> EventDates { get; set; } = [];
        public int[] SelectedEventIds1 { get; set; } = [];
        public int[] SelectedEventIds2 { get; set; } = [];
        public bool SignedUp { get; set; }
    }
}
