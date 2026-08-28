using System.ComponentModel.DataAnnotations;

namespace MockInterviews.Models.ViewModels
{
    public class LunchReport
    {
        public string Name { get; set; } = string.Empty;
        [Display(Name = "Wants Lunch?")]
        public bool LunchDesire { get; set; }
        [Display(Name = "For Date")]
        [DataType(DataType.Date)]
        public DateTime ForDate { get; set; }
    }
}
