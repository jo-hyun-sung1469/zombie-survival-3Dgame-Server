using Microsoft.EntityFrameworkCore;
using zombie_survival_3Dgame_Server.Contracts.Inventory;
using zombie_survival_3Dgame_Server.Data;
using zombie_survival_3Dgame_Server.Firearm.Models;
using zombie_survival_3Dgame_Server.Inventory.Models;

namespace zombie_survival_3Dgame_Server.Inventory;

public sealed class InventoryService(
    GameDbContext dbContext,
    IPlayerSaveDataStore playerSaveDataStore) : IInventoryService
{
    public async Task<PlayerSaveResponse> SaveAsync(
        string playerId,
        SavePlayerDataRequest request,
        CancellationToken cancellationToken)
    {
        var firearmByName = await GetFirearmByNameAsync(cancellationToken);
        ValidateWeaponStates(request.WeaponStates, firearmByName);
        var saveData = await playerSaveDataStore.GetOrCreateAsync(playerId, cancellationToken);

        saveData.Gold = request.Gold;
        saveData.UpdatedAtUtc = DateTime.UtcNow;
        ApplyWeaponStates(saveData, request.WeaponStates, firearmByName);

        await dbContext.SaveChangesAsync(cancellationToken);
        return MapResponse(saveData);
    }

    public async Task<PlayerSaveResponse?> GetByPlayerIdAsync(string playerId, CancellationToken cancellationToken)
    {
        var saveData = await dbContext.PlayerSaveData
            .AsNoTracking()
            .Include(x => x.WeaponStates)
            .ThenInclude(x => x.FirearmDefinition)
            .SingleOrDefaultAsync(x => x.PlayerId == playerId, cancellationToken);

        return saveData is null ? null : MapResponse(saveData);
    }

    private static void ValidateWeaponStates(
        IReadOnlyDictionary<string, bool> weaponStates,
        IReadOnlyDictionary<string, FirearmDefinition> firearmByName)
    {
        var invalidWeaponNames = weaponStates.Keys
            .Select(x => x.Trim())
            .Where(x => !firearmByName.ContainsKey(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (invalidWeaponNames.Length == 0)
        {
            return;
        }

        throw new InvalidWeaponStateException(
            $"Unsupported weapon names: {string.Join(", ", invalidWeaponNames)}");
    }

    private void ApplyWeaponStates(
        PlayerSaveData saveData,
        IReadOnlyDictionary<string, bool> weaponStates,
        IReadOnlyDictionary<string, FirearmDefinition> firearmByName)
    {
        var requestedStates = weaponStates
            .ToDictionary(x => x.Key.Trim(), x => x.Value, StringComparer.OrdinalIgnoreCase);

        var existingStates = saveData.WeaponStates
            .ToDictionary(x => x.FirearmDefinitionId);
        var requestedFirearmIds = requestedStates.Keys
            .Select(name => firearmByName[name].Id)
            .ToHashSet();

        foreach (var requestedState in requestedStates)
        {
            var firearm = firearmByName[requestedState.Key];
            if (existingStates.TryGetValue(firearm.Id, out var existingState))
            {
                existingState.IsOwned = requestedState.Value;
                existingState.WeaponName = firearm.Name;
                continue;
            }

            saveData.WeaponStates.Add(new PlayerWeaponState
            {
                FirearmDefinitionId = firearm.Id,
                WeaponName = firearm.Name,
                IsOwned = requestedState.Value,
                WeaponLevel = 1,
                Damage = firearm.Damage,
                FireRate = firearm.FireRate,
                MagazineSize = firearm.MagazineSize,
                ReloadTimeSeconds = firearm.ReloadTimeSeconds,
                RangeMeters = firearm.RangeMeters,
                HeadshotDamageMultiplier = firearm.HeadshotDamageMultiplier
            });
        }

        var statesToRemove = saveData.WeaponStates
            .Where(x => !requestedFirearmIds.Contains(x.FirearmDefinitionId))
            .ToList();

        foreach (var stateToRemove in statesToRemove)
        {
            saveData.WeaponStates.Remove(stateToRemove);
            dbContext.PlayerWeaponStates.Remove(stateToRemove);
        }
    }

    private async Task<IReadOnlyDictionary<string, FirearmDefinition>> GetFirearmByNameAsync(
        CancellationToken cancellationToken)
    {
        var firearms = await dbContext.FirearmDefinitions
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return firearms
            .ToDictionary(x => x.Name.Trim(), StringComparer.OrdinalIgnoreCase);
    }

    private static PlayerSaveResponse MapResponse(PlayerSaveData saveData)
    {
        return new PlayerSaveResponse
        {
            PlayerId = saveData.PlayerId,
            Gold = saveData.Gold,
            WeaponStates = saveData.WeaponStates
                .OrderBy(x => x.WeaponName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.WeaponName, x => x.IsOwned, StringComparer.OrdinalIgnoreCase),
            Weapons = saveData.WeaponStates
                .OrderBy(x => x.WeaponName, StringComparer.OrdinalIgnoreCase)
                .Select(x => new PlayerWeaponStateResponse
                {
                    WeaponName = x.WeaponName,
                    IsOwned = x.IsOwned,
                    WeaponLevel = x.WeaponLevel,
                    Damage = x.Damage,
                    FireRate = x.FireRate,
                    MagazineSize = x.MagazineSize,
                    ReloadTimeSeconds = x.ReloadTimeSeconds,
                    RangeMeters = x.RangeMeters,
                    HeadshotDamageMultiplier = x.HeadshotDamageMultiplier
                })
                .ToList(),
            UpdatedAtUtc = saveData.UpdatedAtUtc
        };
    }
}
