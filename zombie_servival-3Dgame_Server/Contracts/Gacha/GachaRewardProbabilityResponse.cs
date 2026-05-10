namespace zombie_servival_3Dgame_Server.Contracts.Gacha;

public sealed class GachaRewardProbabilityResponse
{
    public required string RewardName { get; init; }
    public required double Probability { get; init; }
    public required bool IsOwned { get; init; }
}
