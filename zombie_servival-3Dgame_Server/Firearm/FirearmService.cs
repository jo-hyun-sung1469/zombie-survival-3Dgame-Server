using Microsoft.EntityFrameworkCore;
using zombie_survival_3Dgame_Server.Contracts.Firearm;
using zombie_survival_3Dgame_Server.Data;
using zombie_survival_3Dgame_Server.Firearm.Models;

namespace zombie_survival_3Dgame_Server.Firearm;

public sealed class FirearmService(GameDbContext dbContext) : IFirearmService
{
    public async Task<FirearmCollectionResponse> GetAllAsync(CancellationToken cancellationToken)
    {
        var weapons = await dbContext.FirearmDefinitions
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return new FirearmCollectionResponse
        {
            Weapons = weapons.Select(MapResponse).ToList()
        };
    }

    public async Task<FirearmStatsResponse?> GetByNameAsync(string weaponName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(weaponName))
        {
            return null;
        }

        var normalizedWeaponName = weaponName.Trim();
        var weapon = await dbContext.FirearmDefinitions
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Name == normalizedWeaponName, cancellationToken);

        return weapon is null ? null : MapResponse(weapon);
    }

    private static FirearmStatsResponse MapResponse(FirearmDefinition weapon)
    {
        return new FirearmStatsResponse
        {
            Name = weapon.Name,
            DisplayName = weapon.DisplayName,
            Category = weapon.Category,
            Rarity = weapon.Rarity,
            GachaProbability = weapon.GachaProbability,
            Damage = weapon.Damage,
            FireRate = weapon.FireRate,
            MagazineSize = weapon.MagazineSize,
            ReloadTimeSeconds = weapon.ReloadTimeSeconds,
            RangeMeters = weapon.RangeMeters,
            HeadshotDamageMultiplier = weapon.HeadshotDamageMultiplier
        };
    }
}
