namespace zombie_survival_3Dgame_Server.Contracts.Player;

public sealed class UpgradePlayerStatResponse
{
    public required string UpgradedStat { get; init; }
    public required int CurrentLevel { get; init; }
    public required int MaxLevel { get; init; }
    public required int UpgradeCost { get; init; }
    public required int NextUpgradeCost { get; init; }
    public required int CurrentGold { get; init; }
    public required PlayerStatsResponse Stats { get; init; }
    public required DateTime UpdatedAtUtc { get; init; }
}
