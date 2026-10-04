using Microsoft.EntityFrameworkCore;
using zombie_survival_3Dgame_Server.Contracts.Reward;
using zombie_survival_3Dgame_Server.Data;
using zombie_survival_3Dgame_Server.Inventory.Models;

namespace zombie_survival_3Dgame_Server.Reward;

public sealed class SurvivalRewardService(GameDbContext dbContext, TimeProvider timeProvider) : ISurvivalRewardService
{
    public async Task<RewardClaimResult> ClaimAsync(
        string playerId, RewardRequest request, CancellationToken cancellationToken)
    {
        var session = await dbContext.SurvivalGameSessions
            .SingleOrDefaultAsync(x => x.Id == request.SessionId && x.PlayerId == playerId, cancellationToken);
        if (session is null || !await dbContext.Users.AnyAsync(x => x.Id == playerId, cancellationToken))
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
        int reward;
        try
        {
            reward = completed
                ? RewardCalculator.Calculate(session.SurvivalTimeSeconds, session.ClearWave, session.KillZombies)
                : RewardCalculator.Calculate(request.SurvivalTime, request.ClearWave, request.KillZombies);
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
        if (!completed)
        {
            session.CompleteWithReportedStats(now, request.SurvivalTime, request.ClearWave, request.KillZombies);
        }

        if (saveData is null)
        {
            saveData = new PlayerSaveData { PlayerId = playerId };
            dbContext.PlayerSaveData.Add(saveData);
        }

        saveData.Gold = checked(currentGold + reward);
        saveData.MarkChanged(now);
        session.Claim(reward, now);
        // Completion, claim and gold changes commit together with concurrency checks.
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
                ClaimedAtUtc = session.ClaimedAtUtc!.Value,
                AlreadyClaimed = alreadyClaimed
            }
        };
    }
}
