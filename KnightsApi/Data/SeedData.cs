using System;
using System.Linq;
using WarOfMachines.Models;

namespace WarOfMachines.Data
{
    public static class SeedData
    {
        public static void Initialize(AppDbContext db)
        {
            // --- Factions ---
            var iron = db.Cultures.FirstOrDefault(f => f.Code == "iron_alliance")
                       ?? db.Cultures.Add(new Culture
                       {
                           Code = "iron_alliance",
                           Name = "Iron Alliance",
                           Description = "Veteran pilots in armored warframes forged for frontal assaults."
                       }).Entity;

            var nova = db.Cultures.FirstOrDefault(f => f.Code == "nova_syndicate")
                       ?? db.Cultures.Add(new Culture
                       {
                           Code = "nova_syndicate",
                           Name = "Nova Syndicate",
                           Description = "A covert network fielding agile, high-tech combat machines."
                       }).Entity;

            db.SaveChanges();

            // --- Maps ---
            if (!db.Maps.Any())
            {
                db.Maps.AddRange(
                    new Map { Code = "demo_map", Name = "Demo Yard", Description = "Training pit for rookie pilots." },
                    new Map { Code = "steel_arena", Name = "Steel Arena", Description = "Circular proving grounds with scattered cover." }
                );
                db.SaveChanges();
            }

            // ------- Локальні хелпери (ідемпотентні) -------
            Unit EnsureUnit(Unit v)
            {
                var existing = db.Units.FirstOrDefault(x => x.Code == v.Code);
                if (existing != null) return existing;
                db.Units.Add(v);
                db.SaveChanges();
                return v;
            }

            void EnsureLink(int predecessorId, int successorId, int requiredXp)
            {
                bool exists = db.VehicleResearchRequirements
                    .Any(r => r.PredecessorUnitId == predecessorId && r.SuccessorUnitId == successorId);
                if (exists) return;

                db.VehicleResearchRequirements.Add(new UnitResearchRequirement
                {
                    PredecessorUnitId = predecessorId,
                    SuccessorUnitId = successorId,
                    RequiredXpOnPredecessor = requiredXp
                });
                db.SaveChanges();
            }

            // --- Vehicles (ідемпотентно; без if (!db.Vehicles.Any())) ---

            // Iron Alliance (tracked) — L1 + три L2
            var iaStarter = db.Units.FirstOrDefault(v => v.Code == "ia_l1_starter")
                            ?? EnsureUnit(new Unit
                            {
                                Code = "ia_l1_starter",
                                Name = "IA Skirmisher",
                                CultureId = iron.Id,
                                Branch = "tracked",
                                Class = UnitClass.Light,
                                Level = 1,
                                PurchaseCost = 0,

                                HP = 120, Damage = 12,
                               Accuracy = 0.85f,
                                Speed = 6.0f, Acceleration = 3.5f, TraverseSpeed = 35f, 
                                Armor = 50,
                                IsVisible = true
                            });

            var iaL2Scout = EnsureUnit(new Unit
            {
                Code = "ia_l2_scout",
                Name = "IA Strider",
                CultureId = iron.Id,
                Branch = "tracked",
                Class = UnitClass.Light,
                Level = 2,
                PurchaseCost = 5000,
                HP = 150, Damage = 16,
               Accuracy = 0.87f,
                Speed = 7.0f, Acceleration = 3.8f, TraverseSpeed = 38f,
                Armor = 55, 
                IsVisible = true
            });

            var iaL2Guardian = EnsureUnit(new Unit
            {
                Code = "ia_l2_guardian",
                Name = "IA Bulwark",
                CultureId = iron.Id,
                Branch = "tracked",
                Class = UnitClass.Ranged,
                Level = 2,
                PurchaseCost = 9000,
                HP = 220, Damage = 22,
              Accuracy = 0.84f,
                Speed = 5.8f, Acceleration = 3.0f, TraverseSpeed = 32f,
                Armor = 85, 
                IsVisible = true
            });

            var iaL2Colossus = EnsureUnit(new Unit
            {
                Code = "ia_l2_colossus",
                Name = "IA Juggernaut",
                CultureId = iron.Id,
                Branch = "tracked",
                Class = UnitClass.Heavy,
                Level = 2,
                PurchaseCost = 15000,
                HP = 320, Damage = 34,
               Accuracy = 0.80f,
                Speed = 4.8f, Acceleration = 2.4f, TraverseSpeed = 26f,
                Armor = 120,
                IsVisible = true
            });

            // Links: L1 -> (Scout|Guardian|Colossus)
            EnsureLink(iaStarter.Id, iaL2Scout.Id,    requiredXp: 400);
            EnsureLink(iaStarter.Id, iaL2Guardian.Id, requiredXp: 700);
            EnsureLink(iaStarter.Id, iaL2Colossus.Id, requiredXp: 1000);

            // Nova Syndicate (biped) — L1 + три L2
            var nvStarter = db.Units.FirstOrDefault(v => v.Code == "nv_l1_starter")
                            ?? EnsureUnit(new Unit
                            {
                                Code = "nv_l1_starter",
                                Name = "Nova Wisp",
                                CultureId = nova.Id,
                                Branch = "biped",
                                Class = UnitClass.Light,
                                Level = 1,
                                PurchaseCost = 0,

                                HP = 100, Damage = 14,
                              Accuracy = 0.86f,
                                Speed = 7.0f, Acceleration = 4.0f, TraverseSpeed = 38f,
                                Armor = 40,
                                IsVisible = true
                            });

            var nvL2Scout = EnsureUnit(new Unit
            {
                Code = "nv_l2_scout",
                Name = "Nova Flicker",
                CultureId = nova.Id,
                Branch = "biped",
                Class = UnitClass.Light,
                Level = 2,
                PurchaseCost = 5000,
                HP = 130, Damage = 18,
              Accuracy = 0.88f,
                Speed = 7.6f, Acceleration = 4.4f, TraverseSpeed = 40f,
                Armor = 44,
                IsVisible = true
            });

            var nvL2Guardian = EnsureUnit(new Unit
            {
                Code = "nv_l2_guardian",
                Name = "Nova Aegis",
                CultureId = nova.Id,
                Branch = "biped",
                Class = UnitClass.Ranged,
                Level = 2,
                PurchaseCost = 9000,
                HP = 200, Damage = 24,
              Accuracy = 0.85f,
                Speed = 6.2f, Acceleration = 3.6f, TraverseSpeed = 34f,
                Armor = 70,
                IsVisible = true
            });

            var nvL2Colossus = EnsureUnit(new Unit
            {
                Code = "nv_l2_colossus",
                Name = "Nova Titan",
                CultureId = nova.Id,
                Branch = "biped",
                Class = UnitClass.Heavy,
                Level = 2,
                PurchaseCost = 15000,
                HP = 300, Damage = 36,
              Accuracy = 0.82f,
                Speed = 5.0f, Acceleration = 2.8f, TraverseSpeed = 28f,
                Armor = 105,
                IsVisible = true
            });

            // Links: L1 -> (Scout|Guardian|Colossus)
            EnsureLink(nvStarter.Id, nvL2Scout.Id,    requiredXp: 400);
            EnsureLink(nvStarter.Id, nvL2Guardian.Id, requiredXp: 700);
            EnsureLink(nvStarter.Id, nvL2Colossus.Id, requiredXp: 1000);

            // --- Players ---
            if (!db.Players.Any())
            {
                var user = new Player
                {
                    Username = "testuser",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("test123"),
                    IsAdmin = false,
                    Mmr = 1000,
                    FreeXp = 0,
                    Coins = 10000,
                    Gold = 0
                };
                db.Players.Add(user);
                db.SaveChanges();

                // Starter robot assignment
                var starter = db.Units.First(v => v.Code == "ia_l1_starter");
                db.UserUnits.Add(new UserUnit
                {
                    UserId = user.Id,
                    UnitId = starter.Id,
                    IsActive = true,
                    Xp = 0
                });
                db.SaveChanges();
            }

            // --- Demo Match ---
            if (!db.Matches.Any())
            {
                var m = new Match
                {
                    Map = "demo_map",
                    StartedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
                    EndedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
                };
                db.Matches.Add(m);
                db.SaveChanges();

                var u = db.Players.First();
                var starter = db.Units.First(v => v.Code == "ia_l1_starter");
                db.MatchParticipants.Add(new MatchParticipant
                {
                    MatchId = m.Id,
                    UserId = u.Id,
                    UnitId = starter.Id,
                    Team = 1,
                    Result = "win",
                    Kills = 2,
                    Damage = 120,
                    XpEarned = 50,
                    MmrDelta = 10
                });
                db.SaveChanges();
            }
        }
    }
}
