using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;
using zombie_survival_3Dgame_Server.Data;

namespace zombie_survival_3Dgame_Server.Tests.Data.Migrations;

/// <summary>
/// Verifies that the EF Core model and the checked-in migrations
/// (InitialCreate.cs / InitialCreate.Designer.cs / GameDbContextModelSnapshot.cs)
/// stay in sync. These checks do not require a live database connection.
/// </summary>
public class GameDbContextMigrationsTests
{
    private static GameDbContext CreateContext()
        => new GameDbContextFactory().CreateDbContext(Array.Empty<string>());

    [Fact]
    public void Model_HasNoPendingModelChanges()
    {
        using var dbContext = CreateContext();

        var hasPendingChanges = dbContext.Database.HasPendingModelChanges();

        Assert.False(
            hasPendingChanges,
            "The EF Core model no longer matches the latest migration snapshot. " +
            "Add a new migration or regenerate GameDbContextModelSnapshot.");
    }

    [Fact]
    public void MigrationsAssembly_ContainsInitialCreateMigration()
    {
        using var dbContext = CreateContext();
        var migrationsAssembly = dbContext.GetService<IMigrationsAssembly>();

        Assert.Contains("20260720014315_InitialCreate", migrationsAssembly.Migrations.Keys);
    }

    [Fact]
    public void MigrationsAssembly_OnlyContainsTheInitialMigration()
    {
        using var dbContext = CreateContext();
        var migrationsAssembly = dbContext.GetService<IMigrationsAssembly>();

        Assert.Single(migrationsAssembly.Migrations);
    }

    [Fact]
    public void InitialCreateMigration_IsAMigrationType()
    {
        using var dbContext = CreateContext();
        var migrationsAssembly = dbContext.GetService<IMigrationsAssembly>();

        var migrationType = migrationsAssembly.Migrations["20260720014315_InitialCreate"];

        Assert.True(typeof(Migration).IsAssignableFrom(migrationType));
    }
}