using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using zombie_survival_3Dgame_Server.Options;
using zombie_survival_3Dgame_Server.GameSession.Progress;
using zombie_survival_3Dgame_Server.Contracts.Reward;
using zombie_survival_3Dgame_Server.Data;
using zombie_survival_3Dgame_Server.Inventory.Models;

namespace zombie_survival_3Dgame_Server.Reward;

public sealed class SurvivalRewardService(GameDbContext dbContext, TimeProvider timeProvider,
    SessionProgressValidator validator, IGameSessionProgressStore progressStore, IOptions<RedisSessionOptions> options,
    ILogger<SurvivalRewardService> logger) : ISurvivalRewardService
{
    public async Task<RewardClaimResult> ClaimAsync(
        string playerId, RewardRequest request, CancellationToken cancellationToken)
    {
        var session = await dbContext.SurvivalGameSessions
            .SingleOrDefaultAsync(x => x.Id == request.SessionId && x.PlayerId == playerId, cancellationToken);
        if (session is null || (session.CompletedAtUtc ?? session.StartedAtUtc)
            <= timeProvider.GetUtcNow().UtcDateTime.AddDays(-options.Value.RetentionDays)
            || !await dbContext.Users.AnyAsync(x => x.Id == playerId, cancellationToken))
        {
            return new RewardClaimResult { Status = RewardClaimStatus.NotFound };
        }

        var saveData = await dbContext.PlayerSaveData
            .SingleOrDefaultAsync(x => x.PlayerId == playerId, cancellationToken);
        if (session.ClaimedAtUtc.HasValue)
        {
            return Success(session.AwardedGold!.Value, saveData?.Gold ?? 0, true);
        }

        var completed = session.CompletedAtUtc.HasValue;
        if (!completed)
        {
            var validation = await validator.ValidateAsync(session, request.SurvivalTime, request.ClearWave,
                request.KillZombies, true, cancellationToken);
            if (validation.Status != ProgressStatus.Success)
                return new RewardClaimResult { Status = validation.Status == ProgressStatus.InvalidInput
                    ? RewardClaimStatus.InvalidInput : RewardClaimStatus.ProgressConflict };
            var final = validation.Progress!;
            session.CompleteWithReportedStats(final.RecordedAtUtc, final.SurvivalTimeSeconds, final.ClearWave, final.KillZombies);
            // Freeze results durably before shortening Redis TTL or attempting payout.
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        try { await progressStore.FinalizeTtlAsync(session, cancellationToken); }
        catch (RedisSessionUnavailableException exception)
        { logger.LogWarning(exception, "SQL final result is durable; Redis TTL update will be retried for {SessionId}.", session.Id); }
        int reward;
        try
        {
            reward = RewardCalculator.Calculate(session.SurvivalTimeSeconds, session.ClearWave, session.KillZombies);
        }
        catch (ArgumentOutOfRangeException)
        {
            return new RewardClaimResult { Status = RewardClaimStatus.InvalidInput };
        }
        catch (OverflowException)
        {
            return new RewardClaimResult { Status = RewardClaimStatus.InvalidInput };
        }
        var currentGold = saveData?.Gold ?? 0;
        if (currentGold > int.MaxValue - reward)
        {
            return new RewardClaimResult { Status = RewardClaimStatus.GoldLimitExceeded };
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (saveData is null)
        {
            saveData = new PlayerSaveData { PlayerId = playerId };
            dbContext.PlayerSaveData.Add(saveData);
        }

        saveData.Gold = checked(currentGold + reward);
        saveData.MarkChanged(now);
        session.Claim(reward, now);
        // Claim and gold changes commit together with concurrency checks.
        await dbContext.SaveChangesAsync(cancellationToken);
        return Success(reward, saveData.Gold, false);

        RewardClaimResult Success(int awardedGold, int gold, bool alreadyClaimed) => new()
        {
            Status = RewardClaimStatus.Success,
            Response = new RewardResponse
            {
                SessionId = session.Id,
                PlayerId = playerId,
                AwardedGold = awardedGold,
                CurrentGold = gold,
                ClaimedAtUtc = DateTime.SpecifyKind(session.ClaimedAtUtc!.Value, DateTimeKind.Utc),
                AlreadyClaimed = alreadyClaimed
            }
        };
    }
}
