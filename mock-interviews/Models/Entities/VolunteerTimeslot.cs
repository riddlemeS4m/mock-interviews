using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MockInterviews.Models.Entities
{
    [Table("VolunteerTimeslots")]
    public class VolunteerTimeslot
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string StudentId { get; set; } = null!; // Required field is populated by validation or EF Core materialization.

        [Required]
        [ForeignKey("Timeslots")]
        public int TimeslotId { get; set; }

        [ValidateNever]
        public Timeslot Timeslot { get; set; } = null!; // Populated by EF Core when the required relationship is materialized.
        public override string ToString()
        {
            return $"{Timeslot.Time:h\\:mm tt} on {Timeslot.Event.Date:M/dd/yyyy} <br>";
        }
    }
}
