using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WarOfMachines.Models
{
    public class UnitResearchRequirement
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int PredecessorUnitId { get; set; }

        [ForeignKey(nameof(PredecessorUnitId))]
        public Unit? Predecessor { get; set; }

        [Required]
        public int SuccessorUnitId { get; set; }

        [ForeignKey(nameof(SuccessorUnitId))]
        public Unit? Successor { get; set; }

        [Range(0, int.MaxValue)]
        public int RequiredXpOnPredecessor { get; set; } = 0;
    }
}