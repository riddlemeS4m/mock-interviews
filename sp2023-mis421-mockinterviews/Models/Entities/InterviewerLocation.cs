using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace sp2023_mis421_mockinterviews.Models.Entities
{
    [Table("InterviewerLocations")]
    public class InterviewerLocation
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Display(Name = "Interviewer")]
        public string InterviewerId { get; set; }

        [ForeignKey("Locations")]
        [Display(Name = "Locations")]
        public int? LocationId { get; set; }

        [ValidateNever]
        public Location? Location { get; set; }

        [ForeignKey("Events")]
        [Display(Name = "Event")]
        public int? EventId { get; set; }

        [ValidateNever]
        public Event? Event { get; set; }

        [ValidateNever]
        public string Preference { get; set; }

        public override string ToString()
        {
            return $"[Interviewer Locations] Id: {Id}, Interviewer Id: {InterviewerId}, Locations Id: {LocationId}, Event Date Id: {EventId}";
        }
    }
}
