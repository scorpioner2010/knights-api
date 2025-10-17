using KnightsApi.Models;

namespace KnightsApi.Data
{
    public static class SeedData
    {
        public static void Initialize(AppDbContext db)
        {
            // --- Cultures ---
            var yamato = db.Cultures.FirstOrDefault(f => f.Code == "yamato_clan")
                       ?? db.Cultures.Add(new Culture
                       {
                           Code = "yamato_clan",
                           Name = "Yamato Clan",
                           Description = "Ancient samurai of the eastern provinces — disciplined swordmasters, mounted lancers and a code of honour forged in centuries of ceremony and combat."
                       }).Entity;

            var nordic = db.Cultures.FirstOrDefault(f => f.Code == "nordic_clan")
                       ?? db.Cultures.Add(new Culture
                       {
                           Code = "nordic_clan",
                           Name = "Nordic Clan",
                           Description = "Hardy northern warriors — raiders and shield-bearers who value ferocity and endurance, famed for their axes, longships and battle-songs."
                       }).Entity;

            db.SaveChanges();

            // --- Maps ---
            if (!db.Maps.Any())
            {
                db.Maps.AddRange(
                    new Map { Code = "default_map", Name = "default map", Description = "test map" }
                );
                db.SaveChanges();
            }

            // ------- Helpers (idempotent) -------
            Warrior EnsureWarrior(Warrior v)
            {
                var existing = db.Warriors.FirstOrDefault(x => x.Code == v.Code);
                if (existing != null) return existing;
                db.Warriors.Add(v);
                db.SaveChanges();
                return v;
            }

            void EnsureLink(int predecessorId, int successorId, int requiredXp)
            {
                bool exists = db.WarriorResearchRequirements
                    .Any(r => r.PredecessorWarriorId == predecessorId && r.SuccessorWarriorId == successorId);
                if (exists) return;

                db.WarriorResearchRequirements.Add(new WarriorResearchRequirement
                {
                    PredecessorWarriorId = predecessorId,
                    SuccessorWarriorId = successorId,
                    RequiredXpOnPredecessor = requiredXp
                });
                db.SaveChanges();
            }

            // --- Yamato Clan (samurai) ---
            // L1 starter
            var samStarter = db.Warriors.FirstOrDefault(v => v.Code == "sam_l1_starter")
                             ?? EnsureWarrior(new Warrior
                             {
                                 Code = "sam_l1_starter",
                                 Name = "Yamato Squire",
                                 CultureId = yamato.Id,
                                 Branch = "infantry",
                                 Class = WarriorClass.Light,
                                 Level = 1,
                                 PurchaseCost = 0,

                                 HP = 110,
                                 Damage = 14,
                                 Accuracy = 0.92f,
                                 Speed = 6.5f,
                                 Acceleration = 3.8f,
                                 TraverseSpeed = 36f,
                                 Armor = 30,
                                 IsVisible = true
                             });

            // L2 options
            var samRonin = EnsureWarrior(new Warrior
            {
                Code = "sam_l2_ronin",
                Name = "Ronin Scout",
                CultureId = yamato.Id,
                Branch = "infantry",
                Class = WarriorClass.Light,
                Level = 2,
                PurchaseCost = 4500,
                HP = 130,
                Damage = 18,
                Accuracy = 0.94f,
                Speed = 7.4f,
                Acceleration = 4.2f,
                TraverseSpeed = 40f,
                Armor = 35,
                IsVisible = true
            });

            var samAshigaru = EnsureWarrior(new Warrior
            {
                Code = "sam_l2_spearman",
                Name = "Ashigaru Spearman",
                CultureId = yamato.Id,
                Branch = "infantry",
                Class = WarriorClass.Ranged,
                Level = 2,
                PurchaseCost = 9000,
                HP = 200,
                Damage = 22,
                Accuracy = 0.88f,
                Speed = 6.0f,
                Acceleration = 3.4f,
                TraverseSpeed = 34f,
                Armor = 70,
                IsVisible = true
            });

            var samKensei = EnsureWarrior(new Warrior
            {
                Code = "sam_l2_kensei",
                Name = "Kensei Vanguard",
                CultureId = yamato.Id,
                Branch = "mounted",
                Class = WarriorClass.Heavy,
                Level = 2,
                PurchaseCost = 15000,
                HP = 300,
                Damage = 32,
                Accuracy = 0.86f,
                Speed = 5.2f,
                Acceleration = 2.8f,
                TraverseSpeed = 28f,
                Armor = 110,
                IsVisible = true
            });

            // Links: starter -> each L2
            EnsureLink(samStarter.Id, samRonin.Id, requiredXp: 400);
            EnsureLink(samStarter.Id, samAshigaru.Id, requiredXp: 700);
            EnsureLink(samStarter.Id, samKensei.Id, requiredXp: 1000);

            // --- Nordic Clan (vikings) ---
            var vikStarter = db.Warriors.FirstOrDefault(v => v.Code == "vik_l1_starter")
                             ?? EnsureWarrior(new Warrior
                             {
                                 Code = "vik_l1_starter",
                                 Name = "Nordic Reaver",
                                 CultureId = nordic.Id,
                                 Branch = "infantry",
                                 Class = WarriorClass.Light,
                                 Level = 1,
                                 PurchaseCost = 0,

                                 HP = 130,
                                 Damage = 16,
                                 Accuracy = 0.86f,
                                 Speed = 6.2f,
                                 Acceleration = 3.6f,
                                 TraverseSpeed = 36f,
                                 Armor = 40,
                                 IsVisible = true
                             });

            var vikRaider = EnsureWarrior(new Warrior
            {
                Code = "vik_l2_raider",
                Name = "Raider Skirmisher",
                CultureId = nordic.Id,
                Branch = "infantry",
                Class = WarriorClass.Light,
                Level = 2,
                PurchaseCost = 4800,
                HP = 150,
                Damage = 20,
                Accuracy = 0.84f,
                Speed = 7.0f,
                Acceleration = 4.0f,
                TraverseSpeed = 38f,
                Armor = 45,
                IsVisible = true
            });

            var vikShield = EnsureWarrior(new Warrior
            {
                Code = "vik_l2_shieldbearer",
                Name = "Shieldbearer Guardian",
                CultureId = nordic.Id,
                Branch = "infantry",
                Class = WarriorClass.Ranged,
                Level = 2,
                PurchaseCost = 9200,
                HP = 240,
                Damage = 24,
                Accuracy = 0.80f,
                Speed = 5.6f,
                Acceleration = 3.0f,
                TraverseSpeed = 32f,
                Armor = 95,
                IsVisible = true
            });

            var vikBerserker = EnsureWarrior(new Warrior
            {
                Code = "vik_l2_berserker",
                Name = "Berserker Colossus",
                CultureId = nordic.Id,
                Branch = "infantry",
                Class = WarriorClass.Heavy,
                Level = 2,
                PurchaseCost = 15000,
                HP = 340,
                Damage = 36,
                Accuracy = 0.78f,
                Speed = 4.6f,
                Acceleration = 2.6f,
                TraverseSpeed = 26f,
                Armor = 130,
                IsVisible = true
            });

            // Links: starter -> each L2
            EnsureLink(vikStarter.Id, vikRaider.Id, requiredXp: 400);
            EnsureLink(vikStarter.Id, vikShield.Id, requiredXp: 700);
            EnsureLink(vikStarter.Id, vikBerserker.Id, requiredXp: 1000);

            // --- Players (demo) ---
            if (!db.Players.Any())
            {
                var user = new Player
                {
                    Username = "testknight",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("test123"),
                    IsAdmin = false,
                    Mmr = 1000,
                    FreeXp = 0,
                    Coins = 10000,
                    Gold = 0
                };
                db.Players.Add(user);
                db.SaveChanges();

                // assign a starter (samurai starter by default)
                var starter = db.Warriors.First(v => v.Code == "sam_l1_starter");
                db.UserWarriors.Add(new UserWarrior
                {
                    UserId = user.Id,
                    WarriorId = starter.Id,
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
                    Map = "jousting_field",
                    StartedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
                    EndedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
                };
                db.Matches.Add(m);
                db.SaveChanges();

                var u = db.Players.First();
                var starter = db.Warriors.First(v => v.Code == "sam_l1_starter");
                db.MatchParticipants.Add(new MatchParticipant
                {
                    MatchId = m.Id,
                    UserId = u.Id,
                    WarriorId = starter.Id,
                    Team = 1,
                    Result = "win",
                    Kills = 2,
                    Damage = 80,
                    XpEarned = 60,
                    MmrDelta = 10
                });
                db.SaveChanges();
            }
        }
    }
}
