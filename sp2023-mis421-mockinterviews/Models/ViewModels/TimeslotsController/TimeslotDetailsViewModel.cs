using sp2023_mis421_mockinterviews.Models.Entities;

namespace sp2023_mis421_mockinterviews.Models.ViewModels.TimeslotsController
{
    public class TimeslotDetailsViewModel
    {
        public Timeslot Timeslot { get; set; }
        public List<string>? VolunteerNames { get; set; }
        public List<string>? StudentNames { get; set; }
        public List<string>? InterviewerNames { get; set; }
    }
}
