using Microsoft.AspNetCore.Mvc.Rendering;

namespace MockInterviews.Models.ViewModels
{
    public class InterviewerCheckInViewModel
    {
        public bool CheckedIn { get; set; }
        public string Room { get; set; }
        public string Name { get; set; }
        public string InterviewerId { get; set; }
        public List<SelectListItem> Interviewers { get; set; }
    }
}
