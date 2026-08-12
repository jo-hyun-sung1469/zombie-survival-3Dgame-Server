namespace zombie_survival_3Dgame_Server.Contracts.Player;

public sealed class PlayerStatsResponse
{
    public required string PlayerId { get; init; }
    public required int MaxHealth { get; init; }
    public required int AttackPower { get; init; }
    public required int Defense { get; init; }
    public required double MoveSpeed { get; init; }
    public required double HeadshotDamageMultiplier { get; init; }
    public required IReadOnlyList<PlayerStatUpgradeStateResponse> StatUpgrades { get; init; }
}
