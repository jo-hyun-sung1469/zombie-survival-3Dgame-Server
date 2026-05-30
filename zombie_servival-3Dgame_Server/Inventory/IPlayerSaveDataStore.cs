namespace zombie_survival_3Dgame_Server.Inventory;

public interface IPlayerSaveDataStore
{
    Task<PlayerSaveData> GetOrCreateAsync(string playerId, CancellationToken cancellationToken);
}
