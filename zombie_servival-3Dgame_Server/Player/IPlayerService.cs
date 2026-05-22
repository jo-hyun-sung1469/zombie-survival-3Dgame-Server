using zombie_servival_3Dgame_Server.Contracts.Player;

namespace zombie_servival_3Dgame_Server.Player;

public interface IPlayerService
{
    Task<PlayerStatsResponse> GetStatsAsync(string playerId, CancellationToken cancellationToken);
}
