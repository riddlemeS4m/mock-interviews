using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MockInterviews.Models.Entities
{
    [Table("InterviewerTimeslots")]
    public class InterviewerTimeslot
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("InterviewerSignups")]
        public int InterviewerSignupId { get; set; }

        [ValidateNever]
        public InterviewerSignup InterviewerSignup { get; set; } = null!; // Populated by EF Core when the required relationship is materialized.

        [Required]
        [ForeignKey("Timeslots")]
        public int TimeslotId { get; set; }

        [ValidateNever]
        public Timeslot Timeslot { get; set; } = null!; // Populated by EF Core when the required relationship is materialized.

        public override string ToString()
        {
            return $"[Interviewer Timeslots] Id: {Id}, Interviewer Signup Id: {InterviewerSignupId}, Timeslots Id: {TimeslotId}";
        }
    }
}
