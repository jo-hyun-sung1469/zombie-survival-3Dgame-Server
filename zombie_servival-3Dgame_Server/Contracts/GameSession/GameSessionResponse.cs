namespace zombie_survival_3Dgame_Server.Contracts.GameSession;

public sealed class GameSessionResponse
{
    public required string SessionId { get; init; }
    public required DateTime StartedAtUtc { get; init; }
    public DateTime? CompletedAtUtc { get; init; }
    public required float SurvivalTimeSeconds { get; init; }
    public required int ClearWave { get; init; }
    public required int KillZombies { get; init; }
    public int? AwardedGold { get; init; }
    public DateTime? ClaimedAtUtc { get; init; }
}
