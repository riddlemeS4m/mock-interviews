using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel;

namespace sp2023_mis421_mockinterviews.Models.Entities
{
    [Table("Locations")]
    public class Location
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Room { get; set; }

        [Required]
        [DefaultValue(false)]
        public bool IsVirtual { get; set; }

        [Required]
        [DefaultValue(true)]
        public bool InPerson { get; set; }

        public override string ToString()
        {
            return $"[Locations] Id: {Id}, Room: {Room}, IsVirtual: {IsVirtual}, InPerson: {InPerson}";
        }
    }
}
