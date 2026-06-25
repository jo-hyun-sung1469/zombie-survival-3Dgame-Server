namespace zombie_survival_3Dgame_Server.Options;

public sealed class PlayerDefaultDataOptions
{
    public const string SectionName = "PlayerDefaultData";

    public int InitialGold { get; init; }
    public bool RepairGoldWhenZero { get; init; }
    public int DefaultWeaponLevel { get; init; } = 1;
    public Dictionary<string, bool> WeaponStates { get; init; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Shotgun"] = true
    };
    public Dictionary<string, int> StatUpgradeLevels { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
