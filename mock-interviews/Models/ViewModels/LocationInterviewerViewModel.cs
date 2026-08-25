
using MockInterviews.Models.Entities;

namespace MockInterviews.Models.ViewModels
{
    public class LocationInterviewerViewModel
    {
        public List<LocationInterviewerWithName> LocationInterviewerWithNames { get; set; }
        public List<Location> Locations { get; set; }
    }
}
