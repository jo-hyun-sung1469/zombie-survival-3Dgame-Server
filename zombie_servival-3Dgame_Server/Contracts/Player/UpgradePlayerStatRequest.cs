using System.ComponentModel.DataAnnotations;

namespace zombie_survival_3Dgame_Server.Contracts.Player;

public sealed class UpgradePlayerStatRequest
{
    [Required]
    public string StatName { get; init; } = string.Empty;
}
