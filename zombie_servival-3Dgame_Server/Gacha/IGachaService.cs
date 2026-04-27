using zombie_servival_3Dgame_Server.Contracts.Gacha;

namespace zombie_servival_3Dgame_Server.Gacha;

public interface IGachaService
{
    Task<GachaPoolResponse> GetPoolAsync(string playerId, CancellationToken cancellationToken);
    Task<GachaPullResult> PullAsync(string playerId, CancellationToken cancellationToken);
}
