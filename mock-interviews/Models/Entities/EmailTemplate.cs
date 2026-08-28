using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace MockInterviews.Models.Entities
{
    [Table("EmailTemplates")]
    public class EmailTemplate
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Display(Name = "Subject Line")]
        public string SubjectLine { get; set; } = null!; // Required field is populated by validation or EF Core materialization.

        public string? Body { get; set; }

        public override string ToString()
        {
            return $"[Email Template] Id: {Id}, Subject Line: {SubjectLine}";
        }
    }
}
