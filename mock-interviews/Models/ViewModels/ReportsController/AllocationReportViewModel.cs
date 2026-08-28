using System.ComponentModel.DataAnnotations;

namespace MockInterviews.Models.ViewModels.ReportsController
{
    public class AllocationReportViewModel
    {
        [Display(Name = "Top 10 Underserved Timeslots")]
        public List<ParticipantCountViewModel> Top10Underserved { get; set; } = [];
        [Display(Name = "Top 10 Available Timeslots")]
        public List<ParticipantCountViewModel> Top10Available { get; set; } = [];
    }
}
