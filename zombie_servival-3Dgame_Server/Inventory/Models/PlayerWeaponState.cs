using zombie_survival_3Dgame_Server.Firearm.Models;

namespace zombie_survival_3Dgame_Server.Inventory.Models;

public sealed class PlayerWeaponState
{
    public int Id { get; set; }
    public int PlayerSaveDataId { get; set; }
    public int FirearmDefinitionId { get; set; }
    public string WeaponName { get; set; } = string.Empty;
    public bool IsOwned { get; set; }
    public int WeaponLevel { get; set; } = 1;
    public int Damage { get; set; }
    public double FireRate { get; set; }
    public int MagazineSize { get; set; }
    public double ReloadTimeSeconds { get; set; }
    public double RangeMeters { get; set; }
    public double HeadshotDamageMultiplier { get; set; }
    public PlayerSaveData? PlayerSaveData { get; set; }
    public FirearmDefinition? FirearmDefinition { get; set; }
}
