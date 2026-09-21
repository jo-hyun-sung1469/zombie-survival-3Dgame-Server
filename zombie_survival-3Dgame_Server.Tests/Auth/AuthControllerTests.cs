using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using zombie_survival_3Dgame_Server.Auth;
using zombie_survival_3Dgame_Server.Contracts.Auth;
using zombie_survival_3Dgame_Server.Inventory;

namespace zombie_survival_3Dgame_Server.Tests.Auth;

public sealed class AuthControllerTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetUserNameAvailabilityAsync_ValidQuery_ReturnsAvailabilityAndPassesCancellation(bool isAvailable)
    {
        // Given
        using var cancellation = new CancellationTokenSource();
        var service = Substitute.For<IAuthService>();
        service.IsUserNameAvailableAsync("ㅋㅋㅋ", cancellation.Token).Returns(isAvailable);
        var controller = CreateController(service);

        // When
        var result = await controller.GetUserNameAvailabilityAsync(
            new UserNameAvailabilityRequest { UserName = "ㅋㅋㅋ" }, cancellation.Token);

        // Then
        var response = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        response.Value.Should().BeEquivalentTo(new UserNameAvailabilityResponse
        {
            UserName = "ㅋㅋㅋ", IsAvailable = isAvailable
        });
        await service.Received(1).IsUserNameAvailableAsync("ㅋㅋㅋ", cancellation.Token);
    }

    [Theory]
    [InlineData(RegisterStatus.DuplicateUserName, "username_already_exists")]
    [InlineData(RegisterStatus.DuplicateEmail, "email_already_exists")]
    public async Task Register_Duplicate_ReturnsFieldSpecificConflict(RegisterStatus status, string expectedCode)
    {
        // Given
        using var cancellation = new CancellationTokenSource();
        var request = new RegisterRequest();
        var service = Substitute.For<IAuthService>();
        service.RegisterAsync(request, cancellation.Token).Returns(new RegisterResult { Status = status });

        // When
        var result = await CreateController(service).Register(request, cancellation.Token);

        // Then
        var response = result.Result.Should().BeOfType<ObjectResult>().Subject;
        response.StatusCode.Should().Be(409);
        var problem = response.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Extensions["code"].Should().Be(expectedCode);
        await service.Received(1).RegisterAsync(request, cancellation.Token);
    }

    [Theory]
    [InlineData(RegisterStatus.EmailVerificationExpired, 401)]
    [InlineData(RegisterStatus.EmailVerificationInvalid, 401)]
    [InlineData(RegisterStatus.EmailVerificationAlreadyUsed, 409)]
    [InlineData(RegisterStatus.EmailMismatch, 400)]
    public async Task Register_VerificationFailure_PreservesStatus(RegisterStatus status, int expectedStatus)
    {
        // Given
        using var cancellation = new CancellationTokenSource();
        var service = Substitute.For<IAuthService>();
        service.RegisterAsync(Arg.Any<RegisterRequest>(), Arg.Any<CancellationToken>())
            .Returns(new RegisterResult { Status = status });

        // When
        var result = await CreateController(service).Register(new RegisterRequest(), cancellation.Token);

        // Then
        result.Result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(expectedStatus);
    }

    [Fact]
    public async Task Register_Created_PreservesSuccessContract()
    {
        // Given
        using var cancellation = new CancellationTokenSource();
        var service = Substitute.For<IAuthService>();
        var user = new RegisterResponse
        {
            UserId = "new-user", UserName = "player1", Email = "player@example.com",
            Role = "Player", CreatedAtUtc = DateTime.UtcNow
        };
        service.RegisterAsync(Arg.Any<RegisterRequest>(), Arg.Any<CancellationToken>())
            .Returns(new RegisterResult { Status = RegisterStatus.Created, User = user });

        // When
        var result = await CreateController(service).Register(new RegisterRequest(), cancellation.Token);

        // Then
        var response = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        response.StatusCode.Should().Be(201);
        response.Value.Should().BeSameAs(user);
        response.ActionName.Should().Be(nameof(AuthController.Me));
    }

    private static AuthController CreateController(IAuthService service) => new(
        service, Substitute.For<IJwtTokenService>(), Substitute.For<IPlayerDefaultDataRepairService>());
}
