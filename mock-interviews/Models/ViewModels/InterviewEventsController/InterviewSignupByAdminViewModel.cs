using Microsoft.AspNetCore.Mvc.Rendering;
using MockInterviews.Models.Entities;
using MockInterviews.Models.ViewModels.Shared;

namespace MockInterviews.Models.ViewModels.InterviewEventsController
{
    public class InterviewSignupByAdminViewModel
    {
        public List<SelectListItem> Students { get; set; } = [];
        public IReadOnlyList<EventDaySelectionViewModel> EventDays { get; set; } = [];
        public int[] SelectedTimeslotIds { get; set; } = [];
        public string StudentId { get; set; } = string.Empty;
    }
}
