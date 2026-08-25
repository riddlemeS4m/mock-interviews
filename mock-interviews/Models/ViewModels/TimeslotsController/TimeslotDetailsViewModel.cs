using MockInterviews.Models.Entities;

namespace MockInterviews.Models.ViewModels.TimeslotsController
{
    public class TimeslotDetailsViewModel
    {
        public Timeslot Timeslot { get; set; }
        public List<string>? VolunteerNames { get; set; }
        public List<string>? StudentNames { get; set; }
        public List<string>? InterviewerNames { get; set; }
    }
}
