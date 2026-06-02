using Microsoft.Extensions.Options;
using zombie_survival_3Dgame_Server.Contracts.Player;
using zombie_survival_3Dgame_Server.Options;

namespace zombie_survival_3Dgame_Server.Player;

public sealed class PlayerService(IOptions<PlayerOptions> playerOptions) : IPlayerService
{
    private readonly PlayerBaseStatsOptions _baseStats = playerOptions.Value.BaseStats;

    public Task<PlayerStatsResponse> GetStatsAsync(string playerId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(new PlayerStatsResponse
        {
            PlayerId = playerId,
            MaxHealth = _baseStats.MaxHealth,
            AttackPower = _baseStats.AttackPower,
            Defense = _baseStats.Defense,
            MoveSpeed = _baseStats.MoveSpeed,
            CriticalChance = _baseStats.CriticalChance
        });
    }
}
