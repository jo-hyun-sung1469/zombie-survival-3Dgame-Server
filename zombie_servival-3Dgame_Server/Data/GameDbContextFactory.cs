using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace zombie_survival_3Dgame_Server.Data;

public sealed class GameDbContextFactory : IDesignTimeDbContextFactory<GameDbContext>
{
    public static readonly MySqlServerVersion MySqlServerVersion = new(new Version(8, 4, 10));

    public GameDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<GameDbContext>()
            .UseMySql(
                "Server=localhost;Port=3306;Database=zombie_survival;",
                MySqlServerVersion)
            .Options;

        return new GameDbContext(options);
    }
}
