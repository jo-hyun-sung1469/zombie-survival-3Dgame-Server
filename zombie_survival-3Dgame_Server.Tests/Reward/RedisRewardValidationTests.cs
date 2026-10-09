using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using zombie_survival_3Dgame_Server.Auth.Models;
using zombie_survival_3Dgame_Server.Contracts.Reward;
using zombie_survival_3Dgame_Server.Data;
using zombie_survival_3Dgame_Server.GameSession.Models;
using zombie_survival_3Dgame_Server.GameSession.Progress;
using zombie_survival_3Dgame_Server.Inventory.Models;
using zombie_survival_3Dgame_Server.Reward;
using zombie_survival_3Dgame_Server.Tests.GameSession;

namespace zombie_survival_3Dgame_Server.Tests.Reward;

public sealed class RedisRewardValidationTests
{
    private static readonly DateTime Start = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(ProgressStatus.Missing)]
    [InlineData(ProgressStatus.Conflict)]
    public async Task ClaimAsync_RedisRejects_DoesNotCompleteOrPay(ProgressStatus status)
    {
        // Given
        using var cancellation = new CancellationTokenSource();
        using var db = CreateDb();
        var session = await SeedAsync(db, cancellation.Token);
        var store = SessionTestServices.Store();
        store.UpdateAsync(Arg.Any<SurvivalGameSession>(), Arg.Any<SessionProgress>(), true, Arg.Any<CancellationToken>()).Returns(new ProgressResult(status));
        // When
        var result = await SessionTestServices.Reward(db, store: store).ClaimAsync("player", Request(session.Id), cancellation.Token);
        // Then
        result.Status.Should().Be(RewardClaimStatus.ProgressConflict);
        session.CompletedAtUtc.Should().BeNull();
        (await db.PlayerSaveData.SingleAsync(cancellation.Token)).Gold.Should().Be(0);
    }

    [Fact]
    public async Task ClaimAsync_RedisDown_DoesNotFallBackToClientStats()
    {
        // Given
        using var cancellation = new CancellationTokenSource();
        using var db = CreateDb();
        var session = await SeedAsync(db, cancellation.Token);
        var store = SessionTestServices.Store();
        store.GetFinalAsync(Arg.Any<SurvivalGameSession>(), Arg.Any<CancellationToken>()).ThrowsAsync(new RedisSessionUnavailableException(new IOException()));
        // When
        var act = () => SessionTestServices.Reward(db, store: store).ClaimAsync("player", Request(session.Id), cancellation.Token);
        // Then
        await act.Should().ThrowAsync<RedisSessionUnavailableException>();
        session.ClaimedAtUtc.Should().BeNull();
        (await db.PlayerSaveData.SingleAsync(cancellation.Token)).Gold.Should().Be(0);
    }

    [Fact]
    public async Task ClaimAsync_DurableFinalResultAndRedisDown_PaysOnceUsingSql()
    {
        // Given
        using var cancellation = new CancellationTokenSource();
        using var db = CreateDb();
        var session = await SeedAsync(db, cancellation.Token);
        session.Complete(Start.AddSeconds(100), 1, 10);
        await db.SaveChangesAsync(cancellation.Token);
        var store = SessionTestServices.Store();
        store.FinalizeTtlAsync(Arg.Any<SurvivalGameSession>(), Arg.Any<CancellationToken>()).ThrowsAsync(new RedisSessionUnavailableException(new IOException()));
        var service = SessionTestServices.Reward(db, store: store);
        // When
        var first = await service.ClaimAsync("player", Request(session.Id), cancellation.Token);
        var replay = await service.ClaimAsync("player", Request(session.Id), cancellation.Token);
        // Then
        first.Response!.AwardedGold.Should().Be(450);
        replay.Response!.AlreadyClaimed.Should().BeTrue();
        (await db.PlayerSaveData.SingleAsync(cancellation.Token)).Gold.Should().Be(450);
        await store.DidNotReceive().GetFinalAsync(Arg.Any<SurvivalGameSession>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClaimAsync_GoldLimitThenRedisExpires_RetriesFrozenSqlResult()
    {
        // Given
        using var cancellation = new CancellationTokenSource();
        using var db = CreateDb();
        var session = await SeedAsync(db, cancellation.Token);
        var save = await db.PlayerSaveData.SingleAsync(cancellation.Token);
        save.Gold = int.MaxValue;
        await db.SaveChangesAsync(cancellation.Token);
        var store = SessionTestServices.Store();
        var service = SessionTestServices.Reward(db, store: store);
        var first = await service.ClaimAsync("player", Request(session.Id), cancellation.Token);
        save.Gold = 0;
        await db.SaveChangesAsync(cancellation.Token);
        store.GetFinalAsync(Arg.Any<SurvivalGameSession>(), Arg.Any<CancellationToken>()).ThrowsAsync(new RedisSessionUnavailableException(new IOException()));
        // When
        var result = await service.ClaimAsync("player", new RewardRequest
            { SessionId = session.Id, SurvivalTime = 2000, ClearWave = 99, KillZombies = 999 }, cancellation.Token);
        // Then
        first.Status.Should().Be(RewardClaimStatus.GoldLimitExceeded);
        result.Response!.AwardedGold.Should().Be(450);
        session.ClearWave.Should().Be(1);
        save.Gold.Should().Be(450);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ClaimAsync_ExpiredSession_RejectsEvenBeforeCleanup(bool completed)
    {
        // Given
        using var cancellation = new CancellationTokenSource();
        using var db = CreateDb();
        var session = await SeedAsync(db, cancellation.Token);
        if (completed) session.Complete(Start.AddSeconds(100), 1, 10);
        await db.SaveChangesAsync(cancellation.Token);
        var clock = new SessionTestServices.FixedClock(new DateTimeOffset(Start.AddDays(3).AddSeconds(100)));
        // When
        var result = await SessionTestServices.Reward(db, clock).ClaimAsync("player", Request(session.Id), cancellation.Token);
        // Then
        result.Status.Should().Be(RewardClaimStatus.NotFound);
        (await db.PlayerSaveData.SingleAsync(cancellation.Token)).Gold.Should().Be(0);
    }

    private static RewardRequest Request(string id) => new() { SessionId = id, SurvivalTime = 100, ClearWave = 1, KillZombies = 10 };
    private static GameDbContext CreateDb() => new(new DbContextOptionsBuilder<GameDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static async Task<SurvivalGameSession> SeedAsync(GameDbContext db, CancellationToken cancellationToken)
    {
        db.Users.Add(new AppUser { Id = "player" });
        db.PlayerSaveData.Add(new PlayerSaveData { PlayerId = "player" });
        var session = SurvivalGameSession.Start("player", Start);
        db.SurvivalGameSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);
        return session;
    }
}
