namespace zombie_servival_3Dgame_Server.Inventory;

public sealed class PlayerWeaponState
{
    public int Id { get; set; }
    public int PlayerSaveDataId { get; set; }
    public string WeaponName { get; set; } = string.Empty;
    public bool IsOwned { get; set; }
    public PlayerSaveData? PlayerSaveData { get; set; }
}
