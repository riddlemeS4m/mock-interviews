using System.ComponentModel;
using Microsoft.AspNetCore.Mvc.Rendering;
using MockInterviews.Models.Entities;
using MockInterviews.Models.ViewModels.Shared;

namespace MockInterviews.Models.ViewModels.SignupInterviewerTimeslotsController
{
    public class SignupInterviewerTimeslotsViewModel
    {
        public List<Timeslot> Timeslots { get; set; } = [];
        public IReadOnlyList<EventDaySelectionViewModel> EventDays { get; set; } = [];
        public List<Event> EventDates { get; set; } = [];
        public InterviewerSignup SignupInterviewer { get; set; } = null!; // Assigned when the controller composes a signup page.
        public Dictionary<int, bool> EventDateDictionary { get; set; } = [];
        public List<SelectListItem> Interviewers { get; set; } = [];
        [DisplayName("Interviewer Name")]
        public string InterviewerId { get; set; } = string.Empty;
        public int[] SelectedTimeslotIds { get; set; } = [];
        public bool SignedUp { get; set; }
        public bool Lunch { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Company { get; set; } = string.Empty;
    }
}
