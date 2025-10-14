using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WarOfMachines.Models
{
    public class MatchParticipant
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        // Посилання на матч
        [Required]
        public int MatchId { get; set; }

        // Посилання на гравця
        [Required]
        public int UserId { get; set; }

        // Який танк/робот використовувався
        [Required]
        public int UnitId { get; set; }

        // Номер команди (наприклад 1 чи 2)
        [Required]
        public int Team { get; set; }

        // Результат для гравця (win/lose/draw)
        [Required]
        public string Result { get; set; } = string.Empty;

        public int Kills { get; set; } = 0;
        public int Damage { get; set; } = 0;
        public int XpEarned { get; set; } = 0;
        public int MmrDelta { get; set; } = 0;

        // Навігаційні властивості
        [ForeignKey(nameof(MatchId))]
        public Match? Match { get; set; }

        [ForeignKey(nameof(UserId))]
        public Player? User { get; set; }

        [ForeignKey(nameof(UnitId))]
        public Unit? Unit { get; set; }
    }
}