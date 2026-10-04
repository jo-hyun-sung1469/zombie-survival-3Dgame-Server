using zombie_survival_3Dgame_Server.Contracts.GameSession;

namespace zombie_survival_3Dgame_Server.GameSession;

public interface IGameSessionService
{
    Task<GameSessionResponse?> StartAsync(string playerId, CancellationToken cancellationToken);
    Task<GameSessionResponse?> GetAsync(string playerId, string sessionId, CancellationToken cancellationToken);
}
