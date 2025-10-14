using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WarOfMachines.Models
{
    [Table("UserVehicles")]
    public class UserUnit
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public int UnitId { get; set; }

        [Required]
        public bool IsActive { get; set; } = false;

        [Required]
        [Range(0, int.MaxValue)]
        public int Xp { get; set; } = 0;

        [ForeignKey(nameof(UserId))]
        public virtual Player? User { get; set; }

        [ForeignKey(nameof(UnitId))]
        public virtual Unit? Unit { get; set; }
    }
}