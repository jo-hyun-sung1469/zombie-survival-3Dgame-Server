namespace zombie_survival_3Dgame_Server.Contracts.Firearm;

public sealed class FirearmCollectionResponse
{
    public required IReadOnlyList<FirearmStatsResponse> Weapons { get; init; }
}
