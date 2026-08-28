using MockInterviews.Models.Entities;
using MockInterviews.Models.ViewModels.ReportsController;

namespace MockInterviews.Models.ViewModels.TimeslotsController
{
    public class TimeslotViewModel
    {
        public List<ParticipantCountViewModel> Timeslots { get; set; } = [];
        public List<Event> EventDates { get; set; } = [];
    }
}
