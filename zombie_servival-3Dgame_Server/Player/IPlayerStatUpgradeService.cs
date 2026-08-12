namespace zombie_survival_3Dgame_Server.Player;

public interface IPlayerStatUpgradeService
{
    Task<PlayerStatUpgradeResult> UpgradeAsync(
        string playerId,
        string statName,
        CancellationToken cancellationToken);
}
