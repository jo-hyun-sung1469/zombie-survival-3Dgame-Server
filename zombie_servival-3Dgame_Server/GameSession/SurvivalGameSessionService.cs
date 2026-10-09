using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using zombie_survival_3Dgame_Server.Options;
using zombie_survival_3Dgame_Server.GameSession.Progress;
using zombie_survival_3Dgame_Server.Contracts.GameSession;
using zombie_survival_3Dgame_Server.Data;
using zombie_survival_3Dgame_Server.GameSession.Models;

namespace zombie_survival_3Dgame_Server.GameSession;

public sealed class SurvivalGameSessionService(GameDbContext dbContext, TimeProvider timeProvider,
    IGameSessionProgressStore progressStore, IOptions<RedisSessionOptions> options)
    : IGameSessionService, IServerGameSessionRecorder
{
    public async Task<GameSessionResponse?> GetAsync(
        string playerId, string sessionId, CancellationToken cancellationToken)
    {
        var session = await dbContext.SurvivalGameSessions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == sessionId && x.PlayerId == playerId, cancellationToken);
        if (session is null || IsExpired(session)) return null;
        var progress = session.CompletedAtUtc.HasValue ? null : await progressStore.GetProgressAsync(session, cancellationToken);
        return ToResponse(session, progress);
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
            if (!IsExpired(active))
                return ToResponse(active, await progressStore.GetProgressAsync(active, cancellationToken));
            dbContext.SurvivalGameSessions.Remove(active);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var previous = await dbContext.SurvivalGameSessions.AsNoTracking()
            .Where(x => x.PlayerId == playerId && x.CompletedAtUtc != null)
            .OrderByDescending(x => x.CompletedAtUtc).FirstOrDefaultAsync(cancellationToken);
        if (previous is not null)
            await progressStore.RemoveAsync(playerId, previous.Id, cancellationToken);
        var session = SurvivalGameSession.Start(playerId, timeProvider.GetUtcNow().UtcDateTime);
        await progressStore.InitializeAsync(session, cancellationToken);
        dbContext.SurvivalGameSessions.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(session);
    }

    public async Task<GameSessionResponse?> CompleteAsync(
        string playerId, string sessionId, int clearWave, int killZombies, CancellationToken cancellationToken)
    {
        var session = await dbContext.SurvivalGameSessions
            .SingleOrDefaultAsync(x => x.Id == sessionId && x.PlayerId == playerId, cancellationToken);
        if (session is null || IsExpired(session))
        {
            return null;
        }

        session.Complete(timeProvider.GetUtcNow().UtcDateTime, clearWave, killZombies);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(session);
    }

    private bool IsExpired(SurvivalGameSession session) =>
        (session.CompletedAtUtc ?? session.StartedAtUtc) <= timeProvider.GetUtcNow().UtcDateTime.AddDays(-options.Value.RetentionDays);

    private static GameSessionResponse ToResponse(SurvivalGameSession session, SessionProgress? progress = null) => new()
    {
        SessionId = session.Id,
        StartedAtUtc = DateTime.SpecifyKind(session.StartedAtUtc, DateTimeKind.Utc),
        CompletedAtUtc = session.CompletedAtUtc.HasValue ? DateTime.SpecifyKind(session.CompletedAtUtc.Value, DateTimeKind.Utc) : null,
        SurvivalTimeSeconds = progress?.SurvivalTimeSeconds ?? session.SurvivalTimeSeconds,
        ClearWave = progress?.ClearWave ?? session.ClearWave,
        KillZombies = progress?.KillZombies ?? session.KillZombies,
        AwardedGold = session.AwardedGold,
        ClaimedAtUtc = session.ClaimedAtUtc.HasValue ? DateTime.SpecifyKind(session.ClaimedAtUtc.Value, DateTimeKind.Utc) : null
    };
}
