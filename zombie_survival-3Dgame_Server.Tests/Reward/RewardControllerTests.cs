using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using zombie_survival_3Dgame_Server.Reward;
using zombie_survival_3Dgame_Server.Contracts.Reward;

namespace zombie_survival_3Dgame_Server.Tests.Reward;

public sealed class RewardControllerTests
{
    [Theory]
    [InlineData(RewardClaimStatus.Success, 200)]
    [InlineData(RewardClaimStatus.NotFound, 404)]
    [InlineData(RewardClaimStatus.InvalidInput, 400)]
    [InlineData(RewardClaimStatus.GoldLimitExceeded, 409)]
    public async Task SurvivalRewardAsync_AuthenticatedPlayer_UsesClaimAndMapsStatus(
        RewardClaimStatus status, int expectedStatus)
    {
        // Given
        var service = Substitute.For<ISurvivalRewardService>();
        var request = new RewardRequest { SessionId = new string('a', 32), SurvivalTime = 100, ClearWave = 2, KillZombies = 10 };
        using var cancellation = new CancellationTokenSource();
        service.ClaimAsync("player-1", request, cancellation.Token)
            .Returns(new RewardClaimResult { Status = status });
        var controller = CreateController(service, "player-1");

        // When
        var result = await controller.SurvivalRewardAsync(request, cancellation.Token);

        // Then
        ((ObjectResult)result.Result!).StatusCode.Should().Be(expectedStatus);
        await service.Received(1).ClaimAsync("player-1", request, cancellation.Token);
    }

    [Fact]
    public async Task SurvivalRewardAsync_MissingClaim_DoesNotCallService()
    {
        // Given
        var service = Substitute.For<ISurvivalRewardService>();
        var request = new RewardRequest { SessionId = new string('a', 32), SurvivalTime = 100, ClearWave = 2, KillZombies = 10 };
        var controller = CreateController(service, null);
        using var requestCancellation = new CancellationTokenSource();
        var cancellationToken = requestCancellation.Token;

        // When
        var result = await controller.SurvivalRewardAsync(request, cancellationToken);

        // Then
        ((ObjectResult)result.Result!).StatusCode.Should().Be(401);
        await service.DidNotReceive().ClaimAsync(Arg.Any<string>(), Arg.Any<RewardRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Controller_RewardEndpoint_RequiresAuthorization()
    {
        // Given
        var controllerType = typeof(RewardController);

        // When
        var attribute = Attribute.GetCustomAttribute(controllerType, typeof(AuthorizeAttribute));

        // Then
        attribute.Should().NotBeNull();
    }

    private static RewardController CreateController(ISurvivalRewardService service, string? playerId) => new(service)
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
