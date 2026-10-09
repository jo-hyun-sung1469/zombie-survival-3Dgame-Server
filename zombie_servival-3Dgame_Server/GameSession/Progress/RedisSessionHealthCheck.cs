using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace zombie_survival_3Dgame_Server.GameSession.Progress;

public sealed class RedisSessionHealthCheck(RedisSessionConnection connection) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = await connection.GetDatabaseAsync(cancellationToken);
            await db.PingAsync(CommandFlags.DemandMaster).WaitAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception) { return HealthCheckResult.Unhealthy("Redis session storage unavailable.", exception); }
    }
}
