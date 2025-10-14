using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KnightsApi.Models
{
    public class Warrior
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public string Code { get; set; } = string.Empty;

        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public int CultureId { get; set; }

        [ForeignKey(nameof(CultureId))]
        public Culture? Culture { get; set; }

        [Required]
        public string Branch { get; set; } = "tracked"; // "tracked" | "biped"

        [Required]
        public WarriorClass Class { get; set; } = WarriorClass.Light; // Scout|Guardian|Colossus

        [Range(1, 4)]
        public int Level { get; set; } = 1; // 1..4
        public int PurchaseCost { get; set; } = 0;
        public int HP { get; set; } = 0;
        public int Damage { get; set; } = 0;
        public float Accuracy { get; set; } = 0f;
        public float Speed { get; set; } = 0f;
        public float Acceleration { get; set; } = 0f;
        public float TraverseSpeed { get; set; } = 0f;
        public int Armor { get; set; } = 0;

        public ICollection<WarriorResearchRequirement> ResearchFrom { get; set; } = new List<WarriorResearchRequirement>();
        
        public bool IsVisible { get; set; } = true;
    }
}