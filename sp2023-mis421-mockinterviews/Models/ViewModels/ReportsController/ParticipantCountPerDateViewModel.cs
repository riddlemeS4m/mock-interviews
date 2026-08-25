using sp2023_mis421_mockinterviews.Models.Entities;
using System.ComponentModel.DataAnnotations;

namespace sp2023_mis421_mockinterviews.Models.ViewModels.ReportsController
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