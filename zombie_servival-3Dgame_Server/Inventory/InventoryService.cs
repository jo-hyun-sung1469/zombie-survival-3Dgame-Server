using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using zombie_servival_3Dgame_Server.Contracts.Inventory;
using zombie_servival_3Dgame_Server.Data;
using zombie_servival_3Dgame_Server.Options;

namespace zombie_servival_3Dgame_Server.Inventory;

public sealed class InventoryService(GameDbContext dbContext, IOptions<GachaOptions> gachaOptions) : IInventoryService
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

        var saveData = await dbContext.PlayerSaveData
            .Include(x => x.WeaponStates)
            .SingleOrDefaultAsync(x => x.PlayerId == playerId, cancellationToken);

        if (saveData is null)
        {
            saveData = new PlayerSaveData
            {
                PlayerId = playerId
            };
            dbContext.PlayerSaveData.Add(saveData);
        }

        saveData.Gold = request.Gold;
        saveData.UpdatedAtUtc = DateTime.UtcNow;

        dbContext.PlayerWeaponStates.RemoveRange(saveData.WeaponStates);
        saveData.WeaponStates = request.WeaponStates
            .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Select(x => new PlayerWeaponState
            {
                WeaponName = x.Key.Trim(),
                IsOwned = x.Value
            })
            .ToList();

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
