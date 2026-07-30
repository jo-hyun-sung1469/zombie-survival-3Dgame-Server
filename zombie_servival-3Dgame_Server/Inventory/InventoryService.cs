using Microsoft.EntityFrameworkCore;
using zombie_survival_3Dgame_Server.Contracts.Inventory;
using zombie_survival_3Dgame_Server.Data;
using zombie_survival_3Dgame_Server.Inventory.Models;

namespace zombie_survival_3Dgame_Server.Inventory;

public sealed class InventoryService(GameDbContext dbContext) : IInventoryService
{
    public async Task<PlayerSaveResponse?> GetByPlayerIdAsync(string playerId, CancellationToken cancellationToken)
    {
        var saveData = await dbContext.PlayerSaveData
            .AsNoTracking()
            .Include(x => x.WeaponStates)
            .ThenInclude(x => x.FirearmDefinition)
            .SingleOrDefaultAsync(x => x.PlayerId == playerId, cancellationToken);

        return saveData is null ? null : MapResponse(saveData);
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
