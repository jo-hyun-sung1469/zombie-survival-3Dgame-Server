using zombie_survival_3Dgame_Server.Contracts.Inventory;

namespace zombie_survival_3Dgame_Server.Inventory;

public interface IInventoryService
{
    Task<PlayerSaveResponse> SaveAsync(
        string playerId,
        SavePlayerDataRequest request,
        CancellationToken cancellationToken);

    Task<PlayerSaveResponse?> GetByPlayerIdAsync(string playerId, CancellationToken cancellationToken);
}
