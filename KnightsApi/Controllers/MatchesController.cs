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

        // --- Параметри нарахувань ---
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

        // --- MMR ---
        private const int MmrK = 24;
        private const int MmrCapGain = 30;
        private const int MmrCapLoss = -30;

        // --- АНТИ-ЧИТ КАПИ ---
        private const int MaxKillsPerBattle = 20;
        private const int MaxDamagePerBattle = 20000;
        private const int MinKills = 0;
        private const int MinDamage = 0;

        // --- Відсоток Free XP ---
        private const double FreeXpPercent = 0.05;

        public MatchesController(AppDbContext db)
        {
            _db = db;
        }

        private int CurrentUserId()
        {
            var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.Parse(idStr);
        }

        // -------------------------------
        // POST /matches/start
        // -------------------------------
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

        // ---------------------------------------------
        // POST /matches/{matchId}/end
        // ---------------------------------------------
        [HttpPost("{matchId:int}/end")]
        public IActionResult EndMatch(int matchId, [FromBody] EndMatchRequest req)
        {
            if (req == null || req.Participants == null || req.Participants.Count == 0)
                return BadRequest("Participants required.");

            var match = _db.Matches.FirstOrDefault(m => m.Id == matchId);
            if (match == null)
                return NotFound("Match not found.");

            if (match.EndedAt != null)
                return BadRequest("Match already ended.");

            bool alreadyHasParticipants = _db.MatchParticipants.Any(x => x.MatchId == matchId);
            if (alreadyHasParticipants)
                return Conflict("Results for this match were already submitted.");

            var duplicateUsers = req.Participants
                .GroupBy(p => p.UserId)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicateUsers.Count > 0)
                return BadRequest($"Duplicate participants: {string.Join(",", duplicateUsers)}");

            var userIds = req.Participants.Select(p => p.UserId).Distinct().ToList();
            var users = _db.Players.Where(u => userIds.Contains(u.Id)).ToDictionary(u => u.Id, u => u);

            var missingUsers = userIds.Where(id => !users.ContainsKey(id)).ToList();
            if (missingUsers.Count > 0)
                return BadRequest($"Unknown users: {string.Join(",", missingUsers)}");

            var teamToUserIds = req.Participants
                .GroupBy(p => p.Team)
                .ToDictionary(g => g.Key, g => g.Select(x => x.UserId).ToList());

            var teamAvgMmr = new Dictionary<int, double>();
            foreach (var kv in teamToUserIds)
            {
                var list = kv.Value.Where(id => users.ContainsKey(id)).Select(id => users[id].Mmr).ToList();
                teamAvgMmr[kv.Key] = list.Count > 0 ? list.Average() : 1000.0;
            }

            using var tx = _db.Database.BeginTransaction();

            match.EndedAt = DateTimeOffset.UtcNow;

            foreach (var raw in req.Participants)
            {
                var user = users[raw.UserId];

                int kills = Math.Clamp(raw.Kills, MinKills, MaxKillsPerBattle);
                int damage = Math.Clamp(raw.Damage, MinDamage, MaxDamagePerBattle);
                string result = NormalizeResult(raw.Result);

                // --- XP ---
                int xpBase = result switch
                {
                    "win" => XpWinBase,
                    "draw" => XpDrawBase,
                    _ => XpLoseBase
                };
                int xpFromDamage = (int)Math.Round(damage * XpPerDamage, MidpointRounding.AwayFromZero);
                int xpFromKills = kills * XpPerKill;
                int xpTotal = Math.Clamp(xpBase + xpFromDamage + xpFromKills, 0, XpCapPerBattle);

                // --- Bolts ---
                int bolts = BoltsBaseParticipation
                            + (result == "win" ? BoltsWinBonus : 0)
                            + (damage * BoltsPerDamage)
                            + (kills * BoltsPerKill);
                bolts = Math.Clamp(bolts, 0, BoltsCapPerBattle);

                // --- MMR ---
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

                bool existsForUser = _db.MatchParticipants.Any(x => x.MatchId == match.Id && x.UserId == raw.UserId);
                if (existsForUser)
                {
                    tx.Rollback();
                    return Conflict($"Results already submitted for user {raw.UserId} in this match.");
                }

                var mp = new MatchParticipant
                {
                    MatchId = match.Id,
                    UserId = raw.UserId,
                    WarriorId = raw.WarriorId,
                    Team = raw.Team,
                    Result = result,
                    Kills = kills,
                    Damage = damage,
                    XpEarned = xpTotal,
                    MmrDelta = mmrDelta
                };
                _db.MatchParticipants.Add(mp);

                // --- Оновлення прогресу ---
                user.Mmr += mmrDelta;
                user.Coins += bolts;
                user.FreeXp += (int)Math.Round(xpTotal * FreeXpPercent, MidpointRounding.AwayFromZero);

                // Знаходимо воїна гравця
                var uw = _db.UserWarriors.FirstOrDefault(v => v.UserId == raw.UserId && v.WarriorId == raw.WarriorId);
                if (uw != null)
                {
                    uw.Xp += xpTotal;
                }
            }

            _db.SaveChanges();
            tx.Commit();

            return Ok(new { ok = true });
        }

        private static string NormalizeResult(string input)
        {
            if (string.Equals(input, "win", StringComparison.OrdinalIgnoreCase)) return "win";
            if (string.Equals(input, "draw", StringComparison.OrdinalIgnoreCase)) return "draw";
            return "lose";
        }

        private static int FindEnemyTeam(List<int> teams, int myTeam)
        {
            foreach (var t in teams)
            {
                if (t != myTeam) return t;
            }
            return myTeam;
        }

        public class EndMatchRequest
        {
            public List<ParticipantInput> Participants { get; set; } = new();
        }

        public class ParticipantInput
        {
            public int UserId { get; set; }
            public int WarriorId { get; set; }
            public int Team { get; set; }
            public string Result { get; set; } = "lose";
            public int Kills { get; set; } = 0;
            public int Damage { get; set; } = 0;
        }

        // -------------------------------
        // GET /matches/{matchId}/participants
        // -------------------------------
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
                    Username = x.User != null ? x.User.Username : "",
                    WarriorId = x.WarriorId,
                    WarriorName = x.Warrior != null ? x.Warrior.Name : "",
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
    }
}
