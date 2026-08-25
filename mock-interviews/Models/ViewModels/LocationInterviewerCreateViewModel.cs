using Microsoft.AspNetCore.Mvc.Rendering;
using MockInterviews.Models.Entities;

namespace MockInterviews.Models.ViewModels
{
    public class LocationInterviewerCreateViewModel
    {
        public InterviewerLocation LocationInterviewer { get; set; }
        public string InterviewerName { get; set; }
        public List<SelectListItem> Locations { get; set; }
        public List<SelectListItem> InterviewerNames { get; set; }
        public List<SelectListItem> Dates { get; set; }
    }
}
