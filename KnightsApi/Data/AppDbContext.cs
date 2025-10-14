using Microsoft.EntityFrameworkCore;
using KnightsApi.Models;

namespace KnightsApi.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Player> Players => Set<Player>();
        public DbSet<Warrior> Warriors => Set<Warrior>();
        public DbSet<UserWarrior> UserWarriors => Set<UserWarrior>();
        public DbSet<Match> Matches => Set<Match>();
        public DbSet<MatchParticipant> MatchParticipants => Set<MatchParticipant>();
        public DbSet<Culture> Cultures => Set<Culture>();
        public DbSet<Map> Maps => Set<Map>();

        public DbSet<WarriorResearchRequirement> WarriorResearchRequirements => Set<WarriorResearchRequirement>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Player
            modelBuilder.Entity<Player>()
                .HasIndex(p => p.Username)
                .IsUnique();

            // Culture
            modelBuilder.Entity<Culture>().HasIndex(f => f.Code).IsUnique();
            modelBuilder.Entity<Culture>().Property(f => f.Code).IsRequired();
            modelBuilder.Entity<Culture>().Property(f => f.Name).IsRequired();

            // Warrior
            modelBuilder.Entity<Warrior>().HasIndex(v => v.Code).IsUnique();
            modelBuilder.Entity<Warrior>().Property(v => v.Code).IsRequired();
            modelBuilder.Entity<Warrior>().Property(v => v.Name).IsRequired();
            modelBuilder.Entity<Warrior>().HasOne(v => v.Culture).WithMany().HasForeignKey(v => v.CultureId).OnDelete(DeleteBehavior.Restrict);

            // WarriorResearchRequirement (предок -> нащадок; ResearchFrom прив'язане до Successor)
            modelBuilder.Entity<Warrior>()
                .HasMany(v => v.ResearchFrom)
                .WithOne(r => r.Successor)
                .HasForeignKey(r => r.SuccessorWarriorId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<WarriorResearchRequirement>()
                .HasOne(r => r.Predecessor)
                .WithMany()
                .HasForeignKey(r => r.PredecessorWarriorId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<WarriorResearchRequirement>()
                .HasIndex(r => new { PredecessorWarriorId = r.PredecessorWarriorId, SuccessorWarriorId = r.SuccessorWarriorId })
                .IsUnique();

            // UserWarrior
            modelBuilder.Entity<UserWarrior>()
                .HasIndex(uv => new { uv.UserId, WarriorId = uv.WarriorId }).IsUnique();

            modelBuilder.Entity<UserWarrior>()
                .HasIndex(nameof(UserWarrior.UserId), nameof(UserWarrior.IsActive))
                .HasFilter("\"IsActive\" = TRUE")
                .IsUnique();

            modelBuilder.Entity<UserWarrior>()
                .HasOne(uv => uv.User)
                .WithMany()
                .HasForeignKey(uv => uv.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserWarrior>()
                .HasOne(uv => uv.Warrior)
                .WithMany()
                .HasForeignKey(uv => uv.WarriorId)
                .OnDelete(DeleteBehavior.Restrict);

            // MatchParticipant
            modelBuilder.Entity<MatchParticipant>()
                .HasOne(mp => mp.Match)
                .WithMany()
                .HasForeignKey(mp => mp.MatchId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<MatchParticipant>()
                .HasOne(mp => mp.User)
                .WithMany()
                .HasForeignKey(mp => mp.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<MatchParticipant>()
                .HasOne(mp => mp.Warrior)
                .WithMany()
                .HasForeignKey(mp => mp.WarriorId)
                .OnDelete(DeleteBehavior.Restrict);

            // Map
            modelBuilder.Entity<Map>()
                .HasIndex(m => m.Code)
                .IsUnique();

            modelBuilder.Entity<Map>()
                .Property(m => m.Code).IsRequired();
            modelBuilder.Entity<Map>()
                .Property(m => m.Name).IsRequired();
        }
    }
}
