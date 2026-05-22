using zombie_servival_3Dgame_Server.Contracts.Firearm;

namespace zombie_servival_3Dgame_Server.Firearm;

public interface IFirearmService
{
    Task<FirearmCollectionResponse> GetAllAsync(CancellationToken cancellationToken);
    Task<FirearmStatsResponse?> GetByNameAsync(string weaponName, CancellationToken cancellationToken);
}
