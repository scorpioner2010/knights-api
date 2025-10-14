using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KnightsApi.Models
{
    public class WarriorResearchRequirement
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int PredecessorWarriorId { get; set; }

        [ForeignKey(nameof(PredecessorWarriorId))]
        public Warrior? Predecessor { get; set; }

        [Required]
        public int SuccessorWarriorId { get; set; }

        [ForeignKey(nameof(SuccessorWarriorId))]
        public Warrior? Successor { get; set; }

        [Range(0, int.MaxValue)]
        public int RequiredXpOnPredecessor { get; set; } = 0;
    }
}