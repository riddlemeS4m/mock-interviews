namespace MockInterviews.Models.ViewModels.Shared
{
    public class AvailableInterviewer
    {
        public string InterviewerId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Room { get; set; } = string.Empty;
        public string InterviewType { get; set; } = string.Empty;

        public override string ToString()
        {
            return $"{Name} is available to do {InterviewType} interviews in room {Room}.";
        }
    }
}
