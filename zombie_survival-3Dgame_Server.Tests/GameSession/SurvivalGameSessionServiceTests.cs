using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using zombie_survival_3Dgame_Server.Auth.Models;
using zombie_survival_3Dgame_Server.Data;
using zombie_survival_3Dgame_Server.GameSession;
using zombie_survival_3Dgame_Server.GameSession.Models;
using zombie_survival_3Dgame_Server.Reward;
using zombie_survival_3Dgame_Server.Contracts.Reward;

namespace zombie_survival_3Dgame_Server.Tests.GameSession;

public sealed class SurvivalGameSessionServiceTests
{
    [Fact]
    public async Task StartAsync_ExistingActiveSession_ReturnsSameSession()
    {
        // Given
        using var requestCancellation = new CancellationTokenSource();
        var cancellationToken = requestCancellation.Token;
        using var context = CreateContext();
        context.Users.Add(new AppUser { Id = "player-1" });
        await context.SaveChangesAsync(cancellationToken);
        var clock = new ManualTimeProvider();
        var service = new SurvivalGameSessionService(context, clock);
        var first = await service.StartAsync("player-1", cancellationToken);

        // When
        var second = await service.StartAsync("player-1", cancellationToken);

        // Then
        second!.SessionId.Should().Be(first!.SessionId);
        (await context.SurvivalGameSessions.CountAsync(cancellationToken)).Should().Be(1);
    }

    [Fact]
    public async Task StartAsync_AccountMissing_DoesNotCreateSession()
    {
        // Given
        using var requestCancellation = new CancellationTokenSource();
        var cancellationToken = requestCancellation.Token;
        using var context = CreateContext();
        var service = new SurvivalGameSessionService(context, new ManualTimeProvider());

        // When
        var result = await service.StartAsync("unknown", cancellationToken);

        // Then
        result.Should().BeNull();
        (await context.SurvivalGameSessions.CountAsync(cancellationToken)).Should().Be(0);
    }

    [Fact]
    public async Task CompleteAsync_ServerClock_RecordsElapsedTimeAndAllowsReward()
    {
        // Given
        using var requestCancellation = new CancellationTokenSource();
        var cancellationToken = requestCancellation.Token;
        using var context = CreateContext();
        context.Users.Add(new AppUser { Id = "player-1" });
        await context.SaveChangesAsync(cancellationToken);
        var clock = new ManualTimeProvider();
        var service = new SurvivalGameSessionService(context, clock);
        var started = await service.StartAsync("player-1", cancellationToken);
        clock.Advance(TimeSpan.FromSeconds(100.25));

        // When
        var completed = await service.CompleteAsync("player-1", started!.SessionId, 2, 10, cancellationToken);
        var reward = await new SurvivalRewardService(context, clock)
            .ClaimAsync("player-1", new RewardRequest { SessionId = started.SessionId, SurvivalTime = 0, ClearWave = 0, KillZombies = 0 }, cancellationToken);

        // Then
        completed!.SurvivalTimeSeconds.Should().Be(100.25f);
        completed.CompletedAtUtc.Should().Be(clock.GetUtcNow().UtcDateTime);
        reward.Response!.AwardedGold.Should().Be(550);
        (await context.SurvivalGameSessions.SingleAsync(cancellationToken)).ActivePlayerId.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_OtherPlayer_CannotReadSession()
    {
        // Given
        using var requestCancellation = new CancellationTokenSource();
        var cancellationToken = requestCancellation.Token;
        using var context = CreateContext();
        var session = SurvivalGameSession.Start("player-1", DateTime.UtcNow);
        context.SurvivalGameSessions.Add(session);
        await context.SaveChangesAsync(cancellationToken);
        var service = new SurvivalGameSessionService(context, new ManualTimeProvider());

        // When
        var result = await service.GetAsync("player-2", session.Id, cancellationToken);

        // Then
        result.Should().BeNull();
    }

    [Fact]
    public async Task CompleteAsync_OtherPlayer_DoesNotChangeSession()
    {
        // Given
        using var requestCancellation = new CancellationTokenSource();
        var cancellationToken = requestCancellation.Token;
        using var context = CreateContext();
        var clock = new ManualTimeProvider();
        var session = SurvivalGameSession.Start("player-1", clock.GetUtcNow().UtcDateTime);
        context.SurvivalGameSessions.Add(session);
        await context.SaveChangesAsync(cancellationToken);
        var service = new SurvivalGameSessionService(context, clock);

        // When
        var result = await service.CompleteAsync("player-2", session.Id, 100, 10000, cancellationToken);

        // Then
        result.Should().BeNull();
        session.CompletedAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task CompleteAsync_RepeatedCompletion_RejectsResultChanges()
    {
        // Given
        using var requestCancellation = new CancellationTokenSource();
        var cancellationToken = requestCancellation.Token;
        using var context = CreateContext();
        var clock = new ManualTimeProvider();
        var session = SurvivalGameSession.Start("player-1", clock.GetUtcNow().UtcDateTime);
        context.SurvivalGameSessions.Add(session);
        await context.SaveChangesAsync(cancellationToken);
        var service = new SurvivalGameSessionService(context, clock);
        await service.CompleteAsync("player-1", session.Id, 2, 10, cancellationToken);

        // When
        var act = () => service.CompleteAsync("player-1", session.Id, 100, 10000, cancellationToken);

        // Then
        await act.Should().ThrowAsync<InvalidOperationException>();
        session.ClearWave.Should().Be(2);
        session.KillZombies.Should().Be(10);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    public async Task CompleteAsync_InvalidServerResult_DoesNotCompleteSession(int clearWave, int killZombies)
    {
        // Given
        using var requestCancellation = new CancellationTokenSource();
        var cancellationToken = requestCancellation.Token;
        using var context = CreateContext();
        var clock = new ManualTimeProvider();
        var session = SurvivalGameSession.Start("player-1", clock.GetUtcNow().UtcDateTime);
        context.SurvivalGameSessions.Add(session);
        await context.SaveChangesAsync(cancellationToken);
        var service = new SurvivalGameSessionService(context, clock);

        // When
        var act = () => service.CompleteAsync("player-1", session.Id, clearWave, killZombies, cancellationToken);

        // Then
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
        session.CompletedAtUtc.Should().BeNull();
        session.Version.Should().Be(0);
    }

    [Fact]
    public async Task SaveChangesAsync_StaleSessionClaim_RejectsConcurrencyConflict()
    {
        // Given
        using var requestCancellation = new CancellationTokenSource();
        var cancellationToken = requestCancellation.Token;
        var options = new DbContextOptionsBuilder<GameDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
        using var first = new GameDbContext(options);
        var session = SurvivalGameSession.Start("player-1", DateTime.UtcNow);
        session.Complete(session.StartedAtUtc.AddSeconds(100), 2, 10);
        first.SurvivalGameSessions.Add(session);
        await first.SaveChangesAsync(cancellationToken);
        using var second = new GameDbContext(options);
        var staleSession = await second.SurvivalGameSessions.SingleAsync(cancellationToken);
        session.Claim(550, DateTime.UtcNow);
        await first.SaveChangesAsync(cancellationToken);

        // When
        staleSession.Claim(550, DateTime.UtcNow);
        var act = () => second.SaveChangesAsync(cancellationToken);

        // Then
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
        first.ChangeTracker.Clear();
        (await first.SurvivalGameSessions.SingleAsync(cancellationToken)).Version.Should().Be(2);
    }

    [Fact]
    public void Model_ActiveSession_HasUniquePlayerIndexAndConcurrencyVersion()
    {
        // Given
        using var context = CreateContext();

        // When
        var entity = context.Model.FindEntityType(typeof(SurvivalGameSession))!;
        var activeIndex = entity.GetIndexes().Single(x => x.Properties.Count == 1 && x.Properties[0].Name == "ActivePlayerId");

        // Then
        activeIndex.IsUnique.Should().BeTrue();
        entity.FindProperty("Version")!.IsConcurrencyToken.Should().BeTrue();
    }

    private static GameDbContext CreateContext() => new(
        new DbContextOptionsBuilder<GameDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan duration) => _now += duration;
    }
}
