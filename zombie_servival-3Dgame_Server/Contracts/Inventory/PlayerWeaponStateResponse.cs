namespace zombie_servival_3Dgame_Server.Contracts.Inventory;

public sealed class PlayerWeaponStateResponse
{
    public required string WeaponName { get; init; }
    public required bool IsOwned { get; init; }
    public required int WeaponLevel { get; init; }
    public required int Damage { get; init; }
    public required double FireRate { get; init; }
    public required int MagazineSize { get; init; }
    public required double ReloadTimeSeconds { get; init; }
    public required double RangeMeters { get; init; }
    public required double CriticalMultiplier { get; init; }
}
