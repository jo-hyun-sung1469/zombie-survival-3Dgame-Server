using zombie_survival_3Dgame_Server.Contracts.WeaponUpgrade;

namespace zombie_survival_3Dgame_Server.WeaponUpgrade;

public sealed class WeaponUpgradeResult
{
    public required WeaponUpgradeStatus Status { get; init; }
    public int RequiredGold { get; init; }
    public int CurrentGold { get; init; }
    public int MaxLevel { get; init; }
    public UpgradeWeaponResponse? Response { get; init; }
}
