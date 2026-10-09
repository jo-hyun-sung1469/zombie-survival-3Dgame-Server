using FluentAssertions;
using NSubstitute;
using zombie_survival_3Dgame_Server.GameSession.Models;
using zombie_survival_3Dgame_Server.GameSession.Progress;

namespace zombie_survival_3Dgame_Server.Tests.GameSession;

public sealed class SessionProgressValidatorTests
{
    private static readonly DateTime Start = new(2026, 10, 8, 0, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(94f)]
    [InlineData(106f)]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public async Task ValidateAsync_InvalidReportedTime_DoesNotWriteRedis(float reportedTime)
    {
        // Given
        using var cancellation = new CancellationTokenSource();
        var store = SessionTestServices.Store();
        var validator = Create(store);
        var session = SurvivalGameSession.Start("player", Start);
        // When
        var result = await validator.ValidateAsync(session, reportedTime, 1, 20, false, cancellation.Token);
        // Then
        result.Status.Should().Be(ProgressStatus.InvalidInput);
        await store.DidNotReceive().UpdateAsync(Arg.Any<SurvivalGameSession>(), Arg.Any<SessionProgress>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ValidateAsync_NetworkDelay_StoresServerElapsedTime()
    {
        // Given
        using var cancellation = new CancellationTokenSource();
        var store = SessionTestServices.Store();
        var validator = Create(store);
        var session = SurvivalGameSession.Start("player", Start);
        // When
        var result = await validator.ValidateAsync(session, 97, 1, 20, false, cancellation.Token);
        // Then
        result.Progress!.SurvivalTimeSeconds.Should().Be(100);
        await store.Received(1).UpdateAsync(session, Arg.Is<SessionProgress>(x => x != null && x.SurvivalTimeSeconds == 100), false, cancellation.Token);
    }

    [Theory]
    [InlineData(ProgressStatus.Missing)]
    [InlineData(ProgressStatus.Conflict)]
    public async Task ValidateAsync_StoreRejects_DoesNotUseSubmittedResult(ProgressStatus status)
    {
        // Given
        using var cancellation = new CancellationTokenSource();
        var store = SessionTestServices.Store();
        store.UpdateAsync(Arg.Any<SurvivalGameSession>(), Arg.Any<SessionProgress>(), true, Arg.Any<CancellationToken>())
            .Returns(new ProgressResult(status));
        // When
        var result = await Create(store).ValidateAsync(SurvivalGameSession.Start("player", Start), 100, 3, 30, true, cancellation.Token);
        // Then
        result.Status.Should().Be(status);
        result.Progress.Should().BeNull();
    }

    [Fact]
    public async Task ValidateAsync_FrozenResultRetry_IgnoresLaterChangedSubmission()
    {
        // Given
        using var cancellation = new CancellationTokenSource();
        var store = SessionTestServices.Store();
        var frozen = new SessionProgress(80, 1, 20, Start.AddSeconds(80));
        store.GetFinalAsync(Arg.Any<SurvivalGameSession>(), Arg.Any<CancellationToken>()).Returns(frozen);
        // When
        var result = await Create(store).ValidateAsync(SurvivalGameSession.Start("player", Start), 999, 99, 99999, true, cancellation.Token);
        // Then
        result.Progress.Should().Be(frozen);
        await store.DidNotReceive().UpdateAsync(Arg.Any<SurvivalGameSession>(), Arg.Any<SessionProgress>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    private static SessionProgressValidator Create(IGameSessionProgressStore store) => new(store,
        new SessionTestServices.FixedClock(new DateTimeOffset(Start.AddSeconds(100))), SessionTestServices.Settings);
}
