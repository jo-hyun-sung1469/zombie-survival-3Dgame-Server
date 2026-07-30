using zombie_survival_3Dgame_Server.Player.Models;

namespace zombie_survival_3Dgame_Server.Inventory.Models;

public sealed class PlayerSaveData
{
    public int Id { get; set; }
    public string PlayerId { get; set; } = string.Empty;
    public int Gold { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public long Version { get; private set; }
    public List<PlayerWeaponState> WeaponStates { get; set; } = [];
    public List<PlayerStatUpgradeState> StatUpgradeStates { get; set; } = [];

    public void MarkChanged(DateTime changedAtUtc)
    {
        UpdatedAtUtc = changedAtUtc;
        Version = checked(Version + 1);
    }
}
