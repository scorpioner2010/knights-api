using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KnightsApi.Data;
using KnightsApi.Models;

namespace KnightsApi.Controllers
{
    [ApiController]
    [Route("user-warriors")]
    [Authorize]
    public class UserWarriorsController : ControllerBase
    {
        private readonly AppDbContext _db;

        public UserWarriorsController(AppDbContext db)
        {
            _db = db;
        }

        // -------- helpers --------

        private bool TryGetCurrentUserId(out int uid)
        {
            uid = 0;
            var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(idStr, out uid) && uid > 0;
        }

        private bool IsUnlocked(int userId, Warrior w)
        {
            if (w.Level <= 1) return true;
            return _db.UserUnlockedWarriors.Any(x => x.UserId == userId && x.WarriorId == w.Id);
        }

        // -------- queries --------

        [HttpGet("me")]
        public IActionResult GetMyWarriors()
        {
            if (!TryGetCurrentUserId(out var uid)) return Unauthorized();

            var list = _db.UserWarriors
                .Where(x => x.UserId == uid)
                .Include(x => x.Warrior)
                .Select(x => new UserWarriorDto
                {
                    Id = x.Id,
                    WarriorId = x.WarriorId,
                    WarriorCode = x.Warrior != null ? x.Warrior.Code : string.Empty,
                    WarriorName = x.Warrior != null ? x.Warrior.Name : string.Empty,
                    Xp = x.Xp,
                    IsActive = x.IsActive
                })
                .ToList();

            var freeXp = _db.Players.Where(p => p.Id == uid).Select(p => p.FreeXp).FirstOrDefault();

            return Ok(new { FreeXp = freeXp, Warriors = list });
        }

        [HttpPut("me/active/{warriorId:int}")]
        public IActionResult SetActive(int warriorId)
        {
            if (!TryGetCurrentUserId(out var uid)) return Unauthorized();

            using var tx = _db.Database.BeginTransaction();

            var owned = _db.UserWarriors.Where(x => x.UserId == uid).ToList();
            var target = owned.FirstOrDefault(x => x.WarriorId == warriorId);
            if (target == null) return NotFound("User does not own this warrior.");

            if (target.IsActive)
            {
                tx.Commit();
                return Ok(new { ok = true, activeWarriorId = warriorId });
            }

            var currentActive = owned.FirstOrDefault(x => x.IsActive);
            if (currentActive != null)
            {
                currentActive.IsActive = false;
                _db.SaveChanges();
            }

            target.IsActive = true;
            _db.SaveChanges();

            tx.Commit();
            return Ok(new { ok = true, activeWarriorId = warriorId });
        }

        // -------- buy / sell --------

        [HttpPost("me/buy/{code}")]
        public IActionResult BuyWarrior(string code)
        {
            if (!TryGetCurrentUserId(out var uid)) return Unauthorized();

            var player = _db.Players.FirstOrDefault(p => p.Id == uid);
            if (player == null) return NotFound("Player not found.");

            var warrior = _db.Warriors.FirstOrDefault(v => v.Code == code);
            if (warrior == null) return NotFound("Warrior code not found.");

            if (_db.UserWarriors.Any(x => x.UserId == uid && x.WarriorId == warrior.Id))
                return Conflict("Warrior already owned.");

            // 🔹 ВАЖЛИВО: перевірка «досліджено?» для рівнів > 1
            if (!IsUnlocked(uid, warrior))
                return BadRequest("Warrior is not researched (locked).");

            if (player.Coins < warrior.PurchaseCost)
                return BadRequest("Not enough Bolts.");

            player.Coins -= warrior.PurchaseCost;

            var uw = new UserWarrior
            {
                UserId = uid,
                WarriorId = warrior.Id,
                Xp = 0,
                IsActive = false
            };

            _db.UserWarriors.Add(uw);
            _db.SaveChanges();

            return Ok(new { ok = true, userWarriorId = uw.Id, warriorId = uw.WarriorId, newBolts = player.Coins });
        }

        [HttpPost("me/sell/{warriorId:int}")]
        public IActionResult Sell(int warriorId)
        {
            if (!TryGetCurrentUserId(out var uid)) return Unauthorized();

            using var tx = _db.Database.BeginTransaction();

            var owned = _db.UserWarriors
                .Where(x => x.UserId == uid)
                .Include(x => x.Warrior)
                .ToList();

            if (owned.Count <= 1) return BadRequest("Cannot sell your last remaining warrior.");

            var uw = owned.FirstOrDefault(x => x.WarriorId == warriorId);
            if (uw == null) return NotFound("Warrior not found.");
            if (uw.Warrior == null) return BadRequest("Warrior data is missing.");

            var player = _db.Players.FirstOrDefault(p => p.Id == uid);
            if (player == null) return NotFound("Player not found.");

            int refund = Math.Max(0, uw.Warrior.PurchaseCost / 2);

            bool wasActive = uw.IsActive;
            if (wasActive)
            {
                uw.IsActive = false;
                _db.SaveChanges();
            }

            _db.UserWarriors.Remove(uw);
            player.Coins += refund;
            _db.SaveChanges();

            if (wasActive)
            {
                var replacement = _db.UserWarriors
                    .Where(x => x.UserId == uid)
                    .OrderByDescending(x => x.Xp)
                    .FirstOrDefault();

                if (replacement != null)
                {
                    replacement.IsActive = true;
                    _db.SaveChanges();
                }
            }

            tx.Commit();
            return Ok(new { ok = true, soldWarriorId = warriorId, refundBolts = refund, newBolts = player.Coins });
        }

        [HttpPost("me/add-by-code/{code}")]
        public IActionResult AddByCode(string code)
        {
            if (!TryGetCurrentUserId(out var uid)) return Unauthorized();

            var warrior = _db.Warriors.FirstOrDefault(v => v.Code == code);
            if (warrior == null) return NotFound("Warrior code not found.");

            bool already = _db.UserWarriors.Any(x => x.UserId == uid && x.WarriorId == warrior.Id);
            if (already) return Conflict("Warrior already owned.");

            var uw = new UserWarrior { UserId = uid, WarriorId = warrior.Id, Xp = 0, IsActive = false };
            _db.UserWarriors.Add(uw);
            _db.SaveChanges();

            return Ok(new { ok = true, userWarriorId = uw.Id, warriorId = uw.WarriorId });
        }

        [HttpDelete("me/{warriorId:int}")]
        public IActionResult Remove(int warriorId)
        {
            if (!TryGetCurrentUserId(out var uid)) return Unauthorized();

            using var tx = _db.Database.BeginTransaction();

            var owned = _db.UserWarriors
                .Where(x => x.UserId == uid)
                .Include(x => x.Warrior)
                .ToList();

            if (owned.Count <= 1) return BadRequest("Cannot remove your last remaining warrior.");

            var uw = owned.FirstOrDefault(x => x.WarriorId == warriorId);
            if (uw == null) return NotFound("Warrior not found.");

            bool wasActive = uw.IsActive;
            if (wasActive)
            {
                uw.IsActive = false;
                _db.SaveChanges();
            }

            _db.UserWarriors.Remove(uw);
            _db.SaveChanges();

            if (wasActive)
            {
                var replacement = _db.UserWarriors
                    .Where(x => x.UserId == uid)
                    .OrderByDescending(x => x.Xp)
                    .FirstOrDefault();

                if (replacement != null)
                {
                    replacement.IsActive = true;
                    _db.SaveChanges();
                }
            }

            tx.Commit();
            return Ok(new { ok = true, removedWarriorId = warriorId });
        }

        [HttpGet("xp")]
        public IActionResult GetMyWarriorsXp()
        {
            if (!TryGetCurrentUserId(out var uid)) return Unauthorized();

            var list = _db.UserWarriors
                .Where(x => x.UserId == uid)
                .Include(x => x.Warrior)
                .Select(x => new
                {
                    WarriorId = x.WarriorId,
                    WarriorName = x.Warrior != null ? x.Warrior.Name : "",
                    x.Xp,
                    x.IsActive
                })
                .ToList();

            var freeXp = _db.Players.Where(p => p.Id == uid).Select(p => p.FreeXp).FirstOrDefault();

            return Ok(new { FreeXp = freeXp, Warriors = list });
        }

        [HttpPost("{warriorId:int}/convert-freexp")]
        public IActionResult ConvertFreeXpToWarrior(int warriorId, [FromBody] ConvertFreeXpRequest req)
        {
            if (!TryGetCurrentUserId(out var uid)) return Unauthorized();
            if (req.Amount <= 0) return BadRequest("Amount must be positive.");

            var player = _db.Players.FirstOrDefault(p => p.Id == uid);
            if (player == null) return NotFound("Player not found.");
            if (player.FreeXp < req.Amount) return BadRequest("Not enough Free XP.");

            var uw = _db.UserWarriors
                .Include(x => x.Warrior)
                .FirstOrDefault(x => x.UserId == uid && x.WarriorId == warriorId);

            if (uw == null) return NotFound("Warrior not found or not owned.");

            player.FreeXp -= req.Amount;
            uw.Xp += req.Amount;

            _db.SaveChanges();

            return Ok(new
            {
                ok = true,
                warriorId = uw.WarriorId,
                warriorName = uw.Warrior?.Name,
                addedXp = req.Amount,
                newWarriorXp = uw.Xp,
                remainingFreeXp = player.FreeXp
            });
        }

        // -------- research (unlock) --------

        public class ResearchUnlockRequest
        {
            public int SuccessorWarriorId { get; set; }
            public int PredecessorWarriorId { get; set; }
        }

        [HttpPost("me/research-unlock")]
        public IActionResult ResearchUnlock([FromBody] ResearchUnlockRequest req)
        {
            if (!TryGetCurrentUserId(out var uid)) return Unauthorized();

            using var tx = _db.Database.BeginTransaction();

            var successor = _db.Warriors.FirstOrDefault(w => w.Id == req.SuccessorWarriorId);
            if (successor == null) return NotFound("Successor not found.");

            if (successor.Level <= 1)
            {
                var existsL1 = _db.UserUnlockedWarriors.Any(x => x.UserId == uid && x.WarriorId == successor.Id);
                if (!existsL1)
                {
                    _db.UserUnlockedWarriors.Add(new UserUnlockedWarrior { UserId = uid, WarriorId = successor.Id });
                    _db.SaveChanges();
                }
                tx.Commit();
                return Ok(new { ok = true, unlockedWarriorId = successor.Id });
            }

            if (_db.UserUnlockedWarriors.Any(x => x.UserId == uid && x.WarriorId == successor.Id))
            {
                tx.Commit();
                return Ok(new { ok = true, unlockedWarriorId = successor.Id });
            }

            var link = _db.WarriorResearchRequirements
                .FirstOrDefault(r => r.SuccessorWarriorId == successor.Id && r.PredecessorWarriorId == req.PredecessorWarriorId);

            if (link == null) return BadRequest("Research link not found.");

            var ownedPred = _db.UserWarriors.FirstOrDefault(u => u.UserId == uid && u.WarriorId == link.PredecessorWarriorId);
            if (ownedPred == null) return BadRequest("Predecessor is not owned.");

            if (ownedPred.Xp < link.RequiredXpOnPredecessor)
                return BadRequest("Not enough XP on predecessor.");

            ownedPred.Xp -= link.RequiredXpOnPredecessor;

            _db.UserUnlockedWarriors.Add(new UserUnlockedWarrior { UserId = uid, WarriorId = successor.Id });
            _db.SaveChanges();

            tx.Commit();
            return Ok(new
            {
                ok = true,
                unlockedWarriorId = successor.Id,
                predecessorId = link.PredecessorWarriorId,
                xpSpent = link.RequiredXpOnPredecessor,
                predecessorNewXp = ownedPred.Xp
            });
        }

        [HttpGet("me/unlocked")]
        public IActionResult GetMyUnlocked()
        {
            if (!TryGetCurrentUserId(out var uid)) return Unauthorized();

            var ids = _db.UserUnlockedWarriors
                .Where(x => x.UserId == uid)
                .Select(x => x.WarriorId)
                .ToList();

            var payload = ids.Select(i => new IntWrap { value = i }).ToList();
            return Ok(payload);
        }

        // -------- DTOs --------

        public class UserWarriorDto
        {
            public int Id { get; set; }
            public int WarriorId { get; set; }
            public string WarriorCode { get; set; } = string.Empty;
            public string WarriorName { get; set; } = string.Empty;
            public int Xp { get; set; }
            public bool IsActive { get; set; }
        }

        public class ConvertFreeXpRequest
        {
            public int Amount { get; set; }
        }

        public class IntWrap
        {
            public int value { get; set; }
        }
    }
}
