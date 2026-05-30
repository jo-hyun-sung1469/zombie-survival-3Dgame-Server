namespace zombie_servival_3Dgame_Server.Firearm.Configuration;

public sealed class FirearmCatalogItem
{
    public required string Name { get; init; }
    public required string DisplayName { get; init; }
    public required string Category { get; init; }
    public required string Rarity { get; init; }
    public required double GachaProbability { get; init; }
    public required int Damage { get; init; }
    public required double FireRate { get; init; }
    public required int MagazineSize { get; init; }
    public required double ReloadTimeSeconds { get; init; }
    public required double RangeMeters { get; init; }
    public required double CriticalMultiplier { get; init; }
}
