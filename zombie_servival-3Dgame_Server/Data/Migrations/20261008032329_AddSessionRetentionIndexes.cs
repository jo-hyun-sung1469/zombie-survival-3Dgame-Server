using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace zombie_survival_3Dgame_Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionRetentionIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_SurvivalGameSessions_CompletedAtUtc",
                table: "SurvivalGameSessions",
                column: "CompletedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_SurvivalGameSessions_StartedAtUtc",
                table: "SurvivalGameSessions",
                column: "StartedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SurvivalGameSessions_CompletedAtUtc",
                table: "SurvivalGameSessions");

            migrationBuilder.DropIndex(
                name: "IX_SurvivalGameSessions_StartedAtUtc",
                table: "SurvivalGameSessions");
        }
    }
}
