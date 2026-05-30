using Microsoft.EntityFrameworkCore;
using zombie_servival_3Dgame_Server.Firearm.Configuration;
using zombie_servival_3Dgame_Server.Firearm.Models;

namespace zombie_servival_3Dgame_Server.Data;

public static class FirearmCatalogSeeder
{
    public static async Task UpsertAsync(
        GameDbContext dbContext,
        IReadOnlyList<FirearmCatalogItem> catalog,
        CancellationToken cancellationToken = default)
    {
        var byName = await dbContext.FirearmDefinitions
            .ToDictionaryAsync(x => x.Name, StringComparer.OrdinalIgnoreCase, cancellationToken);

        foreach (var item in catalog)
        {
            if (byName.TryGetValue(item.Name, out var existing))
            {
                existing.DisplayName = item.DisplayName;
                existing.Category = item.Category;
                existing.Rarity = item.Rarity;
                existing.GachaProbability = item.GachaProbability;
                existing.Damage = item.Damage;
                existing.FireRate = item.FireRate;
                existing.MagazineSize = item.MagazineSize;
                existing.ReloadTimeSeconds = item.ReloadTimeSeconds;
                existing.RangeMeters = item.RangeMeters;
                existing.CriticalMultiplier = item.CriticalMultiplier;
                continue;
            }

            dbContext.FirearmDefinitions.Add(new FirearmDefinition
            {
                Name = item.Name,
                DisplayName = item.DisplayName,
                Category = item.Category,
                Rarity = item.Rarity,
                GachaProbability = item.GachaProbability,
                Damage = item.Damage,
                FireRate = item.FireRate,
                MagazineSize = item.MagazineSize,
                ReloadTimeSeconds = item.ReloadTimeSeconds,
                RangeMeters = item.RangeMeters,
                CriticalMultiplier = item.CriticalMultiplier
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
