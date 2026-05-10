using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using zombie_servival_3Dgame_Server.Contracts.Inventory;
using zombie_servival_3Dgame_Server.Data;
using zombie_servival_3Dgame_Server.Options;

namespace zombie_servival_3Dgame_Server.Inventory;

public sealed class InventoryService(
    GameDbContext dbContext,
    IPlayerSaveDataStore playerSaveDataStore,
    IOptions<GachaOptions> gachaOptions) : IInventoryService
{
    private readonly HashSet<string> _validWeaponNames = gachaOptions.Value.Rewards
        .Select(x => x.RewardName.Trim())
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public async Task<PlayerSaveResponse> SaveAsync(
        string playerId,
        SavePlayerDataRequest request,
        CancellationToken cancellationToken)
    {
        ValidateWeaponStates(request.WeaponStates);
        var saveData = await playerSaveDataStore.GetOrCreateAsync(playerId, cancellationToken);

        saveData.Gold = request.Gold;
        saveData.UpdatedAtUtc = DateTime.UtcNow;
        ApplyWeaponStates(saveData, request.WeaponStates);

        await dbContext.SaveChangesAsync(cancellationToken);
        return MapResponse(saveData);
    }

    public async Task<PlayerSaveResponse?> GetByPlayerIdAsync(string playerId, CancellationToken cancellationToken)
    {
        var saveData = await dbContext.PlayerSaveData
            .AsNoTracking()
            .Include(x => x.WeaponStates)
            .SingleOrDefaultAsync(x => x.PlayerId == playerId, cancellationToken);

        return saveData is null ? null : MapResponse(saveData);
    }

    private void ValidateWeaponStates(IReadOnlyDictionary<string, bool> weaponStates)
    {
        var invalidWeaponNames = weaponStates.Keys
            .Select(x => x.Trim())
            .Where(x => !_validWeaponNames.Contains(x))
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

    private void ApplyWeaponStates(PlayerSaveData saveData, IReadOnlyDictionary<string, bool> weaponStates)
    {
        var requestedStates = weaponStates
            .ToDictionary(x => x.Key.Trim(), x => x.Value, StringComparer.OrdinalIgnoreCase);

        var existingStates = saveData.WeaponStates
            .ToDictionary(x => x.WeaponName, StringComparer.OrdinalIgnoreCase);

        foreach (var requestedState in requestedStates)
        {
            if (existingStates.TryGetValue(requestedState.Key, out var existingState))
            {
                existingState.IsOwned = requestedState.Value;
                continue;
            }

            saveData.WeaponStates.Add(new PlayerWeaponState
            {
                WeaponName = requestedState.Key,
                IsOwned = requestedState.Value
            });
        }

        var statesToRemove = saveData.WeaponStates
            .Where(x => !requestedStates.ContainsKey(x.WeaponName))
            .ToList();

        foreach (var stateToRemove in statesToRemove)
        {
            saveData.WeaponStates.Remove(stateToRemove);
            dbContext.PlayerWeaponStates.Remove(stateToRemove);
        }
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
            UpdatedAtUtc = saveData.UpdatedAtUtc
        };
    }
}
