using zombie_survival_3Dgame_Server.GameSession.Models;

namespace zombie_survival_3Dgame_Server.GameSession.Progress;

public interface IGameSessionProgressStore
{
    Task InitializeAsync(SurvivalGameSession session, CancellationToken cancellationToken);
    Task<SessionProgress?> GetFinalAsync(SurvivalGameSession session, CancellationToken cancellationToken);
    Task<SessionProgress?> GetProgressAsync(SurvivalGameSession session, CancellationToken cancellationToken);
    Task<ProgressResult> UpdateAsync(SurvivalGameSession session, SessionProgress progress, bool final, CancellationToken cancellationToken);
    Task RemoveAsync(string playerId, string sessionId, CancellationToken cancellationToken);
    Task FinalizeTtlAsync(SurvivalGameSession session, CancellationToken cancellationToken);
}
