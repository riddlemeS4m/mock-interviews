using sp2023_mis421_mockinterviews.Models.Entities;
using System.ComponentModel.DataAnnotations;

namespace sp2023_mis421_mockinterviews.Models.ViewModels.ReportsController
{
	public class ParticipantCountViewModel
	{
		public Timeslot Timeslot { get; set; }
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
