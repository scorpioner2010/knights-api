using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace KnightsApi.Migrations
{
    /// <inheritdoc />
    public partial class AddUserUnlockedWarriors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserUnlockedWarriors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    WarriorId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserUnlockedWarriors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserUnlockedWarriors_Players_UserId",
                        column: x => x.UserId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserUnlockedWarriors_Warriors_WarriorId",
                        column: x => x.WarriorId,
                        principalTable: "Warriors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Players_Username",
                table: "Players",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserUnlockedWarriors_UserId_WarriorId",
                table: "UserUnlockedWarriors",
                columns: new[] { "UserId", "WarriorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserUnlockedWarriors_WarriorId",
                table: "UserUnlockedWarriors",
                column: "WarriorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserUnlockedWarriors");

            migrationBuilder.DropIndex(
                name: "IX_Players_Username",
                table: "Players");
        }
    }
}
