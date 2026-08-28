using System.ComponentModel.DataAnnotations;
using MockInterviews.Models.Entities;

namespace MockInterviews.Models.ViewModels.LocationInterviewersController
{
    public class LocationInterviewerWithName
    {
        public InterviewerLocation LocationInterviewer { get; set; } = null!; // Assigned while the list row is projected.
        [Display(Name = "Interviewer")]
        public string InterviewerName { get; set; } = string.Empty;
        public string InterviewerPreference { get; set; } = string.Empty;
    }
}
