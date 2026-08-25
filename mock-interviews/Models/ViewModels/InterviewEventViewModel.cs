using MockInterviews.Models.Entities;
using System.ComponentModel.DataAnnotations;

namespace MockInterviews.Models.ViewModels
{
    public class InterviewEventViewModel
    {
        public Interview InterviewEvent { get; set; }

        [Display(Name = "Student Name")]
        public string StudentName { get; set; }

        public string Class { get; set; }

        [Display(Name = "Interviewer Name")]
        public string InterviewerName { get; set; }
    }
}
