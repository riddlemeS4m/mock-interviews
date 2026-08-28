namespace MockInterviews.Models.ViewModels
{
    public class AttendanceReportViewModel
    {
        public List<ClassReport> ClassReports { get; set; } = [];
        public List<ClassReport> SummaryStats { get; set; } = [];
    }
}
