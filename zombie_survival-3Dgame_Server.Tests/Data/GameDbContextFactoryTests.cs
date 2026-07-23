using Microsoft.EntityFrameworkCore;
using Xunit;
using zombie_survival_3Dgame_Server.Data;

namespace zombie_survival_3Dgame_Server.Tests.Data;

public class GameDbContextFactoryTests
{
    [Fact]
    public void CreateDbContext_ReturnsUsableGameDbContext()
    {
        var factory = new GameDbContextFactory();

        using var dbContext = factory.CreateDbContext(Array.Empty<string>());

        Assert.NotNull(dbContext);
        Assert.IsType<GameDbContext>(dbContext);
    }

    [Fact]
    public void CreateDbContext_ConfiguresPomeloMySqlProvider()
    {
        var factory = new GameDbContextFactory();

        using var dbContext = factory.CreateDbContext(Array.Empty<string>());

        Assert.Equal("Pomelo.EntityFrameworkCore.MySql", dbContext.Database.ProviderName);
    }

    [Fact]
    public void CreateDbContext_UsesExpectedDesignTimeConnectionString()
    {
        var factory = new GameDbContextFactory();

        using var dbContext = factory.CreateDbContext(Array.Empty<string>());

        var connectionString = dbContext.Database.GetConnectionString();

        Assert.NotNull(connectionString);
        Assert.Contains("Server=localhost", connectionString);
        Assert.Contains("Port=3306", connectionString);
        Assert.Contains("Database=zombie_survival", connectionString);
    }

    [Fact]
    public void CreateDbContext_WithEmptyArgs_Succeeds()
    {
        var factory = new GameDbContextFactory();

        using var dbContext = factory.CreateDbContext(Array.Empty<string>());

        Assert.NotNull(dbContext);
    }

    [Fact]
    public void CreateDbContext_WithArbitraryArgs_IgnoresThemAndSucceeds()
    {
        var factory = new GameDbContextFactory();

        using var dbContext = factory.CreateDbContext(new[] { "--environment", "Production", "extra-arg" });

        Assert.NotNull(dbContext);
    }

    [Fact]
    public void CreateDbContext_ReturnsNewInstanceEachCall()
    {
        var factory = new GameDbContextFactory();

        using var first = factory.CreateDbContext(Array.Empty<string>());
        using var second = factory.CreateDbContext(Array.Empty<string>());

        Assert.NotSame(first, second);
    }

    [Fact]
    public void MySqlServerVersion_IsExpectedMySql8410()
    {
        var serverVersion = GameDbContextFactory.MySqlServerVersion;

        Assert.Equal(new Version(8, 4, 10), serverVersion.Version);
    }

    [Fact]
    public void MySqlServerVersion_IsSharedStaticInstance()
    {
        Assert.Same(GameDbContextFactory.MySqlServerVersion, GameDbContextFactory.MySqlServerVersion);
    }
}