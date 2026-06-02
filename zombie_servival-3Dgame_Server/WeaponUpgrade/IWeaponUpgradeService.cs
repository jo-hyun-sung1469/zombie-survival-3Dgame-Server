namespace zombie_survival_3Dgame_Server.WeaponUpgrade;

public interface IWeaponUpgradeService
{
    Task<WeaponUpgradeResult> UpgradeAsync(
        string playerId,
        string weaponName,
        CancellationToken cancellationToken);
}
