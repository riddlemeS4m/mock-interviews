using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using MockInterviews.Data.Constants;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MockInterviews.Models.Entities
{
    [Table("Events")]
    public class Event
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public DateTime Date { get; set; }

        [Required]
        [Display(Name = "Event Name")]
        public string Name { get; set; }

        [Display(Name = "For MIS 221?")]
        [DefaultValue(For221.b)]
        [ValidateNever]
        public For221 For221 { get; set; }

        [Required]
        [Display(Name = "Deactivate?")]
        [DefaultValue(true)]
        public bool IsActive { get; set; }

        public override string ToString()
        {
            return $"[Event] Id: {Id}, Date: {Date}, Name: {Name}, IsActive: {IsActive}";
        }
    }
}
