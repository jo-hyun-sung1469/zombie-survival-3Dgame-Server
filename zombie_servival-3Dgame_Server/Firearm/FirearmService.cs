using Microsoft.Extensions.Options;
using zombie_servival_3Dgame_Server.Contracts.Firearm;
using zombie_servival_3Dgame_Server.Options;

namespace zombie_servival_3Dgame_Server.Firearm;

public sealed class FirearmService(IOptions<FirearmOptions> firearmOptions) : IFirearmService
{
    private readonly IReadOnlyList<FirearmDefinitionOption> _weapons = firearmOptions.Value.Weapons
        .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
        .ToList();

    public Task<FirearmCollectionResponse> GetAllAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(new FirearmCollectionResponse
        {
            Weapons = _weapons.Select(MapResponse).ToList()
        });
    }

    public Task<FirearmStatsResponse?> GetByNameAsync(string weaponName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var weapon = _weapons.SingleOrDefault(x =>
            string.Equals(x.Name, weaponName, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(weapon is null ? null : MapResponse(weapon));
    }

    private static FirearmStatsResponse MapResponse(FirearmDefinitionOption weapon)
    {
        return new FirearmStatsResponse
        {
            Name = weapon.Name,
            DisplayName = weapon.DisplayName,
            Category = weapon.Category,
            GachaProbability = weapon.GachaProbability,
            Damage = weapon.Damage,
            FireRate = weapon.FireRate,
            MagazineSize = weapon.MagazineSize,
            ReloadTimeSeconds = weapon.ReloadTimeSeconds,
            RangeMeters = weapon.RangeMeters,
            CriticalMultiplier = weapon.CriticalMultiplier
        };
    }
}
