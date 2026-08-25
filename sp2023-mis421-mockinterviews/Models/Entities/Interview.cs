
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace sp2023_mis421_mockinterviews.Models.Entities
{
    [Table("Interviews")]
    public class Interview
    {
        //probably our most important entity

        [Key]
        public int Id { get; set; }

        [Required]
        public string StudentId { get; set; }

        [ForeignKey("Locations")]
        public int? LocationId { get; set; }

        [ValidateNever]
        public Location? Location { get; set; }

        [Required]
        [ForeignKey("Timeslots")]
        public int TimeslotId { get; set; }

        [ValidateNever]
        public Timeslot Timeslot { get; set; }

        [Display(Name = "Interview Type")]
        public string? Type { get; set; }

        [Required]
        public string Status { get; set; }

        [Display(Name = "Rating")]
        public string? InterviewerRating { get; set; }

        [Display(Name = "Interviewer Feedback")]
        public string? InterviewerFeedback { get; set; }

        [Display(Name = "Process Feedback")]
        public string? ProcessFeedback { get; set; }

        [ForeignKey("InterviewerTimeslots")]
        public int? InterviewerTimeslotId { get; set; }

        [ValidateNever]
        public InterviewerTimeslot? InterviewerTimeslot { get; set; }

        [Display(Name = "Check-In Time")]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-ddTHH:mm:ss'Z'}", ApplyFormatInEditMode = true)]

        public DateTime? CheckedInAt { get; set; }

        [Display(Name = "Interview Timer")]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-ddTHH:mm:ss'Z'}", ApplyFormatInEditMode = true)]
        public DateTime? StartedAt { get; set; }

        [Display(Name = "Interview End Time")]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-ddTHH:mm:ss'Z'}", ApplyFormatInEditMode = true)]
        public DateTime? EndedAt { get; set; }

        public override string ToString()
        {
            return $"{Timeslot.Time:h\\:mm tt} on {Timeslot.Event.Date:M/dd/yyyy} <br>";
        }
    }
}
