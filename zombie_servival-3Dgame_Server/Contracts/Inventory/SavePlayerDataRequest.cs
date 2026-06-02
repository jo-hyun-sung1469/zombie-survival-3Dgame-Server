using System.ComponentModel.DataAnnotations;

namespace zombie_survival_3Dgame_Server.Contracts.Inventory;

public sealed class SavePlayerDataRequest
{
    [Range(0, int.MaxValue)]
    public int Gold { get; init; }

    [Required]
    public Dictionary<string, bool> WeaponStates { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
