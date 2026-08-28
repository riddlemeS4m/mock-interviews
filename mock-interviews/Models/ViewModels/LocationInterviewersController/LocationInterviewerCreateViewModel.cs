using Microsoft.AspNetCore.Mvc.Rendering;
using MockInterviews.Models.Entities;

namespace MockInterviews.Models.ViewModels.LocationInterviewersController
{
    public class LocationInterviewerCreateViewModel
    {
        public InterviewerLocation LocationInterviewer { get; set; } = null!; // Assigned when the controller composes the form.
        public string InterviewerName { get; set; } = string.Empty;
        public List<SelectListItem> Locations { get; set; } = [];
        public List<SelectListItem> InterviewerNames { get; set; } = [];
        public List<SelectListItem> Dates { get; set; } = [];
    }
}
