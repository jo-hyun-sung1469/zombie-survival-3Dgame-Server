using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using zombie_survival_3Dgame_Server.GameSession.Progress;
using zombie_survival_3Dgame_Server.Contracts.GameSession;
using zombie_survival_3Dgame_Server.GameSession;

namespace zombie_survival_3Dgame_Server.Tests.GameSession;

public sealed class GameSessionControllerTests
{
    [Fact]
    public async Task RecordWaveAsync_MissingClaim_DoesNotCallProgressService()
    {
        // Given
        using var cancellation = new CancellationTokenSource();
        var progress = Substitute.For<ISessionProgressService>();
        var controller = new GameSessionController(Substitute.For<IGameSessionService>(), progress)
            { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        // When
        var result = await controller.RecordWaveAsync("session", new WaveProgressRequest
            { ClearWave = 1, KillZombies = 10, SurvivalTimeSeconds = 100 }, cancellation.Token);
        // Then
        ((ObjectResult)result.Result!).StatusCode.Should().Be(401);
        await progress.DidNotReceive().RecordWaveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<WaveProgressRequest>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(ProgressStatus.Success, 200)]
    [InlineData(ProgressStatus.NotFound, 404)]
    [InlineData(ProgressStatus.InvalidInput, 400)]
    [InlineData(ProgressStatus.Conflict, 409)]
    [InlineData(ProgressStatus.Missing, 409)]
    public async Task RecordWaveAsync_AuthenticatedPlayer_UsesClaimAndMapsStatus(ProgressStatus status, int expectedStatus)
    {
        // Given
        using var cancellation = new CancellationTokenSource();
        var progress = Substitute.For<ISessionProgressService>();
        var request = new WaveProgressRequest { ClearWave = 1, KillZombies = 10, SurvivalTimeSeconds = 100 };
        progress.RecordWaveAsync("player-1", "session", request, cancellation.Token)
            .Returns(new ProgressResult(status, status == ProgressStatus.Success
                ? new SessionProgress(100, 1, 10, DateTime.UtcNow) : null));
        var controller = new GameSessionController(Substitute.For<IGameSessionService>(), progress)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext
                { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("userId", "player-1")], "test")) } }
        };
        // When
        var result = await controller.RecordWaveAsync("session", request, cancellation.Token);
        // Then
        ((ObjectResult)result.Result!).StatusCode.Should().Be(expectedStatus);
        await progress.Received(1).RecordWaveAsync("player-1", "session", request, cancellation.Token);
    }

    [Theory]
    [InlineData(true, 200)]
    [InlineData(false, 404)]
    public async Task GetAsync_AuthenticatedPlayer_UsesClaimAndMapsStatus(bool exists, int expectedStatus)
    {
        // Given
        using var cancellation = new CancellationTokenSource();
        var service = Substitute.For<IGameSessionService>();
        service.GetAsync("player-1", "session-1", cancellation.Token).Returns(exists
            ? new GameSessionResponse
            {
                SessionId = "session-1", StartedAtUtc = DateTime.UtcNow,
                SurvivalTimeSeconds = 0, ClearWave = 0, KillZombies = 0
            }
            : null);
        var controller = CreateController(service, "player-1");

        // When
        var result = await controller.GetAsync("session-1", cancellation.Token);

        // Then
        ((ObjectResult)result.Result!).StatusCode.Should().Be(expectedStatus);
        await service.Received(1).GetAsync("player-1", "session-1", cancellation.Token);
    }

    [Fact]
    public async Task GetAsync_MissingClaim_DoesNotCallService()
    {
        // Given
        using var requestCancellation = new CancellationTokenSource();
        var cancellationToken = requestCancellation.Token;
        var service = Substitute.For<IGameSessionService>();
        var controller = CreateController(service, null);

        // When
        var result = await controller.GetAsync("session-1", cancellationToken);

        // Then
        ((ObjectResult)result.Result!).StatusCode.Should().Be(401);
        await service.DidNotReceive().GetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(true, 200)]
    [InlineData(false, 404)]
    public async Task StartAsync_AuthenticatedPlayer_UsesClaimAndMapsStatus(bool exists, int expectedStatus)
    {
        // Given
        using var cancellation = new CancellationTokenSource();
        var service = Substitute.For<IGameSessionService>();
        service.StartAsync("player-1", cancellation.Token).Returns(exists
            ? new GameSessionResponse
            {
                SessionId = new string('a', 32), StartedAtUtc = DateTime.UtcNow,
                SurvivalTimeSeconds = 0, ClearWave = 0, KillZombies = 0
            }
            : null);
        var controller = CreateController(service, "player-1");

        // When
        var result = await controller.StartAsync(cancellation.Token);

        // Then
        ((ObjectResult)result.Result!).StatusCode.Should().Be(expectedStatus);
        await service.Received(1).StartAsync("player-1", cancellation.Token);
    }

    [Fact]
    public async Task StartAsync_MissingClaim_DoesNotCallService()
    {
        // Given
        using var cancellation = new CancellationTokenSource();
        var service = Substitute.For<IGameSessionService>();
        var controller = CreateController(service, null);

        // When
        var result = await controller.StartAsync(cancellation.Token);

        // Then
        ((ObjectResult)result.Result!).StatusCode.Should().Be(401);
        await service.DidNotReceive().StartAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private static GameSessionController CreateController(IGameSessionService service, string? playerId) => new(service, Substitute.For<ISessionProgressService>())
    {
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    playerId is null ? [] : new[] { new Claim("userId", playerId) }, "test"))
            }
        }
    };
}
