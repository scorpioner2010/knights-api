using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WarOfMachines.Data;
using WarOfMachines.Models;

namespace WarOfMachines.Controllers
{
    [ApiController]
    [Route("user-vehicles")]
    [Authorize]
    public class UserUnitsController : ControllerBase
    {
        private readonly AppDbContext _db;

        public UserUnitsController(AppDbContext db)
        {
            _db = db;
        }

        private int CurrentUserId()
        {
            var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.Parse(idStr);
        }

        // ========================================
        // GET /user-vehicles/me
        // ========================================
        [HttpGet("me")]
        public IActionResult GetMyUnits()
        {
            int uid = CurrentUserId();

            var list = _db.UserUnits
                .Where(x => x.UserId == uid)
                .Include(x => x.Unit)
                .Select(x => new UserVehicleDto
                {
                    Id = x.Id,
                    VehicleId = x.UnitId,
                    VehicleCode = x.Unit != null ? x.Unit.Code : string.Empty,
                    VehicleName = x.Unit != null ? x.Unit.Name : string.Empty,
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
                Vehicles = list
            });
        }

        // ========================================
        // PUT /user-vehicles/me/active/{vehicleId}
        // Уникаємо 23505 на IX_UserVehicles_UserId_IsActive:
        // знімаємо старий TRUE -> SaveChanges -> виставляємо новий TRUE -> SaveChanges
        // ========================================
        [HttpPut("me/active/{unitId:int}")]
        public IActionResult SetActive(int unitId)
        {
            int uid = CurrentUserId();

            using var tx = _db.Database.BeginTransaction();

            var owned = _db.UserUnits
                .Where(x => x.UserId == uid)
                .ToList();

            var target = owned.FirstOrDefault(x => x.UnitId == unitId);
            if (target == null)
                return NotFound("User does not own this vehicle.");

            // Якщо вже активний — все ок
            if (target.IsActive)
            {
                tx.Commit();
                return Ok(new { ok = true, activeUnitId = unitId });
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
            return Ok(new { ok = true, activeUnitId = unitId });
        }

        // ========================================
        // POST /user-vehicles/me/buy/{code}
        // Покупка за кодом каталогу Vehicle
        // ========================================
        [HttpPost("me/buy/{code}")]
        public IActionResult BuyUnit(string code)
        {
            int uid = CurrentUserId();

            var player = _db.Players.FirstOrDefault(p => p.Id == uid);
            if (player == null)
                return NotFound("Player not found.");

            var vehicle = _db.Units.FirstOrDefault(v => v.Code == code);
            if (vehicle == null)
                return NotFound("Vehicle code not found.");

            if (_db.UserUnits.Any(x => x.UserId == uid && x.UnitId == vehicle.Id))
                return Conflict("Vehicle already owned.");

            if (player.Coins < vehicle.PurchaseCost)
                return BadRequest("Not enough Bolts.");

            player.Coins -= vehicle.PurchaseCost;

            var uv = new UserUnit
            {
                UserId = uid,
                UnitId = vehicle.Id,
                Xp = 0,
                IsActive = false
            };

            _db.UserUnits.Add(uv);
            _db.SaveChanges();

            return Ok(new
            {
                ok = true,
                userVehicleId = uv.Id,
                vehicleId = uv.UnitId,
                newBolts = player.Coins
            });
        }

        // ========================================
        // POST /user-vehicles/me/sell/{vehicleId}
        // Продає техніку за 50% від PurchaseCost (безпечний порядок апдейтів)
        // ========================================
        [HttpPost("me/sell/{vehicleId:int}")]
        public IActionResult Sell(int vehicleId)
        {
            int uid = CurrentUserId();

            using var tx = _db.Database.BeginTransaction();

            var owned = _db.UserUnits
                .Where(x => x.UserId == uid)
                .Include(x => x.Unit)
                .ToList();

            if (owned.Count <= 1)
                return BadRequest("Cannot sell your last remaining vehicle.");

            var uv = owned.FirstOrDefault(x => x.UnitId == vehicleId);
            if (uv == null)
                return NotFound("Vehicle not found.");

            if (uv.Unit == null)
                return BadRequest("Vehicle data is missing.");

            var player = _db.Players.FirstOrDefault(p => p.Id == uid);
            if (player == null)
                return NotFound("Player not found.");

            int refund = Math.Max(0, uv.Unit.PurchaseCost / 2);

            // Якщо активний — спершу зняти активність, зберегти
            bool wasActive = uv.IsActive;
            if (wasActive)
            {
                uv.IsActive = false;
                _db.SaveChanges(); // зняли TRUE, індекс щасливий
            }

            // Видалити та повернути болти
            _db.UserUnits.Remove(uv);
            player.Coins += refund;
            _db.SaveChanges();

            // Якщо продавали активного — призначити інший активним і зберегти
            if (wasActive)
            {
                var replacement = _db.UserUnits
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
                soldVehicleId = vehicleId,
                refundBolts = refund,
                newBolts = player.Coins
            });
        }

        // ========================================
        // POST /user-vehicles/me/add-by-code/{code}
        // (dev/debug) Додає безкоштовно
        // ========================================
        [HttpPost("me/add-by-code/{code}")]
        public IActionResult AddByCode(string code)
        {
            int uid = CurrentUserId();

            var vehicle = _db.Units.FirstOrDefault(v => v.Code == code);
            if (vehicle == null)
                return NotFound("Vehicle code not found.");

            bool already = _db.UserUnits.Any(x => x.UserId == uid && x.UnitId == vehicle.Id);
            if (already)
                return Conflict("Vehicle already owned.");

            var uv = new UserUnit
            {
                UserId = uid,
                UnitId = vehicle.Id,
                Xp = 0,
                IsActive = false
            };

            _db.UserUnits.Add(uv);
            _db.SaveChanges();

            return Ok(new { ok = true, userVehicleId = uv.Id, vehicleId = uv.UnitId });
        }

        // ========================================
        // DELETE /user-vehicles/me/{vehicleId}
        // Жорстке видалення (з урахуванням активної) — безпечний порядок
        // ========================================
        [HttpDelete("me/{vehicleId:int}")]
        public IActionResult Remove(int vehicleId)
        {
            int uid = CurrentUserId();

            using var tx = _db.Database.BeginTransaction();

            var owned = _db.UserUnits
                .Where(x => x.UserId == uid)
                .Include(x => x.Unit)
                .ToList();

            if (owned.Count <= 1)
                return BadRequest("Cannot sell your last remaining vehicle.");

            var uv = owned.FirstOrDefault(x => x.UnitId == vehicleId);
            if (uv == null)
                return NotFound("Vehicle not found.");

            bool wasActive = uv.IsActive;

            if (wasActive)
            {
                uv.IsActive = false;
                _db.SaveChanges(); // зняти TRUE до будь-яких інших рухів
            }

            _db.UserUnits.Remove(uv);
            _db.SaveChanges();

            if (wasActive)
            {
                var replacement = _db.UserUnits
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
            return Ok(new { ok = true, soldVehicleId = vehicleId });
        }

        // ========================================
        // GET /user-vehicles/xp
        // ========================================
        [HttpGet("xp")]
        public IActionResult GetMyVehiclesXp()
        {
            int userId = CurrentUserId();

            var list = _db.UserUnits
                .Where(x => x.UserId == userId)
                .Include(x => x.Unit)
                .Select(x => new
                {
                    VehicleId = x.UnitId,
                    VehicleName = x.Unit != null ? x.Unit.Name : "",
                    x.Xp,
                    x.IsActive
                })
                .ToList();

            var freeXp = _db.Players
                .Where(p => p.Id == userId)
                .Select(p => p.FreeXp)
                .FirstOrDefault();

            return Ok(new
            {
                FreeXp = freeXp,
                Vehicles = list
            });
        }

        // ========================================
        // POST /user-vehicles/{vehicleId}/convert-freexp
        // ========================================
        [HttpPost("{vehicleId:int}/convert-freexp")]
        public IActionResult ConvertFreeXpToVehicle(int vehicleId, [FromBody] ConvertFreeXpRequest req)
        {
            int userId = CurrentUserId();

            if (req.Amount <= 0)
                return BadRequest("Amount must be positive.");

            var player = _db.Players.FirstOrDefault(p => p.Id == userId);
            if (player == null)
                return NotFound("Player not found.");

            if (player.FreeXp < req.Amount)
                return BadRequest("Not enough Free XP.");

            var uv = _db.UserUnits
                .Include(x => x.Unit)
                .FirstOrDefault(x => x.UserId == userId && x.UnitId == vehicleId);

            if (uv == null)
                return NotFound("Vehicle not found or not owned.");

            player.FreeXp -= req.Amount;
            uv.Xp += req.Amount;

            _db.SaveChanges();

            return Ok(new
            {
                ok = true,
                vehicleId = uv.UnitId,
                vehicleName = uv.Unit?.Name,
                addedXp = req.Amount,
                newVehicleXp = uv.Xp,
                remainingFreeXp = player.FreeXp
            });
        }

        // ========================================
        // DTOs
        // ========================================
        public class UserVehicleDto
        {
            public int Id { get; set; }
            public int VehicleId { get; set; }
            public string VehicleCode { get; set; } = string.Empty;
            public string VehicleName { get; set; } = string.Empty;
            public int Xp { get; set; }
            public bool IsActive { get; set; }
        }

        public class ConvertFreeXpRequest
        {
            public int Amount { get; set; }
        }
    }
}
