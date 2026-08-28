using Microsoft.AspNetCore.Mvc.Rendering;
using MockInterviews.Models.Entities;

namespace MockInterviews.Models.ViewModels
{
    public class InterviewEventIndexViewModel
    {
        public List<InterviewEventViewModel> Interviews { get; set; } = [];
        public List<AvailableInterviewer> AvailableInterviewers { get; set; } = [];
        public List<SelectListItem> TechnicalInterviewers { get; set; } = [];
        public List<SelectListItem> BehavioralInterviewers { get; set; } = [];
    }
}
