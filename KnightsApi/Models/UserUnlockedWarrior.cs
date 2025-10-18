using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KnightsApi.Models
{
    public class UserUnlockedWarrior
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public int WarriorId { get; set; }

        [ForeignKey(nameof(UserId))]
        public Player? User { get; set; }

        [ForeignKey(nameof(WarriorId))]
        public Warrior? Warrior { get; set; }
    }
}