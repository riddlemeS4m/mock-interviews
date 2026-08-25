using sp2023_mis421_mockinterviews.Models.Entities;
using sp2023_mis421_mockinterviews.Models.ViewModels.ReportsController;

namespace sp2023_mis421_mockinterviews.Models.ViewModels.TimeslotsController
{
    public class TimeslotViewModel
    {
        public List<ParticipantCountViewModel> Timeslots { get; set; }
        public List<Event> EventDates { get; set; }
    }
}
