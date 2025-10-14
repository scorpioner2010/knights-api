using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WarOfMachines.Models
{
    public class Match
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        // Назва/код мапи (наприклад: "desert_outpost")
        [Required]
        public string Map { get; set; } = string.Empty;

        // Час старту/завершення бою (UTC)
        [Required]
        public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;

        public DateTimeOffset? EndedAt { get; set; }
    }
}