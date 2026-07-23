using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;
using zombie_survival_3Dgame_Server.Data;
using zombie_survival_3Dgame_Server.Firearm.Models;

namespace zombie_survival_3Dgame_Server.Tests.Data.Migrations;

/// <summary>
/// Exercises the actual Up()/Down() SQL emitted by the InitialCreate migration
/// against a real relational database (SQLite in-memory) to make sure the
/// generated schema (tables, unique indexes) behaves as intended. The MySQL-only
/// annotations (e.g. "MySql:CharSet") are simply ignored by the SQLite provider,
/// while the actual DDL operations (CreateTable, CreateIndex, DropTable, ...) run
/// for real.
/// </summary>
public sealed class InitialCreateMigrationExecutionTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public InitialCreateMigrationExecutionTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private GameDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<GameDbContext>()
            .UseSqlite(_connection)
            .Options;

        return new GameDbContext(options);
    }

    [Fact]
    public void Migrate_CreatesAllExpectedTables()
    {
        using var dbContext = CreateContext();

        dbContext.Database.Migrate();

        var tableNames = GetTableNames(dbContext);

        Assert.Contains("Users", tableNames);
        Assert.Contains("AuthVerificationCodes", tableNames);
        Assert.Contains("FirearmDefinitions", tableNames);
        Assert.Contains("PlayerSaveData", tableNames);
        Assert.Contains("PlayerWeaponStates", tableNames);
        Assert.Contains("PlayerStatUpgradeStates", tableNames);
    }

    [Fact]
    public void Migrate_RecordsMigrationInHistoryTable()
    {
        using var dbContext = CreateContext();

        dbContext.Database.Migrate();

        var appliedMigrations = dbContext.Database.GetAppliedMigrations().ToList();

        Assert.Contains("20260720014315_InitialCreate", appliedMigrations);
    }

    [Fact]
    public void Migrate_CanBeSafelyReapplied()
    {
        using var dbContext = CreateContext();

        dbContext.Database.Migrate();
        var exception = Record.Exception(() => dbContext.Database.Migrate());

        Assert.Null(exception);
    }

    [Fact]
    public void Migrate_AllowsInsertingAndReadingBackAFirearmDefinition()
    {
        using var dbContext = CreateContext();
        dbContext.Database.Migrate();

        dbContext.FirearmDefinitions.Add(CreateFirearm("TestWeapon"));
        dbContext.SaveChanges();

        var saved = dbContext.FirearmDefinitions.Single(x => x.Name == "TestWeapon");

        Assert.Equal("Test Weapon", saved.DisplayName);
        Assert.True(saved.Id > 0);
    }

    [Fact]
    public void Migrate_EnforcesUniqueFirearmDefinitionNameIndex()
    {
        using var dbContext = CreateContext();
        dbContext.Database.Migrate();

        dbContext.FirearmDefinitions.Add(CreateFirearm("DuplicateWeapon"));
        dbContext.SaveChanges();

        dbContext.FirearmDefinitions.Add(CreateFirearm("DuplicateWeapon"));

        Assert.Throws<DbUpdateException>(() => dbContext.SaveChanges());
    }

    [Fact]
    public void Migrate_DownToZero_DropsAllCreatedTables()
    {
        using var dbContext = CreateContext();
        var migrator = dbContext.GetService<IMigrator>();

        migrator.Migrate();
        migrator.Migrate(Migration.InitialDatabase);

        var tableNames = GetTableNames(dbContext);

        Assert.DoesNotContain("Users", tableNames);
        Assert.DoesNotContain("AuthVerificationCodes", tableNames);
        Assert.DoesNotContain("FirearmDefinitions", tableNames);
        Assert.DoesNotContain("PlayerSaveData", tableNames);
        Assert.DoesNotContain("PlayerWeaponStates", tableNames);
        Assert.DoesNotContain("PlayerStatUpgradeStates", tableNames);
    }

    private static FirearmDefinition CreateFirearm(string name) => new()
    {
        Name = name,
        DisplayName = "Test Weapon",
        Category = "Test",
        Rarity = "Common",
        GachaProbability = 0.1,
        Damage = 10,
        FireRate = 1.0,
        MagazineSize = 10,
        ReloadTimeSeconds = 1.0,
        RangeMeters = 10,
        HeadshotDamageMultiplier = 1.0
    };

    private static List<string> GetTableNames(GameDbContext dbContext)
    {
        var connection = dbContext.Database.GetDbConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table';";

        using var reader = command.ExecuteReader();
        var names = new List<string>();
        while (reader.Read())
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }
}