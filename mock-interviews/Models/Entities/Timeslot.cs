using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.CompilerServices;

namespace MockInterviews.Models.Entities
{
    [Table("Timeslots")]
    public class Timeslot
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [DataType(DataType.Time)]
        [DisplayFormat(DataFormatString = "{0:hh:mm tt}")]
        public DateTime Time { get; set; }

        [Required]
        [ForeignKey("Events")]
        public int EventId { get; set; }

        [ValidateNever]
        public Event Event { get; set; } = null!; // Populated by EF Core when the required relationship is materialized.

        [Required]
        [DefaultValue(true)]
        [Display(Name = "Active?")]
        public bool IsActive { get; set; }

        [DefaultValue(false)]
        [Display(Name = "For Volunteers?")]
        public bool IsVolunteer { get; set; }

        [DefaultValue(false)]
        [Display(Name = "For Interviewers?")]
        public bool IsInterviewer { get; set; }

        [DefaultValue(false)]
        [Display(Name = "For Students?")]
        public bool IsStudent { get; set; }

        [Display(Name = "Max Sign Ups")]
        public int MaxSignUps { get; set; }

        public override string ToString()
        {
            return $"[Timeslots] Id: {Id}, Time: {Time}, Date: {EventId}";
        }
    }
}
