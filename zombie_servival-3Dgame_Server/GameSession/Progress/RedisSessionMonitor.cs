using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using zombie_survival_3Dgame_Server.Options;

namespace zombie_survival_3Dgame_Server.GameSession.Progress;

public sealed class RedisSessionMonitor(RedisSessionHealthCheck health, IHttpClientFactory clients,
    TimeProvider clock, IOptions<RedisSessionOptions> options, ILogger<RedisSessionMonitor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        DateTimeOffset? failedAt = null;
        DateTimeOffset? notifiedAt = null;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5), clock);
        try
        {
            do
            {
                var result = await health.CheckHealthAsync(new HealthCheckContext(), stoppingToken);
                var now = clock.GetUtcNow();
                if (result.Status == HealthStatus.Healthy)
                {
                    if (failedAt.HasValue) logger.LogInformation("Redis session storage recovered.");
                    failedAt = notifiedAt = null;
                    continue;
                }
                failedAt ??= now;
                if (now - failedAt.Value < TimeSpan.FromSeconds(options.Value.FailureAlertSeconds)
                    || (notifiedAt.HasValue && now - notifiedAt.Value < TimeSpan.FromMinutes(5))) continue;
                logger.LogError("Redis remains unavailable after failover grace period; session writes are refused.");
                notifiedAt = now;
                if (string.IsNullOrWhiteSpace(options.Value.AlertWebhookUrl)) continue;
                try
                {
                    using var response = await clients.CreateClient("RedisAlert").PostAsJsonAsync(options.Value.AlertWebhookUrl,
                        new { text = "Zombie Survival: Redis session storage unavailable after failover grace period.", occurredAtUtc = now }, stoppingToken);
                    if (!response.IsSuccessStatusCode) logger.LogError("Redis alert webhook returned {StatusCode}.", (int)response.StatusCode);
                }
                catch (HttpRequestException) { logger.LogError("Redis alert webhook could not be delivered."); }
                catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
                { logger.LogError("Redis alert webhook timed out."); }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
