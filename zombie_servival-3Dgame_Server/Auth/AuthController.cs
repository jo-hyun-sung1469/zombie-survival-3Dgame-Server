using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using zombie_servival_3Dgame_Server.Auth;
using zombie_servival_3Dgame_Server.Contracts.Auth;

namespace zombie_servival_3Dgame_Server.Auth;

[ApiController]
[Route("api/[controller]")]
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
            return Conflict(new { message = "Username already exists." });
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
            return Unauthorized(new { message = "Invalid username or password." });
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
            return Unauthorized(new { message = "Token does not contain a valid user id." });
        }

        return Ok(new
        {
            UserId = userId,
            UserName = User.Identity?.Name,
            Role = User.FindFirst("role")?.Value
        });
    }
}
