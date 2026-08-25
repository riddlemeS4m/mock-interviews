using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace sp2023_mis421_mockinterviews.Models.Entities
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
        public InterviewerSignup InterviewerSignup { get; set; }

        [Required]
        [ForeignKey("Timeslots")]
        public int TimeslotId { get; set; }

        [ValidateNever]
        public Timeslot Timeslot { get; set; }

        public override string ToString()
        {
            return $"[Interviewer Timeslots] Id: {Id}, Interviewer Signup Id: {InterviewerSignupId}, Timeslots Id: {TimeslotId}";
        }
    }
}
