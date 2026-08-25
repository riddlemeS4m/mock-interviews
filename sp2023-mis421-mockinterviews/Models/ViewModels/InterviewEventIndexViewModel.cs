using Microsoft.AspNetCore.Mvc.Rendering;
using sp2023_mis421_mockinterviews.Models.Entities;

namespace sp2023_mis421_mockinterviews.Models.ViewModels
{
    public class InterviewEventIndexViewModel
    {
        public List<InterviewEventViewModel> Interviews { get; set; }
        public List<AvailableInterviewer> AvailableInterviewers { get; set; }
        public List<SelectListItem> TechnicalInterviewers { get; set; }
        public List<SelectListItem> BehavioralInterviewers { get; set; }
    }
}
