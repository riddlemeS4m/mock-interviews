using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion.Internal;
using MockInterviews.Models.Entities;
using System.ComponentModel;

namespace MockInterviews.Models.ViewModels
{
    public class SignupInterviewerTimeslotsViewModel
    {
        public List<Timeslot> Timeslots { get; set; } = [];
        public List<Event> EventDates { get; set; } = [];
        public InterviewerSignup SignupInterviewer { get; set; } = null!; // Assigned when the controller composes a signup page.
        public Dictionary<int, bool> EventDateDictionary { get; set; } = [];
        public List<SelectListItem> Interviewers { get; set; } = [];
        [DisplayName("Interviewer Name")]
        public string InterviewerId { get; set; } = string.Empty;
        public int[] SelectedEventIds { get; set; } = [];
        public bool SignedUp { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Company { get; set; } = string.Empty;
    }
}
