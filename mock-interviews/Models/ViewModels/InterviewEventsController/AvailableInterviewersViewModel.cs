using Microsoft.AspNetCore.Mvc.Rendering;

namespace MockInterviews.Models.ViewModels.InterviewEventsController
{
    public class AvailableInterviewersViewModel
    {
        public List<SelectListItem> BehavioralInterviewers { get; set; } = [];
        public List<SelectListItem> TechnicalInterviewers { get; set; } = [];
    }
}
