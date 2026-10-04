using zombie_survival_3Dgame_Server.Reward;

namespace zombie_survival_3Dgame_Server.GameSession.Models;

public sealed class SurvivalGameSession
{
    private SurvivalGameSession() { }

    public string Id { get; private set; } = string.Empty;
    public string PlayerId { get; private set; } = string.Empty;
    public string? ActivePlayerId { get; private set; }
    public DateTime StartedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public float SurvivalTimeSeconds { get; private set; }
    public int ClearWave { get; private set; }
    public int KillZombies { get; private set; }
    public int? AwardedGold { get; private set; }
    public DateTime? ClaimedAtUtc { get; private set; }
    public long Version { get; private set; }

    public static SurvivalGameSession Start(string playerId, DateTime startedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(playerId);
        return new SurvivalGameSession
        {
            Id = Guid.NewGuid().ToString("N"),
            PlayerId = playerId,
            ActivePlayerId = playerId,
            StartedAtUtc = startedAtUtc
        };
    }

    public void Complete(DateTime completedAtUtc, int clearWave, int killZombies)
    {
        CompleteWithReportedStats(
            completedAtUtc, (float)(completedAtUtc - StartedAtUtc).TotalSeconds, clearWave, killZombies);
    }

    public void CompleteWithReportedStats(DateTime completedAtUtc, float survivalTime, int clearWave, int killZombies)
    {
        if (CompletedAtUtc.HasValue)
        {
            throw new InvalidOperationException("The session has already ended.");
        }

        if (completedAtUtc < StartedAtUtc)
        {
            throw new ArgumentOutOfRangeException(nameof(completedAtUtc));
        }

        _ = RewardCalculator.Calculate(survivalTime, clearWave, killZombies);
        Version = checked(Version + 1);
        SurvivalTimeSeconds = survivalTime;
        ClearWave = clearWave;
        KillZombies = killZombies;
        CompletedAtUtc = completedAtUtc;
        ActivePlayerId = null;
    }

    public void Claim(int gold, DateTime claimedAtUtc)
    {
        if (!CompletedAtUtc.HasValue || ClaimedAtUtc.HasValue)
        {
            throw new InvalidOperationException("Only completed, unclaimed sessions can be rewarded.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(gold);
        Version = checked(Version + 1);
        AwardedGold = gold;
        ClaimedAtUtc = claimedAtUtc;
    }
}
