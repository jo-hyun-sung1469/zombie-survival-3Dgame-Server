using zombie_servival_3Dgame_Server.Contracts.Gacha;

namespace zombie_servival_3Dgame_Server.Gacha;

public sealed class GachaPullResult
{
    public required GachaPullStatus Status { get; init; }
    public int RequiredGold { get; init; }
    public int CurrentGold { get; init; }
    public GachaPullResponse? Response { get; init; }
}
