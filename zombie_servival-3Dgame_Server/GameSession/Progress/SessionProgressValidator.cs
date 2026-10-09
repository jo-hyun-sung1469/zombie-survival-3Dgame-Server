using Microsoft.Extensions.Options;
using zombie_survival_3Dgame_Server.GameSession.Models;
using zombie_survival_3Dgame_Server.Options;
using zombie_survival_3Dgame_Server.Reward;

namespace zombie_survival_3Dgame_Server.GameSession.Progress;

public sealed class SessionProgressValidator(
    IGameSessionProgressStore store, TimeProvider clock, IOptions<RedisSessionOptions> options)
{
    public async Task<ProgressResult> ValidateAsync(SurvivalGameSession session, float reportedTime, int wave, int kills,
        bool final, CancellationToken cancellationToken)
    {
        if (final && await store.GetFinalAsync(session, cancellationToken) is { } frozen)
            return new(ProgressStatus.Success, frozen);
        var now = clock.GetUtcNow().UtcDateTime;
        var elapsed = (now - session.StartedAtUtc).TotalSeconds;
        if (!float.IsFinite(reportedTime) || reportedTime < 0 || wave < 0 || kills < 0 || elapsed < 0
            || Math.Abs(elapsed - reportedTime) > options.Value.TimeToleranceSeconds)
            return new(ProgressStatus.InvalidInput);
        try { _ = RewardCalculator.Calculate((float)elapsed, wave, kills); }
        catch (OverflowException) { return new(ProgressStatus.InvalidInput); }
        catch (ArgumentOutOfRangeException) { return new(ProgressStatus.InvalidInput); }
        return await store.UpdateAsync(session, new SessionProgress((float)elapsed, wave, kills, now), final, cancellationToken);
    }
}
