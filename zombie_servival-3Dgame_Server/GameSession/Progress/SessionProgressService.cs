using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using zombie_survival_3Dgame_Server.Contracts.GameSession;
using zombie_survival_3Dgame_Server.Data;
using zombie_survival_3Dgame_Server.Options;

namespace zombie_survival_3Dgame_Server.GameSession.Progress;

public sealed class SessionProgressService(GameDbContext db, SessionProgressValidator validator,
    TimeProvider clock, IOptions<RedisSessionOptions> options) : ISessionProgressService
{
    public async Task<ProgressResult> RecordWaveAsync(string playerId, string sessionId, WaveProgressRequest request,
        CancellationToken cancellationToken)
    {
        var session = await db.SurvivalGameSessions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == sessionId && x.PlayerId == playerId, cancellationToken);
        if (session is null || session.StartedAtUtc <= clock.GetUtcNow().UtcDateTime.AddDays(-options.Value.RetentionDays))
            return new(ProgressStatus.NotFound);
        if (session.CompletedAtUtc.HasValue) return new(ProgressStatus.Conflict);
        return await validator.ValidateAsync(session, request.SurvivalTimeSeconds, request.ClearWave,
            request.KillZombies, false, cancellationToken);
    }
}
