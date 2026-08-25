using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion.Internal;
using sp2023_mis421_mockinterviews.Models.Entities;
using System.ComponentModel;

namespace sp2023_mis421_mockinterviews.Models.ViewModels
{
    public class SignupInterviewerTimeslotsViewModel
    {
        public List<Timeslot> Timeslots { get; set; }
        public List<Event> EventDates { get; set; }
        public InterviewerSignup SignupInterviewer { get; set; }
        public Dictionary<int, bool> EventDateDictionary { get; set; }
        public List<SelectListItem> Interviewers { get; set; }
        [DisplayName("Interviewer Name")]
        public string InterviewerId { get; set; }
        public int[] SelectedEventIds { get; set; }
        public bool SignedUp { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string Company { get; set; }
    }
}
