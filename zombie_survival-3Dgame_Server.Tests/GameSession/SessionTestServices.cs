using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using zombie_survival_3Dgame_Server.Data;
using zombie_survival_3Dgame_Server.GameSession;
using zombie_survival_3Dgame_Server.GameSession.Models;
using zombie_survival_3Dgame_Server.GameSession.Progress;
using zombie_survival_3Dgame_Server.Options;
using zombie_survival_3Dgame_Server.Reward;

namespace zombie_survival_3Dgame_Server.Tests.GameSession;

internal static class SessionTestServices
{
    public static IOptions<RedisSessionOptions> Settings => Microsoft.Extensions.Options.Options.Create(new RedisSessionOptions());

    public static IGameSessionProgressStore Store()
    {
        var store = Substitute.For<IGameSessionProgressStore>();
        store.UpdateAsync(Arg.Any<SurvivalGameSession>(), Arg.Any<SessionProgress>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(call => new ProgressResult(ProgressStatus.Success, call.Arg<SessionProgress>()));
        return store;
    }

    public static SurvivalGameSessionService Session(GameDbContext db, TimeProvider clock) => new(db, clock, Store(), Settings);

    public static SurvivalRewardService Reward(GameDbContext db, TimeProvider? clock = null, IGameSessionProgressStore? store = null)
    {
        clock ??= new FixedClock(new DateTimeOffset(2026, 10, 1, 0, 1, 40, TimeSpan.Zero));
        store ??= Store();
        return new(db, clock, new SessionProgressValidator(store, clock, Settings), store, Settings,
            NullLogger<SurvivalRewardService>.Instance);
    }

    internal sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
