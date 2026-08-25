using MockInterviews.Models.Entities;
using System.ComponentModel.DataAnnotations;

namespace MockInterviews.Models.ViewModels
{
    public class LocationInterviewerWithName
    {
        public InterviewerLocation LocationInterviewer { get; set; }
        [Display(Name = "Interviewer")]
        public string InterviewerName { get; set; }
        public string InterviewerPreference { get; set; }
    }
}
