using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KnightsApi.Data;
using KnightsApi.Models;

namespace KnightsApi.Controllers
{
    [ApiController]
    [Route("warriors")]
    public class WarriorsController : ControllerBase
    {
        private readonly AppDbContext _db;

        public WarriorsController(AppDbContext db)
        {
            _db = db;
        }

        // GET /warriors?faction=iron_alliance&branch=tracked
        [HttpGet]
        public IActionResult GetAll([FromQuery] string? faction = null, [FromQuery] string? branch = null)
        {
            IQueryable<Warrior> q = _db.Warriors.Include(v => v.Culture).AsQueryable();

            if (!string.IsNullOrWhiteSpace(faction))
            {
                string fc = faction.Trim();
                q = q.Where(v => v.Culture != null && v.Culture.Code == fc);
            }

            if (!string.IsNullOrWhiteSpace(branch))
            {
                string br = branch.Trim().ToLowerInvariant();
                q = q.Where(v => v.Branch.ToLower() == br);
            }

            List<WarriorDto> items = q
                .Select(v => new WarriorDto
                {
                    Id = v.Id,
                    Code = v.Code,
                    Name = v.Name,
                    Branch = v.Branch,
                    CultureCode = v.Culture != null ? v.Culture.Code : string.Empty,
                    CultureName = v.Culture != null ? v.Culture.Name : string.Empty,

                    Class = v.Class.ToString(),
                    Level = v.Level,
                    PurchaseCost = v.PurchaseCost,
                    IsVisible = v.IsVisible, // 🔹 додано

                    HP = v.HP,
                    Damage = v.Damage,
                    Accuracy = v.Accuracy,
                    Speed = v.Speed,
                    Acceleration = v.Acceleration,
                    TraverseSpeed = v.TraverseSpeed,

                    Armor = $"{v.Armor}"
                })
                .ToList();

            return Ok(items);
        }

        // GET /warriors/{id:int}
        [HttpGet("{id:int}")]
        public IActionResult GetById(int id)
        {
            var v = _db.Warriors
                .Include(x => x.Culture)
                .FirstOrDefault(x => x.Id == id);
            if (v == null) return NotFound();

            return Ok(new WarriorDto
            {
                Id = v.Id,
                Code = v.Code,
                Name = v.Name,
                Branch = v.Branch,
                CultureCode = v.Culture != null ? v.Culture.Code : string.Empty,
                CultureName = v.Culture != null ? v.Culture.Name : string.Empty,

                Class = v.Class.ToString(),
                Level = v.Level,
                PurchaseCost = v.PurchaseCost,
                IsVisible = v.IsVisible, // 🔹 додано

                HP = v.HP,
                Damage = v.Damage,
                Accuracy = v.Accuracy,
                Speed = v.Speed,
                Acceleration = v.Acceleration,
                TraverseSpeed = v.TraverseSpeed,
                Armor = $"{v.Armor}"
            });
        }

        // GET /warriors/by-code/{code}
        [HttpGet("by-code/{code}")]
        public IActionResult GetByCode(string code)
        {
            var v = _db.Warriors
                .Include(x => x.Culture)
                .FirstOrDefault(x => x.Code == code);
            if (v == null) return NotFound();

            return Ok(new WarriorDto
            {
                Id = v.Id,
                Code = v.Code,
                Name = v.Name,
                Branch = v.Branch,
                CultureCode = v.Culture != null ? v.Culture.Code : string.Empty,
                CultureName = v.Culture != null ? v.Culture.Name : string.Empty,

                Class = v.Class.ToString(),
                Level = v.Level,
                PurchaseCost = v.PurchaseCost,
                IsVisible = v.IsVisible, // 🔹 додано

                HP = v.HP,
                Damage = v.Damage,
                Accuracy = v.Accuracy,
                Speed = v.Speed,
                Acceleration = v.Acceleration,
                TraverseSpeed = v.TraverseSpeed,
                Armor = $"{v.Armor}"
            });
        }

        // --- TECH TREE LINKS ---

        // GET /warriors/{id}/research-from
        [HttpGet("{id:int}/research-from")]
        public IActionResult GetResearchFrom(int id)
        {
            var links = _db.WarriorResearchRequirements
                .Where(r => r.SuccessorWarriorId == id)
                .Select(r => new
                {
                    predecessorId = r.PredecessorWarriorId,
                    requiredXp = r.RequiredXpOnPredecessor
                })
                .ToList();

            return Ok(links);
        }

        public class CreateLinkDto
        {
            public int PredecessorWarriorId { get; set; }
            public int SuccessorWarriorId { get; set; }
            public int RequiredXpOnPredecessor { get; set; }
        }

        // POST /warriors/links
        [HttpPost("links")]
        public IActionResult CreateLink([FromBody] CreateLinkDto dto)
        {
            if (dto.PredecessorWarriorId == dto.SuccessorWarriorId)
            {
                return BadRequest("predecessor == successor");
            }

            bool ok = _db.Warriors.Any(v => v.Id == dto.PredecessorWarriorId)
                   && _db.Warriors.Any(v => v.Id == dto.SuccessorWarriorId);
            if (!ok) return NotFound("warrior not found");

            bool dup = _db.WarriorResearchRequirements
                .Any(x => x.PredecessorWarriorId == dto.PredecessorWarriorId
                       && x.SuccessorWarriorId == dto.SuccessorWarriorId);
            if (dup) return Conflict("link exists");

            var link = new WarriorResearchRequirement
            {
                PredecessorWarriorId = dto.PredecessorWarriorId,
                SuccessorWarriorId = dto.SuccessorWarriorId,
                RequiredXpOnPredecessor = dto.RequiredXpOnPredecessor
            };

            _db.WarriorResearchRequirements.Add(link);
            _db.SaveChanges();

            return Ok(new { link.Id });
        }

        // DELETE /warriors/links/{id}
        [HttpDelete("links/{id:int}")]
        public IActionResult DeleteLink(int id)
        {
            var link = _db.WarriorResearchRequirements.FirstOrDefault(x => x.Id == id);
            if (link == null) return NotFound();

            _db.WarriorResearchRequirements.Remove(link);
            _db.SaveChanges();

            return NoContent();
        }

        // --- GRAPH (для дерева розвитку) ---

        // GET /warriors/graph?faction=iron_alliance
        [HttpGet("graph")]
        public IActionResult GetGraph([FromQuery] string? faction = null)
        {
            var vq = _db.Warriors
                .Include(v => v.Culture)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(faction))
            {
                string fc = faction.Trim();
                vq = vq.Where(v => v.Culture != null && v.Culture.Code == fc);
            }

            var nodes = vq
                .Select(v => new
                {
                    id = v.Id,
                    code = v.Code,
                    name = v.Name,
                    @class = v.Class.ToString(),
                    level = v.Level,
                    branch = v.Branch,
                    cultureCode = v.Culture != null ? v.Culture.Code : string.Empty,
                    isVisible = v.IsVisible // 🔹 додано
                })
                .ToList();

            var nodeIds = nodes.Select(n => n.id).ToList();

            var edges = _db.WarriorResearchRequirements
                .Where(r => nodeIds.Contains(r.PredecessorWarriorId) || nodeIds.Contains(r.SuccessorWarriorId))
                .Select(r => new
                {
                    fromId = r.PredecessorWarriorId,
                    toId = r.SuccessorWarriorId,
                    requiredXp = r.RequiredXpOnPredecessor
                })
                .ToList();

            return Ok(new { nodes, edges });
        }
    }

    // DTO
    public class WarriorDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        public string Branch { get; set; } = string.Empty;
        public string CultureCode { get; set; } = string.Empty;
        public string CultureName { get; set; } = string.Empty;

        public string Class { get; set; } = string.Empty;
        public int Level { get; set; }
        public int PurchaseCost { get; set; }
        public bool IsVisible { get; set; } // 🔹 нове поле

        public int HP { get; set; }
        public int Damage { get; set; }
        public float Accuracy { get; set; }
        public float Speed { get; set; }
        public float Acceleration { get; set; }
        public float TraverseSpeed { get; set; }
        public string Armor { get; set; } = "0/0/0";
    }
}
