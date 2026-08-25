using Microsoft.EntityFrameworkCore.Storage.ValueConversion.Internal;
using sp2023_mis421_mockinterviews.Models.Entities;

namespace sp2023_mis421_mockinterviews.Models.ViewModels
{
    public class IndexViewModel
    {
        public List<InterviewEventViewModel> StudentScheduledInterviews { get; set; }
        public List<VolunteerEventViewModel> VolunteerEventViewModels { get; set; }
        public List<InterviewEventViewModel> InterviewerScheduledInterviews { get;  set; }
        public List<InterviewEventViewModel> CompletedInterviews { get; set; }
        public List<InterviewerTimeslot> SignupInterviewerTimeslots { get; set; }
        public List<TimeRangeViewModel> TimeRangeViewModels { get; set; }
        public List<TimeRangeViewModel> InterviewerRangeViewModels { get; set; }
        public string Name { get; set; }
        public string ZoomLink { get; set; }
        public string ZoomLinkVisible { get; set; }
        public string DisruptionBanner { get; set; }
        public int? SignupInterviewerId1 { get; set; }
        public int? SignupInterviewerId2 { get; set; }
    }
}
