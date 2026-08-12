using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using zombie_survival_3Dgame_Server.Contracts.Inventory;
using zombie_survival_3Dgame_Server.Inventory;

namespace zombie_survival_3Dgame_Server.Tests.Inventory;

public sealed class InventoryControllerTests
{
    [Fact]
    public async Task GetMyDataAsync_AuthenticatedPlayer_UsesClaimIdentity()
    {
        // Given
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var cancellationToken = cancellationTokenSource.Token;
        var service = Substitute.For<IInventoryService>();
        var response = new PlayerSaveResponse
        {
            PlayerId = "player-1",
            Gold = 100,
            WeaponStates = new Dictionary<string, bool>(),
            Weapons = [],
            UpdatedAtUtc = DateTime.UtcNow
        };
        service.GetByPlayerIdAsync("player-1", Arg.Any<CancellationToken>())
            .Returns(response);
        var controller = new InventoryController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(
                        new ClaimsIdentity([new Claim("userId", "player-1")], "test"))
                }
            }
        };

        // When
        var result = await controller.GetMyDataAsync(cancellationToken);

        // Then
        result.Result.Should().BeOfType<OkObjectResult>();
        await service.Received(1).GetByPlayerIdAsync("player-1", Arg.Any<CancellationToken>());
    }
}
