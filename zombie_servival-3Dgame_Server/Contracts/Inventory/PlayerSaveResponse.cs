namespace zombie_servival_3Dgame_Server.Contracts.Inventory;

public sealed class PlayerSaveResponse
{
    public required string PlayerId { get; init; }
    public required int Gold { get; init; }
    public required Dictionary<string, bool> WeaponStates { get; init; }
    public required IReadOnlyList<PlayerWeaponStateResponse> Weapons { get; init; }
    public required DateTime UpdatedAtUtc { get; init; }
}
