using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using zombie_survival_3Dgame_Server.Auth.Models;
using zombie_survival_3Dgame_Server.Data;
using zombie_survival_3Dgame_Server.Contracts.Reward;
using zombie_survival_3Dgame_Server.GameSession.Models;
using zombie_survival_3Dgame_Server.Inventory.Models;
using zombie_survival_3Dgame_Server.Reward;

namespace zombie_survival_3Dgame_Server.Tests.Reward;

public sealed class SurvivalRewardServiceTests
{
    private static readonly DateTime StartedAt = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ClaimAsync_CompletedSession_PersistsGoldAndClaimTogether()
    {
        using var requestCancellation = new CancellationTokenSource();
        var cancellationToken = requestCancellation.Token;
        // Given
        using var context = CreateContext();
        var session = await SeedAsync(context, completed: true, gold: 100, cancellationToken: cancellationToken);
        var service = new SurvivalRewardService(context, TimeProvider.System);

        // When
        var result = await service.ClaimAsync("player-1", CreateRequest(session.Id), cancellationToken);
        context.ChangeTracker.Clear();
        var savedSession = await context.SurvivalGameSessions.SingleAsync(cancellationToken);
        var saveData = await context.PlayerSaveData.SingleAsync(cancellationToken);

        // Then
        result.Status.Should().Be(RewardClaimStatus.Success);
        result.Response!.AwardedGold.Should().Be(550);
        saveData.Gold.Should().Be(650);
        savedSession.AwardedGold.Should().Be(550);
        saveData.Version.Should().Be(1);
    }

    [Fact]
    public async Task ClaimAsync_RepeatedRequest_DoesNotGrantGoldAgain()
    {
        using var requestCancellation = new CancellationTokenSource();
        var cancellationToken = requestCancellation.Token;
        // Given
        using var context = CreateContext();
        var session = await SeedAsync(context, completed: true, cancellationToken: cancellationToken);
        var service = new SurvivalRewardService(context, TimeProvider.System);
        var first = await service.ClaimAsync("player-1", CreateRequest(session.Id), cancellationToken);
        context.ChangeTracker.Clear();

        // When
        var second = await service.ClaimAsync("player-1", CreateRequest(session.Id), cancellationToken);

        // Then
        second.Response!.AlreadyClaimed.Should().BeTrue();
        second.Response.AwardedGold.Should().Be(first.Response!.AwardedGold);
        second.Response.ClaimedAtUtc.Should().Be(first.Response.ClaimedAtUtc);
        (await context.PlayerSaveData.SingleAsync(cancellationToken)).Gold.Should().Be(550);
        (await context.PlayerSaveData.SingleAsync(cancellationToken)).Version.Should().Be(1);
    }

    [Theory]
    [InlineData("player-2", false)]
    [InlineData("player-1", true)]
    public async Task ClaimAsync_WrongOwnerOrUnknownSession_ReturnsNotFound(string playerId, bool unknownSession)
    {
        using var requestCancellation = new CancellationTokenSource();
        var cancellationToken = requestCancellation.Token;
        // Given
        using var context = CreateContext();
        var session = await SeedAsync(context, completed: true, cancellationToken: cancellationToken);
        var service = new SurvivalRewardService(context, TimeProvider.System);

        // When
        var result = await service.ClaimAsync(playerId, CreateRequest(unknownSession ? "unknown" : session.Id), cancellationToken);

        // Then
        result.Status.Should().Be(RewardClaimStatus.NotFound);
        (await context.PlayerSaveData.SingleAsync(cancellationToken)).Gold.Should().Be(0);
        session.ClaimedAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task ClaimAsync_DeletedAccount_ReturnsNotFound()
    {
        using var requestCancellation = new CancellationTokenSource();
        var cancellationToken = requestCancellation.Token;
        // Given
        using var context = CreateContext();
        var session = await SeedAsync(context, completed: true, cancellationToken: cancellationToken);
        context.Users.Remove(await context.Users.SingleAsync(cancellationToken));
        await context.SaveChangesAsync(cancellationToken);
        var service = new SurvivalRewardService(context, TimeProvider.System);

        // When
        var result = await service.ClaimAsync("player-1", CreateRequest(session.Id), cancellationToken);

        // Then
        result.Status.Should().Be(RewardClaimStatus.NotFound);
        (await context.PlayerSaveData.SingleAsync(cancellationToken)).Gold.Should().Be(0);
    }

    [Fact]
    public async Task ClaimAsync_ActiveSession_PersistsReportedStatsAndReward()
    {
        using var requestCancellation = new CancellationTokenSource();
        var cancellationToken = requestCancellation.Token;
        // Given
        using var context = CreateContext();
        var session = await SeedAsync(context, completed: false, cancellationToken: cancellationToken);
        var service = new SurvivalRewardService(context, TimeProvider.System);

        // When
        var result = await service.ClaimAsync("player-1", CreateRequest(session.Id), cancellationToken);

        // Then
        result.Status.Should().Be(RewardClaimStatus.Success);
        (await context.PlayerSaveData.SingleAsync(cancellationToken)).Gold.Should().Be(550);
        session.SurvivalTimeSeconds.Should().Be(100);
        session.ClearWave.Should().Be(2);
        session.ClaimedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task ClaimAsync_GoldLimitExceeded_LeavesSessionUnclaimed()
    {
        using var requestCancellation = new CancellationTokenSource();
        var cancellationToken = requestCancellation.Token;
        // Given
        using var context = CreateContext();
        var session = await SeedAsync(context, completed: false, gold: int.MaxValue, cancellationToken: cancellationToken);
        var service = new SurvivalRewardService(context, TimeProvider.System);

        // When
        var result = await service.ClaimAsync("player-1", CreateRequest(session.Id), cancellationToken);

        // Then
        result.Status.Should().Be(RewardClaimStatus.GoldLimitExceeded);
        (await context.PlayerSaveData.SingleAsync(cancellationToken)).Gold.Should().Be(int.MaxValue);
        session.ClaimedAtUtc.Should().BeNull();
        context.ChangeTracker.HasChanges().Should().BeFalse();
        session.CompletedAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task ClaimAsync_SaveDataMissing_CreatesSaveDataWithReward()
    {
        using var requestCancellation = new CancellationTokenSource();
        var cancellationToken = requestCancellation.Token;
        // Given
        using var context = CreateContext();
        var session = await SeedAsync(context, completed: true, cancellationToken: cancellationToken);
        context.PlayerSaveData.Remove(await context.PlayerSaveData.SingleAsync(cancellationToken));
        await context.SaveChangesAsync(cancellationToken);
        var service = new SurvivalRewardService(context, TimeProvider.System);

        // When
        var result = await service.ClaimAsync("player-1", CreateRequest(session.Id), cancellationToken);

        // Then
        result.Status.Should().Be(RewardClaimStatus.Success);
        (await context.PlayerSaveData.SingleAsync(cancellationToken)).Gold.Should().Be(550);
    }

    [Fact]
    public async Task ClaimAsync_CanceledRequest_DoesNotChangeGold()
    {
        using var requestCancellation = new CancellationTokenSource();
        var cancellationToken = requestCancellation.Token;
        // Given
        using var context = CreateContext();
        var session = await SeedAsync(context, completed: true, cancellationToken: cancellationToken);
        var service = new SurvivalRewardService(context, TimeProvider.System);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        // When
        var act = () => service.ClaimAsync("player-1", CreateRequest(session.Id), cancellation.Token);

        // Then
        await act.Should().ThrowAsync<OperationCanceledException>();
        (await context.PlayerSaveData.SingleAsync(cancellationToken)).Gold.Should().Be(0);
    }

    [Fact]
    public async Task ClaimAsync_ChangedReplay_ReturnsOriginalAwardWithoutChangingStats()
    {
        // Given
        using var cancellation = new CancellationTokenSource();
        var cancellationToken = cancellation.Token;
        using var context = CreateContext();
        var session = await SeedAsync(context, completed: false, cancellationToken: cancellationToken);
        var service = new SurvivalRewardService(context, TimeProvider.System);
        await service.ClaimAsync("player-1", CreateRequest(session.Id), cancellationToken);
        context.ChangeTracker.Clear();
        var changedRequest = new RewardRequest
        {
            SessionId = session.Id, SurvivalTime = 2000, ClearWave = 100, KillZombies = 10000
        };

        // When
        var result = await service.ClaimAsync("player-1", changedRequest, cancellationToken);

        // Then
        result.Response!.AlreadyClaimed.Should().BeTrue();
        result.Response.AwardedGold.Should().Be(550);
        (await context.PlayerSaveData.SingleAsync(cancellationToken)).Gold.Should().Be(550);
        (await context.SurvivalGameSessions.SingleAsync(cancellationToken)).ClearWave.Should().Be(2);
        context.ChangeTracker.HasChanges().Should().BeFalse();
    }

    [Theory]
    [InlineData(-1f, 0, 0)]
    [InlineData(float.NaN, 0, 0)]
    [InlineData(float.PositiveInfinity, 0, 0)]
    [InlineData(float.NegativeInfinity, 0, 0)]
    [InlineData(0f, -1, 0)]
    [InlineData(0f, 0, -1)]
    [InlineData(0f, int.MaxValue, 0)]
    [InlineData(0f, 0, int.MaxValue)]
    public async Task ClaimAsync_InvalidReport_DoesNotCompleteOrAward(
        float survivalTime, int clearWave, int killZombies)
    {
        // Given
        using var cancellation = new CancellationTokenSource();
        var cancellationToken = cancellation.Token;
        using var context = CreateContext();
        var session = await SeedAsync(context, completed: false, cancellationToken: cancellationToken);
        var service = new SurvivalRewardService(context, TimeProvider.System);
        var request = new RewardRequest
        {
            SessionId = session.Id, SurvivalTime = survivalTime, ClearWave = clearWave, KillZombies = killZombies
        };

        // When
        var result = await service.ClaimAsync("player-1", request, cancellationToken);

        // Then
        result.Status.Should().Be(RewardClaimStatus.InvalidInput);
        session.CompletedAtUtc.Should().BeNull();
        session.ClaimedAtUtc.Should().BeNull();
        (await context.PlayerSaveData.SingleAsync(cancellationToken)).Gold.Should().Be(0);
        context.ChangeTracker.HasChanges().Should().BeFalse();
    }

    [Fact]
    public async Task ClaimAsync_ReportAboveTimeLimit_CapsRewardAndPreservesReportedTime()
    {
        // Given
        using var cancellation = new CancellationTokenSource();
        var cancellationToken = cancellation.Token;
        using var context = CreateContext();
        var session = await SeedAsync(context, completed: false, cancellationToken: cancellationToken);
        var service = new SurvivalRewardService(context, TimeProvider.System);
        var request = new RewardRequest
        {
            SessionId = session.Id, SurvivalTime = 3000, ClearWave = 2, KillZombies = 10
        };

        // When
        var result = await service.ClaimAsync("player-1", request, cancellationToken);

        // Then
        result.Response!.AwardedGold.Should().Be(6250);
        session.SurvivalTimeSeconds.Should().Be(3000);
    }

    [Fact]
    public async Task ClaimAsync_ConcurrentStaleRequest_RejectsSecondAward()
    {
        // Given
        using var cancellation = new CancellationTokenSource();
        var cancellationToken = cancellation.Token;
        var options = new DbContextOptionsBuilder<GameDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
        using var first = new GameDbContext(options);
        var session = await SeedAsync(first, completed: false, cancellationToken: cancellationToken);
        using var second = new GameDbContext(options);
        await second.SurvivalGameSessions.SingleAsync(cancellationToken);
        await second.PlayerSaveData.SingleAsync(cancellationToken);
        var request = CreateRequest(session.Id);
        await new SurvivalRewardService(first, TimeProvider.System).ClaimAsync("player-1", request, cancellationToken);
        var staleService = new SurvivalRewardService(second, TimeProvider.System);

        // When
        var act = () => staleService.ClaimAsync("player-1", request, cancellationToken);

        // Then
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
        first.ChangeTracker.Clear();
        (await first.PlayerSaveData.SingleAsync(cancellationToken)).Gold.Should().Be(550);
        (await first.SurvivalGameSessions.SingleAsync(cancellationToken)).AwardedGold.Should().Be(550);
    }

    private static RewardRequest CreateRequest(string sessionId) => new()
    {
        SessionId = sessionId, SurvivalTime = 100, ClearWave = 2, KillZombies = 10
    };

    private static GameDbContext CreateContext() => new(
        new DbContextOptionsBuilder<GameDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);

    private static async Task<SurvivalGameSession> SeedAsync(GameDbContext context, bool completed, int gold = 0, CancellationToken cancellationToken = default)
    {
        context.Users.Add(new AppUser { Id = "player-1" });
        context.PlayerSaveData.Add(new PlayerSaveData { PlayerId = "player-1", Gold = gold });
        var session = SurvivalGameSession.Start("player-1", StartedAt);
        if (completed)
        {
            session.Complete(StartedAt.AddSeconds(100), 2, 10);
        }

        context.SurvivalGameSessions.Add(session);
        await context.SaveChangesAsync(cancellationToken);
        return session;
    }
}
