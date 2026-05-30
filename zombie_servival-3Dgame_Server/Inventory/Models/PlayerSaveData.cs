namespace zombie_survival_3Dgame_Server.Inventory;

public sealed class PlayerSaveData
{
    public int Id { get; set; }
    public string PlayerId { get; set; } = string.Empty;
    public int Gold { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public List<PlayerWeaponState> WeaponStates { get; set; } = [];
}
