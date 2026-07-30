using zombie_survival_3Dgame_Server.Contracts.Inventory;

namespace zombie_survival_3Dgame_Server.Inventory;

public interface IInventoryService
{
    Task<PlayerSaveResponse?> GetByPlayerIdAsync(string playerId, CancellationToken cancellationToken);
}
