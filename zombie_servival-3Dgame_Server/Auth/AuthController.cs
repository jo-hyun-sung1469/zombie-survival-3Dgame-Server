using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using zombie_survival_3Dgame_Server.Auth;
using zombie_survival_3Dgame_Server.Common;
using zombie_survival_3Dgame_Server.Contracts.Auth;

namespace zombie_survival_3Dgame_Server.Auth;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthService authService, IJwtTokenService jwtTokenService) : ControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<RegisterResponse>> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var registeredUser = await authService.RegisterAsync(request, cancellationToken);
        if (registeredUser is null)
        {
            return ApiProblemDetails.Create(StatusCodes.Status409Conflict, "Username already exists.");
        }

        return CreatedAtAction(nameof(Me), new { }, registeredUser);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await authService.ValidateCredentialsAsync(request.UserName, request.Password, cancellationToken);
        if (user is null)
        {
            return ApiProblemDetails.Create(StatusCodes.Status401Unauthorized, "Invalid username or password.");
        }

        var token = jwtTokenService.CreateToken(user);
        return Ok(token);
    }

    [HttpGet("me")]
    [Authorize]
    public IActionResult Me()
    {
        var userId = User.FindFirst("userId")?.Value;
        if (string.IsNullOrWhiteSpace(userId))
        {
            return ApiProblemDetails.Create(StatusCodes.Status401Unauthorized, "Token does not contain a valid user id.");
        }

        return Ok(new
        {
            UserId = userId,
            UserName = User.Identity?.Name,
            Role = User.FindFirst("role")?.Value
        });
    }
}
