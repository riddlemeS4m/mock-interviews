using MockInterviews.Models.Entities;
using System.ComponentModel.DataAnnotations;

namespace MockInterviews.Models.ViewModels.ReportsController
{
	public class ParticipantCountViewModel
	{
        public Timeslot Timeslot { get; set; } = null!; // Assigned while the report row is projected.
		[Display(Name ="Number of Students")]
		public int StudentCount { get; set; }
		[Display(Name = "Number of Interviewers")]
		public int InterviewerCount { get; set; }
		[Display(Name = "Number of Volunteers")]
		public int VolunteerCount { get; set; }
		[Display(Name = "Interviewers Needed")]
		public int? Difference { get; set; }

	}
}
