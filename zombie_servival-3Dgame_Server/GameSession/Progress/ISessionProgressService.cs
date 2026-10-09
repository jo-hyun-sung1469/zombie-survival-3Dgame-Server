using zombie_survival_3Dgame_Server.Contracts.GameSession;

namespace zombie_survival_3Dgame_Server.GameSession.Progress;

public interface ISessionProgressService
{
    Task<ProgressResult> RecordWaveAsync(string playerId, string sessionId, WaveProgressRequest request, CancellationToken cancellationToken);
}
