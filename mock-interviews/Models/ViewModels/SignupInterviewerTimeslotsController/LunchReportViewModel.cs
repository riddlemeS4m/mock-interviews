namespace MockInterviews.Models.ViewModels.SignupInterviewerTimeslotsController
{
    public class LunchReportViewModel
    {
        public List<LunchReport> LunchReports { get; set; } = [];
        public string Day1Name { get; set; } = string.Empty;
        public string? Day2Name { get; set; }
        public int Day1TotalLunchCount { get; set; }
        public int? Day2TotalLunchCount { get; set; }
        public bool AnyLunches { get; set; }
    }
}
