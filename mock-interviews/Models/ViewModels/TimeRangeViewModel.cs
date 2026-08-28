using System.ComponentModel.DataAnnotations;

namespace MockInterviews.Models.ViewModels
{
    public class TimeRangeViewModel
    {
        [Display(Name = "Start Time")]
        public string StartTime { get; set; } = string.Empty;
        [Display(Name = "End Time")]
        public string EndTime { get; set; } = string.Empty;
        [DataType(DataType.Date)]
        public DateTime Date { get; set; }
        public string? Location { get; set; }
        public string? Name { get; set; }
        [Display(Name = "Interview Type")]
        public string? InterviewType { get; set; }
        public List<int> TimeslotIds { get; set; } = [];
        [Display(Name = "Lunch")]
        public bool? WantsLunch { get; set; }
        public int? SignupInterviewerId { get; set; }

    }
}
