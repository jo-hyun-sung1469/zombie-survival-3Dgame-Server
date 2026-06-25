using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using zombie_survival_3Dgame_Server.Data;
using zombie_survival_3Dgame_Server.Firearm.Models;
using zombie_survival_3Dgame_Server.Inventory.Models;
using zombie_survival_3Dgame_Server.Options;
using zombie_survival_3Dgame_Server.Player.Models;

namespace zombie_survival_3Dgame_Server.Inventory;

public sealed class PlayerDefaultDataRepairService(
    GameDbContext dbContext,
    IOptions<PlayerDefaultDataOptions> defaultDataOptions,
    IOptions<PlayerOptions> playerOptions) : IPlayerDefaultDataRepairService
{
    private const string LegacyCriticalChanceStatName = "CriticalChance";
    private const string HeadshotDamageMultiplierStatName = "HeadshotDamageMultiplier";
    private readonly PlayerDefaultDataOptions _defaults = defaultDataOptions.Value;
    private readonly PlayerStatUpgradeOptions _statUpgrades = playerOptions.Value.StatUpgrades;

    public async Task EnsureAsync(string playerId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var changed = false;
        var saveData = await dbContext.PlayerSaveData
            .Include(x => x.WeaponStates)
            .Include(x => x.StatUpgradeStates)
            .SingleOrDefaultAsync(x => x.PlayerId == playerId, cancellationToken);

        if (saveData is null)
        {
            saveData = new PlayerSaveData
            {
                PlayerId = playerId,
                Gold = Math.Max(0, _defaults.InitialGold),
                UpdatedAtUtc = now
            };
            dbContext.PlayerSaveData.Add(saveData);
            changed = true;
        }
        else if (saveData.Gold < 0 || (_defaults.RepairGoldWhenZero && saveData.Gold == 0))
        {
            saveData.Gold = Math.Max(0, _defaults.InitialGold);
            changed = true;
        }

        if (await EnsureWeaponDefaultsAsync(saveData, cancellationToken))
        {
            changed = true;
        }

        if (EnsureStatUpgradeDefaults(saveData))
        {
            changed = true;
        }

        if (!changed)
        {
            return;
        }

        saveData.UpdatedAtUtc = now;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<bool> EnsureWeaponDefaultsAsync(
        PlayerSaveData saveData,
        CancellationToken cancellationToken)
    {
        var defaultWeaponStates = (_defaults.WeaponStates ?? new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase))
            .Where(x => !string.IsNullOrWhiteSpace(x.Key))
            .ToDictionary(x => x.Key.Trim(), x => x.Value, StringComparer.OrdinalIgnoreCase);
        if (defaultWeaponStates.Count == 0)
        {
            return false;
        }

        var firearms = await dbContext.FirearmDefinitions
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var firearmByName = firearms.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
        var changed = false;

        foreach (var defaultWeaponState in defaultWeaponStates)
        {
            if (!firearmByName.TryGetValue(defaultWeaponState.Key, out var firearm))
            {
                continue;
            }

            var weaponState = saveData.WeaponStates.FirstOrDefault(x => x.FirearmDefinitionId == firearm.Id)
                              ?? saveData.WeaponStates.FirstOrDefault(
                                  x => string.Equals(x.WeaponName, firearm.Name, StringComparison.OrdinalIgnoreCase));
            if (weaponState is null)
            {
                saveData.WeaponStates.Add(CreateWeaponState(firearm, defaultWeaponState.Value));
                changed = true;
                continue;
            }

            if (RepairWeaponState(weaponState, firearm))
            {
                changed = true;
            }
        }

        return changed;
    }

    private bool EnsureStatUpgradeDefaults(PlayerSaveData saveData)
    {
        var defaultStatLevels = (_defaults.StatUpgradeLevels ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase))
            .Where(x => !string.IsNullOrWhiteSpace(x.Key))
            .ToDictionary(x => x.Key.Trim(), x => x.Value, StringComparer.OrdinalIgnoreCase);
        var changed = RepairLegacyStatUpgradeNames(saveData);

        foreach (var statUpgradeState in saveData.StatUpgradeStates)
        {
            if (!TryGetCanonicalStatName(statUpgradeState.StatName, out var canonicalStatName))
            {
                continue;
            }

            var repairedLevel = ClampUpgradeLevel(statUpgradeState.UpgradeLevel);
            if (statUpgradeState.StatName != canonicalStatName)
            {
                statUpgradeState.StatName = canonicalStatName;
                changed = true;
            }

            if (statUpgradeState.UpgradeLevel == repairedLevel)
            {
                continue;
            }

            statUpgradeState.UpgradeLevel = repairedLevel;
            changed = true;
        }

        foreach (var defaultStatLevel in defaultStatLevels)
        {
            if (!TryGetCanonicalStatName(defaultStatLevel.Key, out var canonicalStatName))
            {
                continue;
            }

            var statUpgradeState = saveData.StatUpgradeStates.FirstOrDefault(
                x => string.Equals(x.StatName, canonicalStatName, StringComparison.OrdinalIgnoreCase));
            var upgradeLevel = ClampUpgradeLevel(defaultStatLevel.Value);
            if (statUpgradeState is null)
            {
                saveData.StatUpgradeStates.Add(new PlayerStatUpgradeState
                {
                    StatName = canonicalStatName,
                    UpgradeLevel = upgradeLevel
                });
                changed = true;
                continue;
            }

            if (statUpgradeState.UpgradeLevel > 0)
            {
                continue;
            }

            statUpgradeState.UpgradeLevel = upgradeLevel;
            changed = true;
        }

        return changed;
    }

    private bool RepairLegacyStatUpgradeNames(PlayerSaveData saveData)
    {
        var legacyStates = saveData.StatUpgradeStates
            .Where(x => string.Equals(x.StatName, LegacyCriticalChanceStatName, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (legacyStates.Count == 0)
        {
            return false;
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

        return true;
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

    private int ClampUpgradeLevel(int upgradeLevel)
    {
        return Math.Clamp(upgradeLevel <= 0 ? 1 : upgradeLevel, 1, _statUpgrades.MaxLevel);
    }

    private PlayerWeaponState CreateWeaponState(FirearmDefinition firearm, bool isOwned)
    {
        return new PlayerWeaponState
        {
            FirearmDefinitionId = firearm.Id,
            WeaponName = firearm.Name,
            IsOwned = isOwned,
            WeaponLevel = Math.Max(1, _defaults.DefaultWeaponLevel),
            Damage = firearm.Damage,
            FireRate = firearm.FireRate,
            MagazineSize = firearm.MagazineSize,
            ReloadTimeSeconds = firearm.ReloadTimeSeconds,
            RangeMeters = firearm.RangeMeters,
            HeadshotDamageMultiplier = firearm.HeadshotDamageMultiplier
        };
    }

    private bool RepairWeaponState(PlayerWeaponState weaponState, FirearmDefinition firearm)
    {
        var changed = false;

        if (weaponState.FirearmDefinitionId != firearm.Id)
        {
            weaponState.FirearmDefinitionId = firearm.Id;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(weaponState.WeaponName)
            || !string.Equals(weaponState.WeaponName, firearm.Name, StringComparison.Ordinal))
        {
            weaponState.WeaponName = firearm.Name;
            changed = true;
        }

        if (weaponState.WeaponLevel <= 0)
        {
            weaponState.WeaponLevel = Math.Max(1, _defaults.DefaultWeaponLevel);
            changed = true;
        }

        if (weaponState.Damage <= 0)
        {
            weaponState.Damage = firearm.Damage;
            changed = true;
        }

        if (weaponState.FireRate <= 0)
        {
            weaponState.FireRate = firearm.FireRate;
            changed = true;
        }

        if (weaponState.MagazineSize <= 0)
        {
            weaponState.MagazineSize = firearm.MagazineSize;
            changed = true;
        }

        if (weaponState.ReloadTimeSeconds <= 0)
        {
            weaponState.ReloadTimeSeconds = firearm.ReloadTimeSeconds;
            changed = true;
        }

        if (weaponState.RangeMeters <= 0)
        {
            weaponState.RangeMeters = firearm.RangeMeters;
            changed = true;
        }

        if (weaponState.HeadshotDamageMultiplier <= 0)
        {
            weaponState.HeadshotDamageMultiplier = firearm.HeadshotDamageMultiplier;
            changed = true;
        }

        return changed;
    }
}
