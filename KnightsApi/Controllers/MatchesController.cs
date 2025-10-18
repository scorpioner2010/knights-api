using System;
using System.Collections.Generic;
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
    [Route("matches")]
    [Authorize]
    public class MatchesController : ControllerBase
    {
        private readonly AppDbContext _db;

        private const int XpWinBase = 300;
        private const int XpDrawBase = 200;
        private const int XpLoseBase = 150;
        private const double XpPerDamage = 0.5;
        private const int XpPerKill = 50;
        private const int XpCapPerBattle = 2000;

        private const int BoltsBaseParticipation = 500;
        private const int BoltsWinBonus = 500;
        private const int BoltsPerDamage = 2;
        private const int BoltsPerKill = 150;
        private const int BoltsCapPerBattle = 10000;

        private const int MmrK = 24;
        private const int MmrCapGain = 30;
        private const int MmrCapLoss = -30;

        private const int MaxKillsPerBattle = 20;
        private const int MaxDamagePerBattle = 20000;
        private const int MinKills = 0;
        private const int MinDamage = 0;

        private const double FreeXpPercent = 0.05;

        public MatchesController(AppDbContext db)
        {
            _db = db;
        }

        private int CurrentUserId()
        {
            var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
            return int.Parse(idStr);
        }

        [HttpPost("start")]
        public IActionResult StartMatch([FromBody] StartMatchRequest req)
        {
            var match = new Match
            {
                Map = string.IsNullOrWhiteSpace(req.Map) ? "default_map" : req.Map,
                StartedAt = DateTimeOffset.UtcNow
            };
            _db.Matches.Add(match);
            _db.SaveChanges();
            return Ok(new { matchId = match.Id });
        }

        public class StartMatchRequest
        {
            public string Map { get; set; } = "default_map";
        }

        [HttpPost("{matchId:int}/end/me")]
        public IActionResult EndMatchForMe(int matchId, [FromBody] ParticipantMeInput raw)
        {
            if (raw == null)
            {
                return BadRequest("Body required.");
            }

            var match = _db.Matches.FirstOrDefault(m => m.Id == matchId);
            if (match == null)
            {
                return NotFound("Match not found.");
            }

            if (match.EndedAt != null)
            {
                return BadRequest("Match already ended.");
            }

            var userId = CurrentUserId();
            if (_db.MatchParticipants.Any(x => x.MatchId == matchId && x.UserId == userId))
            {
                return Conflict("Results already submitted for this user.");
            }

            var user = _db.Players.FirstOrDefault(u => u.Id == userId);
            if (user == null)
            {
                return BadRequest("User not found.");
            }

            var code = (raw.WarriorCode ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(code))
            {
                return BadRequest("WarriorCode required.");
            }

            var warrior =
                _db.Warriors.FirstOrDefault(w => w.Code == code) ??
                _db.Warriors.FirstOrDefault(w => w.Code.ToLower() == code.ToLower());

            if (warrior == null)
            {
                return BadRequest("Warrior not found by code.");
            }

            var uw = _db.UserWarriors.FirstOrDefault(v => v.UserId == userId && v.WarriorId == warrior.Id);
            if (uw == null)
            {
                return BadRequest("Warrior does not belong to the user.");
            }

            var teamToUserIds = new Dictionary<int, List<int>> { { raw.Team, new List<int> { userId } } };
            var teamAvgMmr = new Dictionary<int, double> { { raw.Team, user.Mmr } };

            using var tx = _db.Database.BeginTransaction();
            match.EndedAt = DateTimeOffset.UtcNow;

            int kills = Math.Clamp(raw.Kills, MinKills, MaxKillsPerBattle);
            int damage = Math.Clamp(raw.Damage, MinDamage, MaxDamagePerBattle);
            string result = NormalizeResult(raw.Result);

            int xpBase = result switch
            {
                "win" => XpWinBase,
                "draw" => XpDrawBase,
                _ => XpLoseBase
            };
            int xpFromDamage = (int)Math.Round(damage * XpPerDamage, MidpointRounding.AwayFromZero);
            int xpFromKills = kills * XpPerKill;
            int xpTotal = Math.Clamp(xpBase + xpFromDamage + xpFromKills, 0, XpCapPerBattle);

            int bolts = BoltsBaseParticipation
                        + (result == "win" ? BoltsWinBonus : 0)
                        + (damage * BoltsPerDamage)
                        + (kills * BoltsPerKill);
            bolts = Math.Clamp(bolts, 0, BoltsCapPerBattle);

            int enemyTeam = FindEnemyTeam(teamToUserIds.Keys.ToList(), raw.Team);
            double enemyAvg = teamAvgMmr.TryGetValue(enemyTeam, out var en) ? en : 1000.0;
            double expected = 1.0 / (1.0 + Math.Pow(10.0, (enemyAvg - user.Mmr) / 400.0));
            double score = result switch
            {
                "win" => 1.0,
                "draw" => 0.5,
                _ => 0.0
            };
            int mmrDelta = (int)Math.Round(MmrK * (score - expected), MidpointRounding.AwayFromZero);
            mmrDelta = Math.Clamp(mmrDelta, MmrCapLoss, MmrCapGain);

            MatchParticipant mp = new MatchParticipant
            {
                MatchId = match.Id,
                UserId = userId,
                WarriorId = warrior.Id,
                Team = raw.Team,
                Result = result,
                Kills = kills,
                Damage = damage,
                XpEarned = xpTotal,
                MmrDelta = mmrDelta
            };
            _db.MatchParticipants.Add(mp);

            user.Mmr += mmrDelta;
            user.Coins += bolts;
            user.FreeXp += (int)Math.Round(xpTotal * FreeXpPercent, MidpointRounding.AwayFromZero);

            uw.Xp += xpTotal;

            _db.SaveChanges();
            tx.Commit();
            return Ok(new { ok = true });
        }

        [HttpGet("{matchId:int}/participants")]
        public IActionResult GetParticipants(int matchId)
        {
            var list = _db.MatchParticipants
                .Where(x => x.MatchId == matchId)
                .Include(x => x.User)
                .Include(x => x.Warrior)
                .Select(x => new
                {
                    x.UserId,
                    Username = x.User != null ? x.User.Username : string.Empty,
                    WarriorId = x.WarriorId,
                    WarriorCode = x.Warrior != null ? x.Warrior.Code : string.Empty,
                    WarriorName = x.Warrior != null ? x.Warrior.Name : string.Empty,
                    x.Team,
                    x.Result,
                    x.Kills,
                    x.Damage,
                    x.XpEarned,
                    x.MmrDelta
                })
                .ToList();

            return Ok(list);
        }

        private static string NormalizeResult(string input)
        {
            if (string.Equals(input, "win", StringComparison.OrdinalIgnoreCase)) { return "win"; }
            if (string.Equals(input, "draw", StringComparison.OrdinalIgnoreCase)) { return "draw"; }
            return "lose";
        }

        private static int FindEnemyTeam(List<int> teams, int myTeam)
        {
            foreach (var t in teams)
            {
                if (t != myTeam)
                {
                    return t;
                }
            }
            return myTeam;
        }

        public class ParticipantMeInput
        {
            public string WarriorCode { get; set; } = string.Empty;
            public int Team { get; set; }
            public string Result { get; set; } = "lose";
            public int Kills { get; set; } = 0;
            public int Damage { get; set; } = 0;
        }
    }
}
