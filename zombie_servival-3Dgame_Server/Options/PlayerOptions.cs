namespace zombie_survival_3Dgame_Server.Options;

public sealed class PlayerOptions
{
    public const string SectionName = "Player";

    public PlayerBaseStatsOptions BaseStats { get; init; } = new();
    public PlayerStatUpgradeOptions StatUpgrades { get; init; } = new();
}

public sealed class PlayerBaseStatsOptions
{
    public int MaxHealth { get; init; } = 100;
    public int AttackPower { get; init; } = 10;
    public int Defense { get; init; } = 5;
    public double MoveSpeed { get; init; } = 5.0;
    public double CriticalChance { get; init; } = 0.05;
}

public sealed class PlayerStatUpgradeOptions
{
    public int MaxLevel { get; init; } = 50;
    public int BaseCost { get; init; } = 150;
    public double CostIncreaseRate { get; init; } = 0.15;
    public Dictionary<string, double> IncreasesByStat { get; init; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["MaxHealth"] = 50,
        ["AttackPower"] = 5,
        ["Defense"] = 1,
        ["CriticalChance"] = 0.01
    };
}
