using zombie_survival_3Dgame_Server.Contracts.Reward;

namespace zombie_survival_3Dgame_Server.Reward;

public sealed class RewardClaimResult
{
    public required RewardClaimStatus Status { get; init; }
    public RewardResponse? Response { get; init; }
}
