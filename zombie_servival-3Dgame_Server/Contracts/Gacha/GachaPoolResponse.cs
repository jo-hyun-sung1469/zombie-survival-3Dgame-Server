namespace zombie_servival_3Dgame_Server.Contracts.Gacha;

public sealed class GachaPoolResponse
{
    public required int PullCost { get; init; }
    public required int CurrentGold { get; init; }
    public required int RemainingRewardCount { get; init; }
    public required IReadOnlyList<GachaRewardProbabilityResponse> Rewards { get; init; }
}
