using System;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KnightsApi.Data;

namespace KnightsApi.Controllers
{
    [ApiController]
    [Route("players")]
    [Authorize]
    public class PlayersController : ControllerBase
    {
        private readonly AppDbContext _db;

        public PlayersController(AppDbContext db)
        {
            _db = db;
        }

        // М'яке читання поточного користувача без ризику 500:
        // повертає true/false і uid через out; при невдачі — клієнту віддамо 401.
        private bool TryGetCurrentUserId(out int uid)
        {
            uid = 0;
            var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(idStr, out uid) && uid > 0;
        }

        [HttpGet("me")]
        public IActionResult GetMe()
        {
            if (!TryGetCurrentUserId(out var uid))
                return Unauthorized();

            var p = _db.Players.FirstOrDefault(x => x.Id == uid);
            if (p == null) return NotFound();

            var owned = _db.UserWarriors
                .Where(u => u.UserId == uid)                 // ВАЖЛИВО: саме UserId
                .Include(u => u.Warrior)
                .AsNoTracking()
                .ToList();

            var active = owned.FirstOrDefault(v => v.IsActive);

            var dto = new PlayerProfileDto
            {
                Id = p.Id,
                Username = p.Username,
                IsAdmin = p.IsAdmin,
                Mmr = p.Mmr,
                Coins = p.Coins,
                Gold = p.Gold,
                FreeXp = p.FreeXp,

                ActiveWarriorId = active?.WarriorId ?? 0,
                ActiveWarriorCode = active?.Warrior?.Code ?? string.Empty,
                ActiveWarriorName = active?.Warrior?.Name ?? string.Empty,

                OwnedWarriors = owned.Select(v => new OwnedWarriorDto
                {
                    WarriorId = v.WarriorId,
                    Code = v.Warrior?.Code ?? string.Empty,
                    Name = v.Warrior?.Name ?? string.Empty,
                    IsActive = v.IsActive,
                    Xp = v.Xp
                }).ToList()
            };

            return Ok(dto);
        }

        [HttpPut("me/active/{warriorId:int}")]
        public IActionResult SetActive(int warriorId)
        {
            if (!TryGetCurrentUserId(out var uid))
                return Unauthorized();

            // Використовуємо безпечний порядок оновлень як у UserWarriorsController,
            // щоб не впертись у фільтрований унікальний індекс (UserId, IsActive=TRUE).
            using var tx = _db.Database.BeginTransaction();

            var owned = _db.UserWarriors
                .Where(x => x.UserId == uid)
                .ToList();

            var target = owned.FirstOrDefault(x => x.WarriorId == warriorId);
            if (target == null)
                return NotFound("User does not own this warrior.");

            // Якщо вже активний — просто повертаємо OK
            if (target.IsActive)
            {
                tx.Commit();
                return Ok(new { ok = true, activeWarriorId = warriorId });
            }

            // 1) зняти активний, якщо є, і зберегти (щоб не було 23505)
            var currentActive = owned.FirstOrDefault(x => x.IsActive);
            if (currentActive != null)
            {
                currentActive.IsActive = false;
                _db.SaveChanges();
            }

            // 2) позначити ціль активним і зберегти
            target.IsActive = true;
            _db.SaveChanges();

            tx.Commit();
            return Ok(new { ok = true, activeWarriorId = warriorId });
        }
    }

    public class PlayerProfileDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = "";
        public bool IsAdmin { get; set; }
        public int Mmr { get; set; }
        public int Coins { get; set; }
        public int Gold { get; set; }
        public int FreeXp { get; set; }

        public int ActiveWarriorId { get; set; }
        public string ActiveWarriorCode { get; set; } = "";
        public string ActiveWarriorName { get; set; } = "";

        public List<OwnedWarriorDto> OwnedWarriors { get; set; } = new();
    }

    public class OwnedWarriorDto
    {
        public int WarriorId { get; set; }
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public bool IsActive { get; set; }
        public int Xp { get; set; }
    }
}
