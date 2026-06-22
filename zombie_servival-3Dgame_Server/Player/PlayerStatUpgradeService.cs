using Microsoft.Extensions.Options;
using zombie_survival_3Dgame_Server.Contracts.Player;
using zombie_survival_3Dgame_Server.Data;
using zombie_survival_3Dgame_Server.Inventory;
using zombie_survival_3Dgame_Server.Options;
using zombie_survival_3Dgame_Server.Player.Models;

namespace zombie_survival_3Dgame_Server.Player;

public sealed class PlayerStatUpgradeService(
    GameDbContext dbContext,
    IPlayerSaveDataStore playerSaveDataStore,
    IOptions<PlayerOptions> playerOptions) : IPlayerStatUpgradeService
{
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

        
        var saveData = await playerSaveDataStore.GetOrCreateAsync(playerId, cancellationToken);
        var upgradeState = saveData.StatUpgradeStates
            .SingleOrDefault(x => string.Equals(x.StatName, canonicalStatName, StringComparison.OrdinalIgnoreCase));

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

        var nextUpgradeCost = upgradeState.UpgradeLevel >= _statUpgrades.MaxLevel
            ? 0
            : PlayerStatsCalculator.CalculateUpgradeCost(_statUpgrades, upgradeState.UpgradeLevel);

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
                CurrentUpgradeLevel = upgradeState.UpgradeLevel,
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
}
