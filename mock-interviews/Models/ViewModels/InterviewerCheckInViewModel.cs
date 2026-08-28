using Microsoft.AspNetCore.Mvc.Rendering;

namespace MockInterviews.Models.ViewModels
{
    public class InterviewerCheckInViewModel
    {
        public bool CheckedIn { get; set; }
        public string Room { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string InterviewerId { get; set; } = string.Empty;
        public List<SelectListItem> Interviewers { get; set; } = [];
    }
}
