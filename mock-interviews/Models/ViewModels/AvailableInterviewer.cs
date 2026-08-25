namespace MockInterviews.Models.ViewModels
{
    public class AvailableInterviewer
    {
        public string InterviewerId { get; set; }
        public string Name { get; set; }
        public string Room { get; set; }
        public string InterviewType { get; set; }

        public override string ToString()
        {
            return $"{Name} is available to do {InterviewType} interviews in room {Room}.";
        }
    }
}
