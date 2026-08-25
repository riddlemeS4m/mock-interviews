using Microsoft.AspNetCore.Mvc.Rendering;
using MockInterviews.Models.Entities;

namespace MockInterviews.Models.ViewModels.InterviewsController
{
    public class InterviewSignupByAdminViewModel
    {
        public List<SelectListItem> Students { get; set; }
        public List<Timeslot> Timeslots { get; set; }
        public List<Event> Events { get; set; }
        public int SelectedEventIds { get; set; }
        public string StudentId { get; set; }
    }
}


