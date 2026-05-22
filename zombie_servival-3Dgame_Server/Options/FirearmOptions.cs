namespace zombie_servival_3Dgame_Server.Options;

public sealed class FirearmOptions
{
    public const string SectionName = "Firearms";

    public List<FirearmDefinitionOption> Weapons { get; init; } = [];
}

public sealed class FirearmDefinitionOption
{
    public string Name { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public double GachaProbability { get; init; }
    public int Damage { get; init; }
    public double FireRate { get; init; }
    public int MagazineSize { get; init; }
    public double ReloadTimeSeconds { get; init; }
    public double RangeMeters { get; init; }
    public double CriticalMultiplier { get; init; }
}
