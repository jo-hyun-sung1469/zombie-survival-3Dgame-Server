namespace zombie_survival_3Dgame_Server.Contracts.Gacha;

public sealed class GachaPullResponse
{
    public required string RewardName { get; init; }
    public required int CurrentGold { get; init; }
    public required int RemainingRewardCount { get; init; }
    public required DateTime UpdatedAtUtc { get; init; }
}
