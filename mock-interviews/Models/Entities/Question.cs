using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace MockInterviews.Models.Entities
{
    [Table("Questions")]
    public class Question
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Q { get; set; } = null!; // Required field is populated by validation or EF Core materialization.

        public string? A { get; set; }

        public override string ToString()
        {
            return $"[Q] Id: {Id}, Q: {Q}, A: {A}";
        }
    }
}
