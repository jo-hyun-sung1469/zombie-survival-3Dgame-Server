using zombie_survival_3Dgame_Server.Contracts.Player;
using zombie_survival_3Dgame_Server.Options;
using zombie_survival_3Dgame_Server.Player.Models;

namespace zombie_survival_3Dgame_Server.Player;

internal static class PlayerStatsCalculator
{
    public const int DefaultUpgradeLevel = 1;

    public static PlayerStatsResponse Calculate(
        string playerId,
        PlayerBaseStatsOptions baseStats,
        PlayerStatUpgradeOptions upgradeOptions,
        IReadOnlyCollection<PlayerStatUpgradeState> upgradeStates)
    {
        var levelsByStat = upgradeStates.ToDictionary(
            x => x.StatName,
            x => Math.Clamp(x.UpgradeLevel, DefaultUpgradeLevel, upgradeOptions.MaxLevel),
            StringComparer.OrdinalIgnoreCase);

        return new PlayerStatsResponse
        {
            PlayerId = playerId,
            MaxHealth = RoundToInt(baseStats.MaxHealth + GetBonus("MaxHealth")),
            AttackPower = RoundToInt(baseStats.AttackPower + GetBonus("AttackPower")),
            Defense = RoundToInt(baseStats.Defense + GetBonus("Defense")),
            MoveSpeed = baseStats.MoveSpeed,
            CriticalChance = RoundToFourDecimals(baseStats.CriticalChance + GetBonus("CriticalChance")),
            StatUpgrades = upgradeOptions.IncreasesByStat.Keys
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .Select(x => new PlayerStatUpgradeStateResponse
                {
                    StatName = x,
                    UpgradeLevel = GetUpgradeLevel(x),
                    MaxLevel = upgradeOptions.MaxLevel,
                    NextUpgradeCost = GetUpgradeLevel(x) >= upgradeOptions.MaxLevel
                        ? 0
                        : CalculateUpgradeCost(upgradeOptions, GetUpgradeLevel(x))
                })
                .ToList()
        };

        int GetUpgradeLevel(string statName)
        {
            return levelsByStat.GetValueOrDefault(statName, DefaultUpgradeLevel);
        }

        double GetBonus(string statName)
        {
            return upgradeOptions.IncreasesByStat.TryGetValue(statName, out var increase)
                ? increase * Math.Max(0, GetUpgradeLevel(statName) - DefaultUpgradeLevel)
                : 0;
        }
    }

    public static int CalculateUpgradeCost(PlayerStatUpgradeOptions upgradeOptions, int currentUpgradeLevel)
    {
        var multiplier = Math.Pow(
            1 + upgradeOptions.CostIncreaseRate,
            Math.Max(0, currentUpgradeLevel - DefaultUpgradeLevel));
        return (int)Math.Ceiling(upgradeOptions.BaseCost * multiplier);
    }

    private static int RoundToInt(double value)
    {
        return (int)Math.Round(value, MidpointRounding.AwayFromZero);
    }

    private static double RoundToFourDecimals(double value)
    {
        return Math.Round(value, 4, MidpointRounding.AwayFromZero);
    }
}
