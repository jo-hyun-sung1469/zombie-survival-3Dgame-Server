using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace zombie_survival_3Dgame_Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AuthVerificationCodes",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Email = table.Column<string>(type: "varchar(254)", maxLength: 254, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CodeHash = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    VerifiedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ConsumedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthVerificationCodes", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "FirearmDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DisplayName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Category = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Rarity = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    GachaProbability = table.Column<double>(type: "double", nullable: false),
                    Damage = table.Column<int>(type: "int", nullable: false),
                    FireRate = table.Column<double>(type: "double", nullable: false),
                    MagazineSize = table.Column<int>(type: "int", nullable: false),
                    ReloadTimeSeconds = table.Column<double>(type: "double", nullable: false),
                    RangeMeters = table.Column<double>(type: "double", nullable: false),
                    HeadshotDamageMultiplier = table.Column<double>(type: "double", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FirearmDefinitions", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PlayerSaveData",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    PlayerId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Gold = table.Column<int>(type: "int", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerSaveData", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UserName = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Email = table.Column<string>(type: "varchar(254)", maxLength: 254, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PasswordHash = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Role = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PlayerStatUpgradeStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    PlayerSaveDataId = table.Column<int>(type: "int", nullable: false),
                    StatName = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UpgradeLevel = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerStatUpgradeStates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerStatUpgradeStates_PlayerSaveData_PlayerSaveDataId",
                        column: x => x.PlayerSaveDataId,
                        principalTable: "PlayerSaveData",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PlayerWeaponStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    PlayerSaveDataId = table.Column<int>(type: "int", nullable: false),
                    FirearmDefinitionId = table.Column<int>(type: "int", nullable: false),
                    WeaponName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsOwned = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    WeaponLevel = table.Column<int>(type: "int", nullable: false),
                    Damage = table.Column<int>(type: "int", nullable: false),
                    FireRate = table.Column<double>(type: "double", nullable: false),
                    MagazineSize = table.Column<int>(type: "int", nullable: false),
                    ReloadTimeSeconds = table.Column<double>(type: "double", nullable: false),
                    RangeMeters = table.Column<double>(type: "double", nullable: false),
                    HeadshotDamageMultiplier = table.Column<double>(type: "double", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerWeaponStates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerWeaponStates_FirearmDefinitions_FirearmDefinitionId",
                        column: x => x.FirearmDefinitionId,
                        principalTable: "FirearmDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlayerWeaponStates_PlayerSaveData_PlayerSaveDataId",
                        column: x => x.PlayerSaveDataId,
                        principalTable: "PlayerSaveData",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_AuthVerificationCodes_Email",
                table: "AuthVerificationCodes",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_FirearmDefinitions_Name",
                table: "FirearmDefinitions",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerSaveData_PlayerId",
                table: "PlayerSaveData",
                column: "PlayerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerStatUpgradeStates_PlayerSaveDataId_StatName",
                table: "PlayerStatUpgradeStates",
                columns: new[] { "PlayerSaveDataId", "StatName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerWeaponStates_FirearmDefinitionId",
                table: "PlayerWeaponStates",
                column: "FirearmDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerWeaponStates_PlayerSaveDataId_FirearmDefinitionId",
                table: "PlayerWeaponStates",
                columns: new[] { "PlayerSaveDataId", "FirearmDefinitionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_UserName",
                table: "Users",
                column: "UserName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuthVerificationCodes");

            migrationBuilder.DropTable(
                name: "PlayerStatUpgradeStates");

            migrationBuilder.DropTable(
                name: "PlayerWeaponStates");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "FirearmDefinitions");

            migrationBuilder.DropTable(
                name: "PlayerSaveData");
        }
    }
}
