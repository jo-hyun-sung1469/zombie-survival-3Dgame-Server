namespace zombie_servival_3Dgame_Server.Firearm.Models;

public sealed class FirearmDefinition
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public double GachaProbability { get; set; }
    public int Damage { get; set; }
    public double FireRate { get; set; }
    public int MagazineSize { get; set; }
    public double ReloadTimeSeconds { get; set; }
    public double RangeMeters { get; set; }
    public double CriticalMultiplier { get; set; }
}
