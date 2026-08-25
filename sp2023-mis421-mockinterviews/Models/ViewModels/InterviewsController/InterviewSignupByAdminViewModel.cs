using Microsoft.AspNetCore.Mvc.Rendering;
using sp2023_mis421_mockinterviews.Models.Entities;

namespace sp2023_mis421_mockinterviews.Models.ViewModels.InterviewsController
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


