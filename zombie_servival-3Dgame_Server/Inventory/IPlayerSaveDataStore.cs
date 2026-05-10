namespace zombie_servival_3Dgame_Server.Inventory;

public interface IPlayerSaveDataStore
{
    Task<PlayerSaveData> GetOrCreateAsync(string playerId, CancellationToken cancellationToken);
}
