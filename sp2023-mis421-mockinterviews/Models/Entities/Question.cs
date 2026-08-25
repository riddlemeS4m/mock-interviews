using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace sp2023_mis421_mockinterviews.Models.Entities
{
    [Table("Questions")]
    public class Question
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Q { get; set; }

        public string? A { get; set; }

        public override string ToString()
        {
            return $"[Q] Id: {Id}, Q: {Q}, A: {A}";
        }
    }
}
