using MockInterviews.Data.Constants;
using MockInterviews.Models.ViewModels.Shared;

namespace MockInterviews.Models.ViewModels.InterviewEventsController
{
    public class InterviewEventSignupViewModel
    {
        public IReadOnlyList<EventDaySelectionViewModel> EventDays { get; set; } = [];
        public int[] SelectedTimeslotIds { get; set; } = [];
        public Classes StudentClass { get; set; }
        public bool SignedUp { get; set; }
        public bool For221 { get; set; }
    }
}
