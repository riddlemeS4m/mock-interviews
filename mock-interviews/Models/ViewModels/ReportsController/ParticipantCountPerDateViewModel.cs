using MockInterviews.Models.Entities;
using System.ComponentModel.DataAnnotations;

namespace MockInterviews.Models.ViewModels.ReportsController
{
    public class ParticipantCountPerDateViewModel
    {
        [Display(Name = "Event Name")]
        public Event? EventDate { get; set; }
        [Display(Name = "No. of Students")]
        public int? StudentCount { get; set; }
        [Display(Name = "No. of Interviewers")]
        public int? InterviewerCount { get; set; }
        [Display(Name = "No. of Volunteers")]
        public int? VolunteerCount { get; set; }
    }
}