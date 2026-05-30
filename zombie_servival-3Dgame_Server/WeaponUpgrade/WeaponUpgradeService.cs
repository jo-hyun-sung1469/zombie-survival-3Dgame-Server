using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using zombie_survival_3Dgame_Server.Contracts.Inventory;
using zombie_survival_3Dgame_Server.Contracts.WeaponUpgrade;
using zombie_survival_3Dgame_Server.Data;
using zombie_survival_3Dgame_Server.Firearm.Models;
using zombie_survival_3Dgame_Server.Inventory;
using zombie_survival_3Dgame_Server.Options;

namespace zombie_survival_3Dgame_Server.WeaponUpgrade;

public sealed class WeaponUpgradeService(
    GameDbContext dbContext,
    IPlayerSaveDataStore playerSaveDataStore,
    IOptions<WeaponUpgradeOptions> weaponUpgradeOptions) : IWeaponUpgradeService
{
    private static readonly string[] UpgradeCycle = ["Damage", "FireRate", "CriticalMultiplier"];
    private readonly WeaponUpgradeOptions _weaponUpgradeOptions = weaponUpgradeOptions.Value;

    public async Task<WeaponUpgradeResult> UpgradeAsync(
        string playerId,
        string weaponName,
        CancellationToken cancellationToken)
    {
        var normalizedWeaponName = weaponName.Trim();
        if (string.IsNullOrWhiteSpace(normalizedWeaponName))
        {
            return new WeaponUpgradeResult { Status = WeaponUpgradeStatus.WeaponNotFound };
        }

        var firearm = await dbContext.FirearmDefinitions
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Name.ToLower() == normalizedWeaponName.ToLower(), cancellationToken);

        if (firearm is null)
        {
            return new WeaponUpgradeResult { Status = WeaponUpgradeStatus.WeaponNotFound };
        }

        var saveData = await playerSaveDataStore.GetOrCreateAsync(playerId, cancellationToken);
        var weaponState = saveData.WeaponStates
            .SingleOrDefault(x => x.FirearmDefinitionId == firearm.Id);

        if (weaponState is null || !weaponState.IsOwned)
        {
            return new WeaponUpgradeResult
            {
                Status = WeaponUpgradeStatus.WeaponNotOwned,
                CurrentGold = saveData.Gold
            };
        }

        var currentLevel = Math.Max(1, weaponState.WeaponLevel);
        if (currentLevel >= _weaponUpgradeOptions.MaxLevel)
        {
            return new WeaponUpgradeResult
            {
                Status = WeaponUpgradeStatus.MaxLevelReached,
                CurrentGold = saveData.Gold,
                MaxLevel = _weaponUpgradeOptions.MaxLevel
            };
        }

        var upgradeCost = CalculateUpgradeCost(firearm.Rarity, currentLevel);
        if (saveData.Gold < upgradeCost)
        {
            return new WeaponUpgradeResult
            {
                Status = WeaponUpgradeStatus.InsufficientGold,
                RequiredGold = upgradeCost,
                CurrentGold = saveData.Gold
            };
        }

        var upgradedStat = UpgradeCycle[(currentLevel - 1) % UpgradeCycle.Length];
        saveData.Gold -= upgradeCost;
        saveData.UpdatedAtUtc = DateTime.UtcNow;
        weaponState.WeaponLevel = currentLevel + 1;
        weaponState.WeaponName = firearm.Name;
        RecalculateStats(weaponState, firearm);

        await dbContext.SaveChangesAsync(cancellationToken);

        return new WeaponUpgradeResult
        {
            Status = WeaponUpgradeStatus.Success,
            RequiredGold = upgradeCost,
            CurrentGold = saveData.Gold,
            Response = new UpgradeWeaponResponse
            {
                Weapon = MapWeaponResponse(weaponState),
                CurrentGold = saveData.Gold,
                UpgradeCost = upgradeCost,
                NextUpgradeCost = CalculateUpgradeCost(firearm.Rarity, weaponState.WeaponLevel),
                UpgradedStat = upgradedStat,
                UpdatedAtUtc = saveData.UpdatedAtUtc
            }
        };
    }

    private int CalculateUpgradeCost(string rarity, int weaponLevel)
    {
        var baseCost = _weaponUpgradeOptions.BaseCostsByRarity.TryGetValue(rarity, out var configuredCost)
            ? configuredCost
            : _weaponUpgradeOptions.BaseCostsByRarity["Common"];
        var multiplier = Math.Pow(1 + _weaponUpgradeOptions.CostIncreaseRate, Math.Max(1, weaponLevel) - 1);

        return (int)Math.Ceiling(baseCost * multiplier);
    }

    private void RecalculateStats(PlayerWeaponState weaponState, FirearmDefinition firearm)
    {
        var completedUpgradeCount = Math.Max(0, weaponState.WeaponLevel - 1);
        var damageUpgradeCount = CountStatUpgrades(completedUpgradeCount, "Damage");
        var fireRateUpgradeCount = CountStatUpgrades(completedUpgradeCount, "FireRate");
        var criticalUpgradeCount = CountStatUpgrades(completedUpgradeCount, "CriticalMultiplier");

        weaponState.Damage = RoundToInt(firearm.Damage * GetStatMultiplier(damageUpgradeCount));
        weaponState.FireRate = RoundToTwoDecimals(firearm.FireRate * GetStatMultiplier(fireRateUpgradeCount));
        weaponState.CriticalMultiplier = RoundToTwoDecimals(
            firearm.CriticalMultiplier * GetStatMultiplier(criticalUpgradeCount));
        weaponState.MagazineSize = firearm.MagazineSize;
        weaponState.ReloadTimeSeconds = firearm.ReloadTimeSeconds;
        weaponState.RangeMeters = firearm.RangeMeters;
    }

    private static int CountStatUpgrades(int completedUpgradeCount, string statName)
    {
        var count = 0;
        for (var index = 0; index < completedUpgradeCount; index++)
        {
            if (UpgradeCycle[index % UpgradeCycle.Length] == statName)
            {
                count++;
            }
        }

        return count;
    }

    private double GetStatMultiplier(int upgradeCount)
    {
        return 1 + _weaponUpgradeOptions.StatIncreaseRate * upgradeCount;
    }

    private static int RoundToInt(double value)
    {
        return (int)Math.Round(value, MidpointRounding.AwayFromZero);
    }

    private static double RoundToTwoDecimals(double value)
    {
        return Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    private static PlayerWeaponStateResponse MapWeaponResponse(PlayerWeaponState weaponState)
    {
        return new PlayerWeaponStateResponse
        {
            WeaponName = weaponState.WeaponName,
            IsOwned = weaponState.IsOwned,
            WeaponLevel = weaponState.WeaponLevel,
            Damage = weaponState.Damage,
            FireRate = weaponState.FireRate,
            MagazineSize = weaponState.MagazineSize,
            ReloadTimeSeconds = weaponState.ReloadTimeSeconds,
            RangeMeters = weaponState.RangeMeters,
            CriticalMultiplier = weaponState.CriticalMultiplier
        };
    }
}
