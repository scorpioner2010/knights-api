using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WarOfMachines.Models
{
    public class Map
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        // Унікальний код мапи (використовуй у Match.Map як code)
        [Required]
        public string Code { get; set; } = string.Empty;

        // Назва для користувача
        [Required]
        public string Name { get; set; } = string.Empty;

        // (опційно) опис
        public string? Description { get; set; }
    }
}