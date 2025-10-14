using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using WarOfMachines.Data;
using WarOfMachines.Models;

namespace WarOfMachines.Controllers
{
    [ApiController]
    [Route("auth")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ILogger<AuthController> _logger;
        private readonly byte[] _jwtKey;

        public AuthController(AppDbContext db, ILogger<AuthController> logger, IConfiguration cfg)
        {
            _db = db;
            _logger = logger;
            _jwtKey = Encoding.UTF8.GetBytes(cfg["Jwt:Key"] ?? "super_secret_key_change_me_please_32+");
        }

        public class RegisterRequest
        {
            public string Username { get; set; } = "";
            public string Password { get; set; } = "";
        }

        public class LoginRequest
        {
            public string Username { get; set; } = "";
            public string Password { get; set; } = "";
        }

        public class TokenResponse
        {
            public string Token { get; set; } = "";
        }

        [HttpPost("register")]
        [AllowAnonymous]
        public IActionResult Register([FromBody] RegisterRequest request)
        {
            _logger.LogInformation("Register: {Request}", request);

            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
                return BadRequest("Username/password required.");

            bool exists = _db.Players.Any(p => p.Username == request.Username);
            if (exists) return Conflict("Username already taken.");

            var player = new Player
            {
                Username = request.Username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                IsAdmin = false,
                CreatedAt = DateTimeOffset.UtcNow,
                Mmr = 0,
                FreeXp = 0,
                Coins = 10000,
                Gold = 0
            };

            _db.Players.Add(player);
            _db.SaveChanges();

            EnsureStarterVehicle(player.Id);

            var token = IssueJwt(player);
            return Ok(new TokenResponse { Token = token });
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            _logger.LogInformation("Login attempt: {Username}", request.Username);

            var user = _db.Players.FirstOrDefault(p => p.Username == request.Username);
            if (user == null) return Unauthorized("Invalid username or password.");

            bool ok = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
            if (!ok) return Unauthorized("Invalid username or password.");

            // auto-heal для старих юзерів без стартової техніки
            EnsureStarterVehicle(user.Id);

            var token = IssueJwt(user);
            return Ok(new TokenResponse { Token = token });
        }

        // ===== helpers =====

        private void EnsureStarterVehicle(int userId)
        {
            bool hasAny = _db.UserUnits.Any(x => x.UserId == userId);
            if (hasAny) return;

            // 1) спробувати конкретний відомий код
            Unit? starter = _db.Units.FirstOrDefault(v => v.Code == "ia_l1_starter");

            // 2) якщо ні — взяти будь-який видимий 1 рівня, або просто найдешевший
            starter ??= _db.Units
                .OrderBy(v => v.Level)        // Level 1 насамперед
                .ThenByDescending(v => v.IsVisible)
                .ThenBy(v => v.PurchaseCost)
                .FirstOrDefault();

            if (starter == null) return; // у БД нема техніки — нічого не робимо

            var uv = new UserUnit
            {
                UserId = userId,
                UnitId = starter.Id,
                IsActive = true,
                Xp = 0
            };
            _db.UserUnits.Add(uv);
            _db.SaveChanges();
        }

        private string IssueJwt(Player user)
        {
            var handler = new JwtSecurityTokenHandler();

            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.IsAdmin ? "admin" : "user"),
            });

            var creds = new SigningCredentials(new SymmetricSecurityKey(_jwtKey), SecurityAlgorithms.HmacSha256);

            var token = handler.CreateJwtSecurityToken(
                subject: identity,
                notBefore: DateTime.UtcNow,
                expires: DateTime.UtcNow.AddDays(7),
                signingCredentials: creds);

            return handler.WriteToken(token);
        }
    }
}
