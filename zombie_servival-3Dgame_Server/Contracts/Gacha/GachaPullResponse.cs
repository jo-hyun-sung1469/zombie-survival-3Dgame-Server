namespace zombie_servival_3Dgame_Server.Contracts.Gacha;

public sealed class GachaPullResponse
{
    public required string RewardName { get; init; }
    public required string Rarity { get; init; }
    public required int RemainingRewardCount { get; init; }
    public required int SpentGold { get; init; }
    public required int RemainingGold { get; init; }
}
