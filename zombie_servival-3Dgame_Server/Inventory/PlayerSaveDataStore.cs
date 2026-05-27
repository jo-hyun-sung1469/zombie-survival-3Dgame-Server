using Microsoft.EntityFrameworkCore;
using zombie_servival_3Dgame_Server.Data;

namespace zombie_servival_3Dgame_Server.Inventory;

public sealed class PlayerSaveDataStore(GameDbContext dbContext) : IPlayerSaveDataStore
{
    public async Task<PlayerSaveData> GetOrCreateAsync(string playerId, CancellationToken cancellationToken)
    {
        var saveData = await dbContext.PlayerSaveData
            .Include(x => x.WeaponStates)
            .ThenInclude(x => x.FirearmDefinition)
            .SingleOrDefaultAsync(x => x.PlayerId == playerId, cancellationToken);

        if (saveData is not null)
        {
            return saveData;
        }

        saveData = new PlayerSaveData
        {
            PlayerId = playerId,
            UpdatedAtUtc = DateTime.UtcNow
        };

        dbContext.PlayerSaveData.Add(saveData);
        return saveData;
    }
}
