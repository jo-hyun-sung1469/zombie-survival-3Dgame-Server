using zombie_survival_3Dgame_Server.Contracts.Reward;

namespace zombie_survival_3Dgame_Server.Reward;

public interface ISurvivalRewardService
{
    Task<RewardClaimResult> ClaimAsync(string playerId, RewardRequest request, CancellationToken cancellationToken);
}
