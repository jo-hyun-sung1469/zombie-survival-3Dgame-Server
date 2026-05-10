namespace zombie_servival_3Dgame_Server.Options;

public sealed class PlayerOptions
{
    public const string SectionName = "Player";

    public PlayerBaseStatsOptions BaseStats { get; init; } = new();
}

public sealed class PlayerBaseStatsOptions
{
    public int MaxHealth { get; init; } = 100;
    public int AttackPower { get; init; } = 10;
    public int Defense { get; init; } = 5;
    public double MoveSpeed { get; init; } = 5.0;
    public double CriticalChance { get; init; } = 0.05;
}
