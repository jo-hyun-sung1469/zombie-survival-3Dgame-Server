namespace zombie_survival_3Dgame_Server.Contracts.Player;

public sealed class PlayerStatUpgradeStateResponse
{
    public required string StatName { get; init; }
    public required int Level { get; init; }
    public required int MaxLevel { get; init; }
    public required int NextUpgradeCost { get; init; }
}
