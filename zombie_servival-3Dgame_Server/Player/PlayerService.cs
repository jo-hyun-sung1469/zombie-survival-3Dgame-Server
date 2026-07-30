using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using zombie_survival_3Dgame_Server.Contracts.Player;
using zombie_survival_3Dgame_Server.Data;
using zombie_survival_3Dgame_Server.Options;

namespace zombie_survival_3Dgame_Server.Player;

public sealed class PlayerService(
    GameDbContext dbContext,
    IOptions<PlayerOptions> playerOptions) : IPlayerService
{
    private readonly PlayerBaseStatsOptions _baseStats = playerOptions.Value.BaseStats;
    private readonly PlayerStatUpgradeOptions _statUpgrades = playerOptions.Value.StatUpgrades;

    public async Task<PlayerStatsResponse> GetStatsAsync(string playerId, CancellationToken cancellationToken)
    {
        var upgradeStates = await dbContext.PlayerSaveData
            .AsNoTracking()
            .Where(x => x.PlayerId == playerId)
            .SelectMany(x => x.StatUpgradeStates)
            .ToListAsync(cancellationToken);

        return PlayerStatsCalculator.Calculate(playerId, _baseStats, _statUpgrades, upgradeStates);
    }
}
