using Microsoft.AspNetCore.Mvc.Rendering;
using MockInterviews.Models.Entities;

namespace MockInterviews.Models.ViewModels
{
    public class InterviewEventManageViewModel
    {
        public Interview InterviewEvent { get; set; } = null!; // Assigned when the controller composes the management page.
        public List<SelectListItem> BehavioralInterviewers { get; set; } = [];
        public List<SelectListItem> TechnicalInterviewers { get; set; } = [];
        public List<SelectListItem> RequestedInterviewers { get; set; } = [];
        public string InterviewerId { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public string StudentClass { get; set; } = string.Empty;
    }
}
