using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using zombie_survival_3Dgame_Server.Contracts.Player;
using zombie_survival_3Dgame_Server.Data;
using zombie_survival_3Dgame_Server.Inventory.Models;
using zombie_survival_3Dgame_Server.Options;
using zombie_survival_3Dgame_Server.Player.Models;

namespace zombie_survival_3Dgame_Server.Player;

public sealed class PlayerStatUpgradeService(
    GameDbContext dbContext,
    IOptions<PlayerOptions> playerOptions) : IPlayerStatUpgradeService
{
    private const string LegacyCriticalChanceStatName = "CriticalChance";
    private const string HeadshotDamageMultiplierStatName = "HeadshotDamageMultiplier";
    private readonly PlayerBaseStatsOptions _baseStats = playerOptions.Value.BaseStats;
    private readonly PlayerStatUpgradeOptions _statUpgrades = playerOptions.Value.StatUpgrades;

    public async Task<PlayerStatUpgradeResult> UpgradeAsync(
        string playerId,
        string statName,
        CancellationToken cancellationToken)
    {
        if (!TryGetCanonicalStatName(statName, out var canonicalStatName))
        {
            return new PlayerStatUpgradeResult { Status = PlayerStatUpgradeStatus.InvalidStat };
        }

        var saveData = await GetOrCreateSaveDataWithStatUpgradesAsync(playerId, cancellationToken);
        RepairLegacyStatUpgradeNames(saveData);
        var upgradeState = saveData.StatUpgradeStates
            .FirstOrDefault(x => string.Equals(x.StatName, canonicalStatName, StringComparison.OrdinalIgnoreCase));

        var currentUpgradeLevel = Math.Clamp(
            upgradeState?.UpgradeLevel ?? PlayerStatsCalculator.DefaultUpgradeLevel,
            PlayerStatsCalculator.DefaultUpgradeLevel,
            _statUpgrades.MaxLevel);
        if (currentUpgradeLevel >= _statUpgrades.MaxLevel)
        {
            return new PlayerStatUpgradeResult
            {
                Status = PlayerStatUpgradeStatus.MaxLevelReached,
                CurrentGold = saveData.Gold,
                CurrentUpgradeLevel = currentUpgradeLevel,
                MaxLevel = _statUpgrades.MaxLevel
            };
        }

        var upgradeCost = PlayerStatsCalculator.CalculateUpgradeCost(_statUpgrades, currentUpgradeLevel);
        if (saveData.Gold < upgradeCost)
        {
            return new PlayerStatUpgradeResult
            {
                Status = PlayerStatUpgradeStatus.InsufficientGold,
                RequiredGold = upgradeCost,
                CurrentGold = saveData.Gold,
                CurrentUpgradeLevel = currentUpgradeLevel,
                MaxLevel = _statUpgrades.MaxLevel
            };
        }

        if (upgradeState is null)
        {
            upgradeState = new PlayerStatUpgradeState
            {
                StatName = canonicalStatName
            };
            saveData.StatUpgradeStates.Add(upgradeState);
        }

        saveData.Gold -= upgradeCost;
        saveData.UpdatedAtUtc = DateTime.UtcNow;
        upgradeState.StatName = canonicalStatName;
        upgradeState.UpgradeLevel = currentUpgradeLevel + 1;

        await dbContext.SaveChangesAsync(cancellationToken);

        var nextUpgradeCost = upgradeState.UpgradeLevel >= _statUpgrades.MaxLevel ? 
            0 : PlayerStatsCalculator.CalculateUpgradeCost(_statUpgrades, upgradeState.UpgradeLevel);

        return new PlayerStatUpgradeResult
        {
            Status = PlayerStatUpgradeStatus.Success,
            RequiredGold = upgradeCost,
            CurrentGold = saveData.Gold,
            CurrentUpgradeLevel = upgradeState.UpgradeLevel,
            MaxLevel = _statUpgrades.MaxLevel,
            Response = new UpgradePlayerStatResponse
            {
                UpgradedStat = canonicalStatName,
                CurrentLevel = upgradeState.UpgradeLevel,
                MaxLevel = _statUpgrades.MaxLevel,
                UpgradeCost = upgradeCost,
                NextUpgradeCost = nextUpgradeCost,
                CurrentGold = saveData.Gold,
                Stats = PlayerStatsCalculator.Calculate(
                    saveData.PlayerId,
                    _baseStats,
                    _statUpgrades,
                    saveData.StatUpgradeStates),
                UpdatedAtUtc = saveData.UpdatedAtUtc
            }
        };
    }

    private bool TryGetCanonicalStatName(string statName, out string canonicalStatName)
    {
        canonicalStatName = string.Empty;
        if (string.IsNullOrWhiteSpace(statName))
        {
            return false;
        }

        var requestedStatName = statName.Trim();
        if (string.Equals(requestedStatName, LegacyCriticalChanceStatName, StringComparison.OrdinalIgnoreCase)
            && _statUpgrades.IncreasesByStat.ContainsKey(HeadshotDamageMultiplierStatName))
        {
            canonicalStatName = HeadshotDamageMultiplierStatName;
            return true;
        }

        foreach (var availableStatName in _statUpgrades.IncreasesByStat.Keys)
        {
            if (!string.Equals(availableStatName, requestedStatName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            canonicalStatName = availableStatName;
            return true;
        }

        return false;
    }

    private void RepairLegacyStatUpgradeNames(PlayerSaveData saveData)
    {
        var legacyStates = saveData.StatUpgradeStates
            .Where(x => string.Equals(x.StatName, LegacyCriticalChanceStatName, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (legacyStates.Count == 0)
        {
            return;
        }

        var headshotState = saveData.StatUpgradeStates.FirstOrDefault(
            x => string.Equals(x.StatName, HeadshotDamageMultiplierStatName, StringComparison.OrdinalIgnoreCase));
        foreach (var legacyState in legacyStates)
        {
            if (headshotState is null)
            {
                legacyState.StatName = HeadshotDamageMultiplierStatName;
                headshotState = legacyState;
                continue;
            }

            headshotState.UpgradeLevel = Math.Max(headshotState.UpgradeLevel, legacyState.UpgradeLevel);
            saveData.StatUpgradeStates.Remove(legacyState);
            dbContext.PlayerStatUpgradeStates.Remove(legacyState);
        }
    }

    private async Task<PlayerSaveData> GetOrCreateSaveDataWithStatUpgradesAsync(
        string playerId,
        CancellationToken cancellationToken)
    {
        var saveData = await dbContext.PlayerSaveData
            .Include(x => x.StatUpgradeStates)
            .SingleOrDefaultAsync(x => x.PlayerId == playerId, cancellationToken);

        if (saveData is not null)
        {
            return saveData;
        }

        saveData = new PlayerSaveData
        {
            PlayerId = playerId,
            UpdatedAtUtc = DateTime.UtcNow
        };

        dbContext.PlayerSaveData.Add(saveData);
        return saveData;
    }
}
