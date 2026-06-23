using zombie_survival_3Dgame_Server.Inventory.Models;

namespace zombie_survival_3Dgame_Server.Player.Models;

public sealed class PlayerStatUpgradeState
{
    public int Id { get; set; }
    public int PlayerSaveDataId { get; set; }
    public PlayerSaveData? PlayerSaveData { get; set; }
    public string StatName { get; set; } = string.Empty;
    public int UpgradeLevel { get; set; }
}
