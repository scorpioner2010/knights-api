using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace WarOfMachines.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Factions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Factions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Maps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Maps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Matches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Map = table.Column<string>(type: "text", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Matches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Players",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Username = table.Column<string>(type: "text", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    IsAdmin = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Mmr = table.Column<int>(type: "integer", nullable: false),
                    Bolts = table.Column<int>(type: "integer", nullable: false),
                    Adamant = table.Column<int>(type: "integer", nullable: false),
                    FreeXp = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Players", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Vehicles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    FactionId = table.Column<int>(type: "integer", nullable: false),
                    Branch = table.Column<string>(type: "text", nullable: false),
                    Class = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    PurchaseCost = table.Column<int>(type: "integer", nullable: false),
                    HP = table.Column<int>(type: "integer", nullable: false),
                    Damage = table.Column<int>(type: "integer", nullable: false),
                    Penetration = table.Column<int>(type: "integer", nullable: false),
                    ReloadTime = table.Column<float>(type: "real", nullable: false),
                    Accuracy = table.Column<float>(type: "real", nullable: false),
                    AimTime = table.Column<float>(type: "real", nullable: false),
                    Speed = table.Column<float>(type: "real", nullable: false),
                    Acceleration = table.Column<float>(type: "real", nullable: false),
                    TraverseSpeed = table.Column<float>(type: "real", nullable: false),
                    TurretTraverseSpeed = table.Column<float>(type: "real", nullable: false),
                    TurretArmorFront = table.Column<int>(type: "integer", nullable: false),
                    TurretArmorSide = table.Column<int>(type: "integer", nullable: false),
                    TurretArmorRear = table.Column<int>(type: "integer", nullable: false),
                    HullArmorFront = table.Column<int>(type: "integer", nullable: false),
                    HullArmorSide = table.Column<int>(type: "integer", nullable: false),
                    HullArmorRear = table.Column<int>(type: "integer", nullable: false),
                    IsVisible = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vehicles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Vehicles_Factions_FactionId",
                        column: x => x.FactionId,
                        principalTable: "Factions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MatchParticipants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MatchId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    VehicleId = table.Column<int>(type: "integer", nullable: false),
                    Team = table.Column<int>(type: "integer", nullable: false),
                    Result = table.Column<string>(type: "text", nullable: false),
                    Kills = table.Column<int>(type: "integer", nullable: false),
                    Damage = table.Column<int>(type: "integer", nullable: false),
                    XpEarned = table.Column<int>(type: "integer", nullable: false),
                    MmrDelta = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchParticipants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatchParticipants_Matches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MatchParticipants_Players_UserId",
                        column: x => x.UserId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MatchParticipants_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserVehicles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    VehicleId = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Xp = table.Column<int>(type: "integer", nullable: false),
                    PlayerId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserVehicles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserVehicles_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UserVehicles_Players_UserId",
                        column: x => x.UserId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserVehicles_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VehicleResearchRequirements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PredecessorVehicleId = table.Column<int>(type: "integer", nullable: false),
                    SuccessorVehicleId = table.Column<int>(type: "integer", nullable: false),
                    RequiredXpOnPredecessor = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleResearchRequirements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VehicleResearchRequirements_Vehicles_PredecessorVehicleId",
                        column: x => x.PredecessorVehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VehicleResearchRequirements_Vehicles_SuccessorVehicleId",
                        column: x => x.SuccessorVehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Factions_Code",
                table: "Factions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Maps_Code",
                table: "Maps",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MatchParticipants_MatchId",
                table: "MatchParticipants",
                column: "MatchId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchParticipants_UserId",
                table: "MatchParticipants",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchParticipants_VehicleId",
                table: "MatchParticipants",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_UserVehicles_PlayerId",
                table: "UserVehicles",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_UserVehicles_UserId_IsActive",
                table: "UserVehicles",
                columns: new[] { "UserId", "IsActive" },
                unique: true,
                filter: "\"IsActive\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_UserVehicles_UserId_VehicleId",
                table: "UserVehicles",
                columns: new[] { "UserId", "VehicleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserVehicles_VehicleId",
                table: "UserVehicles",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleResearchRequirements_PredecessorVehicleId_SuccessorV~",
                table: "VehicleResearchRequirements",
                columns: new[] { "PredecessorVehicleId", "SuccessorVehicleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VehicleResearchRequirements_SuccessorVehicleId",
                table: "VehicleResearchRequirements",
                column: "SuccessorVehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_Code",
                table: "Vehicles",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_FactionId",
                table: "Vehicles",
                column: "FactionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Maps");

            migrationBuilder.DropTable(
                name: "MatchParticipants");

            migrationBuilder.DropTable(
                name: "UserVehicles");

            migrationBuilder.DropTable(
                name: "VehicleResearchRequirements");

            migrationBuilder.DropTable(
                name: "Matches");

            migrationBuilder.DropTable(
                name: "Players");

            migrationBuilder.DropTable(
                name: "Vehicles");

            migrationBuilder.DropTable(
                name: "Factions");
        }
    }
}
