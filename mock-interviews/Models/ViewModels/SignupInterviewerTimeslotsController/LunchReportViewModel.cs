namespace MockInterviews.Models.ViewModels.SignupInterviewerTimeslotsController
{
    public class LunchReportViewModel
    {
        public List<LunchReport> LunchReports { get; set; } = [];
        public List<LunchTotalViewModel> LunchTotals { get; set; } = [];
    }

    public sealed record LunchTotalViewModel(string EventName, DateTime Date, int Count);
}
