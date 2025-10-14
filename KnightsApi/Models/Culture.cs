using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KnightsApi.Models
{
    public class Culture
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        // Короткий унікальний код, напр.: "north_alliance", "sand_collective"
        [Required]
        public string Code { get; set; } = string.Empty;

        // Людинозрозуміла назва, напр.: "Північний Альянс"
        [Required]
        public string Name { get; set; } = string.Empty;

        // (опціонально) опис/лором
        public string? Description { get; set; }
    }
}