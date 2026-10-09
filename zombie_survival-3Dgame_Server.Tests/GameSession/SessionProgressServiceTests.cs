using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using zombie_survival_3Dgame_Server.Contracts.GameSession;
using zombie_survival_3Dgame_Server.Data;
using zombie_survival_3Dgame_Server.GameSession.Models;
using zombie_survival_3Dgame_Server.GameSession.Progress;

namespace zombie_survival_3Dgame_Server.Tests.GameSession;

public sealed class SessionProgressServiceTests
{
    [Theory]
    [InlineData("other", false, false, ProgressStatus.NotFound)]
    [InlineData("player", true, false, ProgressStatus.Conflict)]
    [InlineData("player", false, true, ProgressStatus.NotFound)]
    public async Task RecordWaveAsync_NotOwnedActiveSession_DoesNotWriteRedis(
        string owner, bool completed, bool expired, ProgressStatus expected)
    {
        // Given
        using var cancellation = new CancellationTokenSource();
        using var db = new GameDbContext(new DbContextOptionsBuilder<GameDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var start = new DateTime(2026, 10, 8, 0, 0, 0, DateTimeKind.Utc);
        var session = SurvivalGameSession.Start("player", start);
        if (completed) session.Complete(start.AddSeconds(100), 1, 10);
        db.SurvivalGameSessions.Add(session);
        await db.SaveChangesAsync(cancellation.Token);
        var clock = new SessionTestServices.FixedClock(new DateTimeOffset(expired ? start.AddDays(3) : start.AddSeconds(100)));
        var store = SessionTestServices.Store();
        var service = new SessionProgressService(db, new SessionProgressValidator(store, clock, SessionTestServices.Settings), clock, SessionTestServices.Settings);
        // When
        var result = await service.RecordWaveAsync(owner, session.Id, new WaveProgressRequest
            { ClearWave = 1, KillZombies = 10, SurvivalTimeSeconds = 100 }, cancellation.Token);
        // Then
        result.Status.Should().Be(expected);
        await store.DidNotReceive().UpdateAsync(Arg.Any<SurvivalGameSession>(), Arg.Any<SessionProgress>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void StartAsync_SubMicrosecondTimestamp_UsesMySqlPrecision()
    {
        // Given
        var start = new DateTime(2026, 10, 8, 0, 0, 0, DateTimeKind.Utc).AddTicks(17);
        // When
        var session = SurvivalGameSession.Start("player", start);
        // Then
        session.StartedAtUtc.Ticks.Should().Be(start.Ticks - 7);
    }
}
