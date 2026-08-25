using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace MockInterviews.Models.Entities
{
    [Table("Settings")]
    public class Setting
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Name { get; set; }

        [Required]
        public string Value { get; set; }

        public override string ToString()
        {
            return $"[Setting] Id: {Id}, Name: {Name}, Value: {Value}";
        }
    }
}
