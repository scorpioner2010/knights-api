using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WarOfMachines.Data;

namespace WarOfMachines.Controllers
{
    [ApiController]
    [Route("leaderboard")]
    [Authorize]
    public class LeaderboardController : ControllerBase
    {
        private readonly AppDbContext _db;

        public LeaderboardController(AppDbContext db)
        {
            _db = db;
        }

        // GET /leaderboard/mmr?top=10
        [HttpGet("mmr")]
        public IActionResult GetByMmr([FromQuery] int top = 10)
        {
            var list = _db.Players
                .OrderByDescending(p => p.Mmr)
                .Take(top)
                .Select(p => new LeaderboardEntry
                {
                    UserId = p.Id,
                    Username = p.Username,
                    Value = p.Mmr
                })
                .ToList();

            return Ok(list);
        }

        // GET /leaderboard/free-xp?top=10
        [HttpGet("free-xp")]
        public IActionResult GetByFreeXp([FromQuery] int top = 10)
        {
            var list = _db.Players
                .OrderByDescending(p => p.FreeXp)
                .Take(top)
                .Select(p => new LeaderboardEntry
                {
                    UserId = p.Id,
                    Username = p.Username,
                    Value = p.FreeXp
                })
                .ToList();

            return Ok(list);
        }

        // GET /leaderboard/vehicle-xp?top=10
        // Рейтинг за досвідом конкретних роботів
        [HttpGet("vehicle-xp")]
        public IActionResult GetByVehicleXp([FromQuery] int top = 10)
        {
            var list = _db.UserUnits
                .OrderByDescending(v => v.Xp)
                .Take(top)
                .Select(v => new
                {
                    UserId = v.UserId,
                    Username = v.User != null ? v.User.Username : "",
                    VehicleName = v.Unit != null ? v.Unit.Name : "",
                    Xp = v.Xp
                })
                .ToList();

            return Ok(list);
        }
    }

    public class LeaderboardEntry
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public int Value { get; set; }
    }
}
