namespace MockInterviews.Models.ViewModels
{
    public class AttendanceProgramBySectionViewModel
    {
        public string StudentName { get; set; } = string.Empty;
        public string Class { get; set; } = string.Empty;
        public bool SignedUp { get; set; }
        public bool ShowedUp { get; set; }
        public bool Completed { get; set; }
    }
}
