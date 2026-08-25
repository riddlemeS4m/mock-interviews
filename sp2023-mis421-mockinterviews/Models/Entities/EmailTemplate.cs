using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace sp2023_mis421_mockinterviews.Models.Entities
{
    [Table("EmailTemplates")]
    public class EmailTemplate
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Display(Name = "Subject Line")]
        public string SubjectLine { get; set; }

        public string? Body { get; set; }

        public override string ToString()
        {
            return $"[Email Template] Id: {Id}, Subject Line: {SubjectLine}";
        }
    }
}
