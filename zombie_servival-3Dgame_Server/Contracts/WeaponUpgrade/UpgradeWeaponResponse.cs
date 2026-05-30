using zombie_survival_3Dgame_Server.Contracts.Inventory;

namespace zombie_survival_3Dgame_Server.Contracts.WeaponUpgrade;

public sealed class UpgradeWeaponResponse
{
    public required PlayerWeaponStateResponse Weapon { get; init; }
    public required int CurrentGold { get; init; }
    public required int UpgradeCost { get; init; }
    public required int NextUpgradeCost { get; init; }
    public required string UpgradedStat { get; init; }
    public required DateTime UpdatedAtUtc { get; init; }
}
