namespace zombie_survival_3Dgame_Server.Inventory;

public interface IPlayerDefaultDataRepairService
{
    Task EnsureAsync(string playerId, CancellationToken cancellationToken);
}
