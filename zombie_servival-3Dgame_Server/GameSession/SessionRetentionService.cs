using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using zombie_survival_3Dgame_Server.Data;
using zombie_survival_3Dgame_Server.Options;

namespace zombie_survival_3Dgame_Server.GameSession;

public sealed class SessionRetentionService(IServiceScopeFactory scopes, TimeProvider clock,
    IOptions<RedisSessionOptions> options, ILogger<SessionRetentionService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(10), clock);
        try
        {
            do
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
                    var cutoff = clock.GetUtcNow().UtcDateTime.AddDays(-options.Value.RetentionDays);
                    var removed = await db.SurvivalGameSessions
                        .Where(x => (x.CompletedAtUtc != null && x.CompletedAtUtc <= cutoff)
                            || (x.CompletedAtUtc == null && x.StartedAtUtc <= cutoff))
                        .ExecuteDeleteAsync(stoppingToken);
                    if (removed > 0) logger.LogInformation("Removed {Count} expired sessions.", removed);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
                catch (Exception) { logger.LogError("Session retention cleanup failed; it will be retried."); }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
