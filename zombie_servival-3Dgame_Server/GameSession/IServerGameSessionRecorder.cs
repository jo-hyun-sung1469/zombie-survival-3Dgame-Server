using zombie_survival_3Dgame_Server.Contracts.GameSession;

namespace zombie_survival_3Dgame_Server.GameSession;

// Only trusted server game logic may supply these results; never forward client reports here.
public interface IServerGameSessionRecorder
{
    Task<GameSessionResponse?> StartAsync(string playerId, CancellationToken cancellationToken);
    Task<GameSessionResponse?> CompleteAsync(
        string playerId, string sessionId, int clearWave, int killZombies, CancellationToken cancellationToken);
}
