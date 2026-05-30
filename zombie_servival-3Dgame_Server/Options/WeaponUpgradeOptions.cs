namespace zombie_servival_3Dgame_Server.Options;

public sealed class WeaponUpgradeOptions
{
    public const string SectionName = "WeaponUpgrade";

    public int MaxLevel { get; init; } = 50;
    public double CostIncreaseRate { get; init; } = 0.2;
    public double StatIncreaseRate { get; init; } = 0.1;
    public Dictionary<string, int> BaseCostsByRarity { get; init; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Common"] = 500,
        ["Rare"] = 800,
        ["Epic"] = 1200,
        ["Legendary"] = 2000
    };
}
