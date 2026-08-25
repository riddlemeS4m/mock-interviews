using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace sp2023_mis421_mockinterviews.Models.Entities
{
    [Table("VolunteerTimeslots")]
    public class VolunteerTimeslot
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string StudentId { get; set; }

        [Required]
        [ForeignKey("Timeslots")]
        public int TimeslotId { get; set; }

        [ValidateNever]
        public Timeslot Timeslot { get; set; }
        public override string ToString()
        {
            return $"{Timeslot.Time:h\\:mm tt} on {Timeslot.Event.Date:M/dd/yyyy} <br>";
        }
    }
}
