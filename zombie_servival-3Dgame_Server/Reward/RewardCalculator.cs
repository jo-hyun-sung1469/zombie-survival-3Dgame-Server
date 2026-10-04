namespace zombie_survival_3Dgame_Server.Reward;

public static class RewardCalculator
{
    public static int Calculate(float survivalTime, int clearWave, int killZombies)
    {
        if (!float.IsFinite(survivalTime) || survivalTime < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(survivalTime), survivalTime, "Survival time must be finite and non-negative.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(clearWave);
        ArgumentOutOfRangeException.ThrowIfNegative(killZombies);

        var cappedSurvivalTime = Math.Min((double)survivalTime, RewardConstants.MaxSurvivalTimeSeconds);
        var reward = cappedSurvivalTime * RewardConstants.GoldPerSurvivalSecond
                     + (double)clearWave * RewardConstants.GoldPerClearedWave
                     + (double)killZombies * RewardConstants.GoldPerZombieKill;

        return checked((int)reward);
    }
}
