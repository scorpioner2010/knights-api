using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KnightsApi.Models
{
    [Table("UserWarriors")]
    public class UserWarrior
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public int WarriorId { get; set; }

        [Required]
        public bool IsActive { get; set; } = false;

        [Required]
        [Range(0, int.MaxValue)]
        public int Xp { get; set; } = 0;

        [ForeignKey(nameof(UserId))]
        public virtual Player? User { get; set; }

        [ForeignKey(nameof(WarriorId))]
        public virtual Warrior? Warrior { get; set; }
    }
}