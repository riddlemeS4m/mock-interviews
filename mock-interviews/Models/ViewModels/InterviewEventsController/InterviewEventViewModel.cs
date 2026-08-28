using System.ComponentModel.DataAnnotations;
using MockInterviews.Models.Entities;

namespace MockInterviews.Models.ViewModels.InterviewEventsController
{
    public class InterviewEventViewModel
    {
        public Interview InterviewEvent { get; set; } = null!; // Assigned while a schedule row is projected.

        [Display(Name = "Student Name")]
        public string StudentName { get; set; } = string.Empty;

        public string Class { get; set; } = string.Empty;

        [Display(Name = "Interviewer Name")]
        public string InterviewerName { get; set; } = string.Empty;
    }
}
