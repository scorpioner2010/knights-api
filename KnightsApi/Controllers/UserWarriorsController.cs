using System;
using System.Linq;
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

        // ✅ Безпечне читання UID без ризику 500
        private bool TryGetCurrentUserId(out int uid)
        {
            uid = 0;
            var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(idStr, out uid) && uid > 0;
        }

        // (залишаю для сумісності; не використовується далі)
        private int CurrentUserId()
        {
            var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.Parse(idStr);
        }

        // ========================================
        // GET /user-warriors/me
        // ========================================
        [HttpGet("me")]
        public IActionResult GetMyWarriors()
        {
            if (!TryGetCurrentUserId(out var uid))
                return Unauthorized();

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

            var freeXp = _db.Players
                .Where(p => p.Id == uid)
                .Select(p => p.FreeXp)
                .FirstOrDefault();

            return Ok(new
            {
                FreeXp = freeXp,
                Warriors = list
            });
        }

        // ========================================
        // PUT /user-warriors/me/active/{warriorId}
        // Уникаємо 23505 на IX_UserWarriors_UserId_IsActive:
        // знімаємо старий TRUE -> SaveChanges -> виставляємо новий TRUE -> SaveChanges
        // ========================================
        [HttpPut("me/active/{warriorId:int}")]
        public IActionResult SetActive(int warriorId)
        {
            if (!TryGetCurrentUserId(out var uid))
                return Unauthorized();

            using var tx = _db.Database.BeginTransaction();

            var owned = _db.UserWarriors
                .Where(x => x.UserId == uid)
                .ToList();

            var target = owned.FirstOrDefault(x => x.WarriorId == warriorId);
            if (target == null)
                return NotFound("User does not own this warrior.");

            // Якщо вже активний — все ок
            if (target.IsActive)
            {
                tx.Commit();
                return Ok(new { ok = true, activeWarriorId = warriorId });
            }

            // 1) зняти активний, якщо є
            var currentActive = owned.FirstOrDefault(x => x.IsActive);
            if (currentActive != null)
            {
                currentActive.IsActive = false;
                _db.SaveChanges(); // важливо: зняти TRUE перед встановленням нового
            }

            // 2) позначити ціль активним
            target.IsActive = true;
            _db.SaveChanges();

            tx.Commit();
            return Ok(new { ok = true, activeWarriorId = warriorId });
        }

        // ========================================
        // POST /user-warriors/me/buy/{code}
        // Покупка за кодом каталогу Warrior
        // ========================================
        [HttpPost("me/buy/{code}")]
        public IActionResult BuyWarrior(string code)
        {
            if (!TryGetCurrentUserId(out var uid))
                return Unauthorized();

            var player = _db.Players.FirstOrDefault(p => p.Id == uid);
            if (player == null)
                return NotFound("Player not found.");

            var warrior = _db.Warriors.FirstOrDefault(v => v.Code == code);
            if (warrior == null)
                return NotFound("Warrior code not found.");

            if (_db.UserWarriors.Any(x => x.UserId == uid && x.WarriorId == warrior.Id))
                return Conflict("Warrior already owned.");

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

            return Ok(new
            {
                ok = true,
                userWarriorId = uw.Id,
                warriorId = uw.WarriorId,
                newBolts = player.Coins
            });
        }

        // ========================================
        // POST /user-warriors/me/sell/{warriorId}
        // Продає воїна за 50% від PurchaseCost (безпечний порядок апдейтів)
        // ========================================
        [HttpPost("me/sell/{warriorId:int}")]
        public IActionResult Sell(int warriorId)
        {
            if (!TryGetCurrentUserId(out var uid))
                return Unauthorized();

            using var tx = _db.Database.BeginTransaction();

            var owned = _db.UserWarriors
                .Where(x => x.UserId == uid)
                .Include(x => x.Warrior)
                .ToList();

            if (owned.Count <= 1)
                return BadRequest("Cannot sell your last remaining warrior.");

            var uw = owned.FirstOrDefault(x => x.WarriorId == warriorId);
            if (uw == null)
                return NotFound("Warrior not found.");

            if (uw.Warrior == null)
                return BadRequest("Warrior data is missing.");

            var player = _db.Players.FirstOrDefault(p => p.Id == uid);
            if (player == null)
                return NotFound("Player not found.");

            int refund = Math.Max(0, uw.Warrior.PurchaseCost / 2);

            // Якщо активний — спершу зняти активність, зберегти
            bool wasActive = uw.IsActive;
            if (wasActive)
            {
                uw.IsActive = false;
                _db.SaveChanges(); // зняли TRUE, індекс щасливий
            }

            // Видалити та повернути болти
            _db.UserWarriors.Remove(uw);
            player.Coins += refund;
            _db.SaveChanges();

            // Якщо продавали активного — призначити інший активним і зберегти
            if (wasActive)
            {
                var replacement = _db.UserWarriors
                    .Where(x => x.UserId == uid)
                    .OrderByDescending(x => x.Xp) // або інша твоя логіка вибору
                    .FirstOrDefault();

                if (replacement != null)
                {
                    replacement.IsActive = true;
                    _db.SaveChanges();
                }
            }

            tx.Commit();
            return Ok(new
            {
                ok = true,
                soldWarriorId = warriorId,
                refundBolts = refund,
                newBolts = player.Coins
            });
        }

        // ========================================
        // POST /user-warriors/me/add-by-code/{code}
        // (dev/debug) Додає безкоштовно
        // ========================================
        [HttpPost("me/add-by-code/{code}")]
        public IActionResult AddByCode(string code)
        {
            if (!TryGetCurrentUserId(out var uid))
                return Unauthorized();

            var warrior = _db.Warriors.FirstOrDefault(v => v.Code == code);
            if (warrior == null)
                return NotFound("Warrior code not found.");

            bool already = _db.UserWarriors.Any(x => x.UserId == uid && x.WarriorId == warrior.Id);
            if (already)
                return Conflict("Warrior already owned.");

            var uw = new UserWarrior
            {
                UserId = uid,
                WarriorId = warrior.Id,
                Xp = 0,
                IsActive = false
            };

            _db.UserWarriors.Add(uw);
            _db.SaveChanges();

            return Ok(new { ok = true, userWarriorId = uw.Id, warriorId = uw.WarriorId });
        }

        // ========================================
        // DELETE /user-warriors/me/{warriorId}
        // Жорстке видалення (з урахуванням активного) — безпечний порядок
        // ========================================
        [HttpDelete("me/{warriorId:int}")]
        public IActionResult Remove(int warriorId)
        {
            if (!TryGetCurrentUserId(out var uid))
                return Unauthorized();

            using var tx = _db.Database.BeginTransaction();

            var owned = _db.UserWarriors
                .Where(x => x.UserId == uid)
                .Include(x => x.Warrior)
                .ToList();

            if (owned.Count <= 1)
                return BadRequest("Cannot remove your last remaining warrior.");

            var uw = owned.FirstOrDefault(x => x.WarriorId == warriorId);
            if (uw == null)
                return NotFound("Warrior not found.");

            bool wasActive = uw.IsActive;

            if (wasActive)
            {
                uw.IsActive = false;
                _db.SaveChanges(); // зняти TRUE до будь-яких інших рухів
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

        // ========================================
        // GET /user-warriors/xp
        // ========================================
        [HttpGet("xp")]
        public IActionResult GetMyWarriorsXp()
        {
            if (!TryGetCurrentUserId(out var uid))
                return Unauthorized();

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

            var freeXp = _db.Players
                .Where(p => p.Id == uid)
                .Select(p => p.FreeXp)
                .FirstOrDefault();

            return Ok(new
            {
                FreeXp = freeXp,
                Warriors = list
            });
        }

        // ========================================
        // POST /user-warriors/{warriorId}/convert-freexp
        // ========================================
        [HttpPost("{warriorId:int}/convert-freexp")]
        public IActionResult ConvertFreeXpToWarrior(int warriorId, [FromBody] ConvertFreeXpRequest req)
        {
            if (!TryGetCurrentUserId(out var uid))
                return Unauthorized();

            if (req.Amount <= 0)
                return BadRequest("Amount must be positive.");

            var player = _db.Players.FirstOrDefault(p => p.Id == uid);
            if (player == null)
                return NotFound("Player not found.");

            if (player.FreeXp < req.Amount)
                return BadRequest("Not enough Free XP.");

            var uw = _db.UserWarriors
                .Include(x => x.Warrior)
                .FirstOrDefault(x => x.UserId == uid && x.WarriorId == warriorId);

            if (uw == null)
                return NotFound("Warrior not found or not owned.");

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

        // ========================================
        // DTOs
        // ========================================
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
    }
}
