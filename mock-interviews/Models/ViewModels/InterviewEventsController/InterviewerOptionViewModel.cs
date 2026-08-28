namespace MockInterviews.Models.ViewModels.InterviewEventsController
{
    public class InterviewerOptionViewModel
    {
        public string Name { get; set; } = string.Empty;
        public string Id { get; set; } = string.Empty;
        public bool Technical { get; set; }
        public bool Behavioral { get; set; }
    }
}
