using Microsoft.EntityFrameworkCore;
using zombie_servival_3Dgame_Server.Contracts.Inventory;
using zombie_servival_3Dgame_Server.Data;

namespace zombie_servival_3Dgame_Server.Inventory;

public sealed class InventoryService(GameDbContext dbContext) : IInventoryService
{
    public async Task<PlayerSaveResponse> SaveAsync(
        string playerId,
        SavePlayerDataRequest request,
        CancellationToken cancellationToken)
    {
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
            .Select(x => new PlayerWeaponState
            {
                WeaponName = x.Key,
                IsOwned = x.Value
            })
            .ToList();

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(saveData);
    }

    public async Task<PlayerSaveResponse?> GetByPlayerIdAsync(string playerId, CancellationToken cancellationToken)
    {
        var saveData = await dbContext.PlayerSaveData
            .AsNoTracking()
            .Include(x => x.WeaponStates)
            .SingleOrDefaultAsync(x => x.PlayerId == playerId, cancellationToken);

        return saveData is null ? null : ToResponse(saveData);
    }

    private static PlayerSaveResponse ToResponse(PlayerSaveData saveData)
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
