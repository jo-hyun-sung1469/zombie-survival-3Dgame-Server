using zombie_survival_3Dgame_Server.Player.Models;

namespace zombie_survival_3Dgame_Server.Inventory.Models;

public sealed class PlayerSaveData
{
    public int Id { get; set; }
    public string PlayerId { get; set; } = string.Empty;
    public int Gold { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public List<PlayerWeaponState> WeaponStates { get; set; } = [];
    public List<PlayerStatUpgradeState> StatUpgradeStates { get; set; } = [];
}
