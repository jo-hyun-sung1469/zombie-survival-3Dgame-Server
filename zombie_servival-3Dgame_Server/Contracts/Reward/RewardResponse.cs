namespace zombie_survival_3Dgame_Server.Contracts.Reward;

public sealed class RewardResponse
{
    public required string SessionId { get; init; }
    public required string PlayerId { get; init; }
    public required int AwardedGold { get; init; }
    public required int CurrentGold { get; init; }
    public required DateTime ClaimedAtUtc { get; init; }
    public required bool AlreadyClaimed { get; init; }
}
