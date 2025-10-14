using Microsoft.EntityFrameworkCore;
using WarOfMachines.Models;

namespace WarOfMachines.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Player> Players => Set<Player>();
        public DbSet<Unit> Units => Set<Unit>();
        public DbSet<UserUnit> UserUnits => Set<UserUnit>();
        public DbSet<Match> Matches => Set<Match>();
        public DbSet<MatchParticipant> MatchParticipants => Set<MatchParticipant>();
        public DbSet<Culture> Cultures => Set<Culture>();
        public DbSet<Map> Maps => Set<Map>();

        public DbSet<UnitResearchRequirement> VehicleResearchRequirements => Set<UnitResearchRequirement>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Culture
            modelBuilder.Entity<Culture>().HasIndex(f => f.Code).IsUnique();
            modelBuilder.Entity<Culture>().Property(f => f.Code).IsRequired();
            modelBuilder.Entity<Culture>().Property(f => f.Name).IsRequired();

            // Unit
            modelBuilder.Entity<Unit>().HasIndex(v => v.Code).IsUnique();
            modelBuilder.Entity<Unit>().Property(v => v.Code).IsRequired();
            modelBuilder.Entity<Unit>().Property(v => v.Name).IsRequired();
            modelBuilder.Entity<Unit>().HasOne(v => v.Culture).WithMany().HasForeignKey(v => v.CultureId).OnDelete(DeleteBehavior.Restrict);

            // VehicleResearchRequirement (предок -> нащадок; ResearchFrom прив'язане до Successor)
            modelBuilder.Entity<Unit>()
                .HasMany(v => v.ResearchFrom)
                .WithOne(r => r.Successor)
                .HasForeignKey(r => r.SuccessorUnitId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UnitResearchRequirement>()
                .HasOne(r => r.Predecessor)
                .WithMany()
                .HasForeignKey(r => r.PredecessorUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<UnitResearchRequirement>()
                .HasIndex(r => new { PredecessorVehicleId = r.PredecessorUnitId, SuccessorVehicleId = r.SuccessorUnitId })
                .IsUnique();

            // UserVehicle
            modelBuilder.Entity<UserUnit>()
                .HasIndex(uv => new { uv.UserId, VehicleId = uv.UnitId }).IsUnique();

            modelBuilder.Entity<UserUnit>()
                .HasIndex(nameof(UserUnit.UserId), nameof(UserUnit.IsActive))
                .HasFilter("\"IsActive\" = TRUE")
                .IsUnique();

            modelBuilder.Entity<UserUnit>()
                .HasOne(uv => uv.User)
                .WithMany()
                .HasForeignKey(uv => uv.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserUnit>()
                .HasOne(uv => uv.Unit)
                .WithMany()
                .HasForeignKey(uv => uv.UnitId)
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
                .HasOne(mp => mp.Unit)
                .WithMany()
                .HasForeignKey(mp => mp.UnitId)
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
