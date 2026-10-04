using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace zombie_survival_3Dgame_Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSurvivalGameSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SurvivalGameSessions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PlayerId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ActivePlayerId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    SurvivalTimeSeconds = table.Column<float>(type: "float", nullable: false),
                    ClearWave = table.Column<int>(type: "int", nullable: false),
                    KillZombies = table.Column<int>(type: "int", nullable: false),
                    AwardedGold = table.Column<int>(type: "int", nullable: true),
                    ClaimedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SurvivalGameSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SurvivalGameSessions_Users_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_SurvivalGameSessions_ActivePlayerId",
                table: "SurvivalGameSessions",
                column: "ActivePlayerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SurvivalGameSessions_PlayerId_StartedAtUtc",
                table: "SurvivalGameSessions",
                columns: new[] { "PlayerId", "StartedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SurvivalGameSessions");
        }
    }
}
