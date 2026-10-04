using Microsoft.EntityFrameworkCore;
using zombie_survival_3Dgame_Server.Contracts.GameSession;
using zombie_survival_3Dgame_Server.Data;
using zombie_survival_3Dgame_Server.GameSession.Models;

namespace zombie_survival_3Dgame_Server.GameSession;

public sealed class SurvivalGameSessionService(GameDbContext dbContext, TimeProvider timeProvider)
    : IGameSessionService, IServerGameSessionRecorder
{
    public async Task<GameSessionResponse?> GetAsync(
        string playerId, string sessionId, CancellationToken cancellationToken)
    {
        var session = await dbContext.SurvivalGameSessions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == sessionId && x.PlayerId == playerId, cancellationToken);
        return session is null ? null : ToResponse(session);
    }

    public async Task<GameSessionResponse?> StartAsync(string playerId, CancellationToken cancellationToken)
    {
        if (!await dbContext.Users.AnyAsync(x => x.Id == playerId, cancellationToken))
        {
            return null;
        }

        var active = await dbContext.SurvivalGameSessions
            .SingleOrDefaultAsync(x => x.ActivePlayerId == playerId, cancellationToken);
        if (active is not null)
        {
            return ToResponse(active);
        }

        var session = SurvivalGameSession.Start(playerId, timeProvider.GetUtcNow().UtcDateTime);
        dbContext.SurvivalGameSessions.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(session);
    }

    public async Task<GameSessionResponse?> CompleteAsync(
        string playerId, string sessionId, int clearWave, int killZombies, CancellationToken cancellationToken)
    {
        var session = await dbContext.SurvivalGameSessions
            .SingleOrDefaultAsync(x => x.Id == sessionId && x.PlayerId == playerId, cancellationToken);
        if (session is null)
        {
            return null;
        }

        session.Complete(timeProvider.GetUtcNow().UtcDateTime, clearWave, killZombies);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(session);
    }

    private static GameSessionResponse ToResponse(SurvivalGameSession session) => new()
    {
        SessionId = session.Id,
        StartedAtUtc = session.StartedAtUtc,
        CompletedAtUtc = session.CompletedAtUtc,
        SurvivalTimeSeconds = session.SurvivalTimeSeconds,
        ClearWave = session.ClearWave,
        KillZombies = session.KillZombies,
        AwardedGold = session.AwardedGold,
        ClaimedAtUtc = session.ClaimedAtUtc
    };
}
