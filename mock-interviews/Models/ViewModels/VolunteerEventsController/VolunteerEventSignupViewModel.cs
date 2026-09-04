using MockInterviews.Models.ViewModels.Shared;

namespace MockInterviews.Models.ViewModels.VolunteerEventsController
{
    public class VolunteerEventSignupViewModel
    {
        public IReadOnlyList<EventDaySelectionViewModel> EventDays { get; set; } = [];
        public int[] SelectedTimeslotIds { get; set; } = [];
        public bool SignedUp { get; set; }
    }
}
