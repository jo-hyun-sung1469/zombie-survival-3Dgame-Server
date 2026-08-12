using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace zombie_survival_3Dgame_Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddConcurrencyVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "PlayerSaveData",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "AuthVerificationCodes",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Version",
                table: "PlayerSaveData");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "AuthVerificationCodes");
        }
    }
}
