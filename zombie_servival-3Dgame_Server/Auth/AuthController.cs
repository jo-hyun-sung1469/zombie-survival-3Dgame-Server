using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using zombie_survival_3Dgame_Server.Common;
using zombie_survival_3Dgame_Server.Contracts.Auth;
using zombie_survival_3Dgame_Server.Inventory;

namespace zombie_survival_3Dgame_Server.Auth;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    IAuthService authService,
    IJwtTokenService jwtTokenService,
    IPlayerDefaultDataRepairService playerDefaultDataRepairService) : ControllerBase
{
    [HttpPost("register/email-code")]
    [AllowAnonymous]
    public async Task<ActionResult<RegisterEmailCodeResponse>> SendRegisterEmailCode(
        [FromBody] SendRegisterEmailCodeRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authService.SendRegisterEmailCodeAsync(request, cancellationToken);
        return result.Status switch
        {
            RegisterEmailCodeStatus.Sent => Ok(new RegisterEmailCodeResponse
            {
                EmailVerificationId = result.EmailVerificationId!,
                ExpiresAtUtc = result.ExpiresAtUtc!.Value
            }),
            RegisterEmailCodeStatus.EmailAlreadyExists =>
                ApiProblemDetails.Create(StatusCodes.Status409Conflict, "Email already exists."),
            RegisterEmailCodeStatus.EmailDeliveryFailed =>
                ApiProblemDetails.Create(StatusCodes.Status503ServiceUnavailable, "Could not send signup verification code."),
            _ => ApiProblemDetails.Create(StatusCodes.Status500InternalServerError, "Signup verification could not be started.")
        };
    }

    [HttpPost("register/email-code/verify")]
    [AllowAnonymous]
    public async Task<ActionResult<VerifyRegisterEmailCodeResponse>> VerifyRegisterEmailCode(
        [FromBody] VerifyRegisterEmailCodeRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authService.VerifyRegisterEmailCodeAsync(request, cancellationToken);
        if (result.Status is RegisterEmailVerificationStatus.Success)
        {
            return Ok(new VerifyRegisterEmailCodeResponse
            {
                EmailVerificationId = result.EmailVerificationId!,
                VerifiedAtUtc = result.VerifiedAtUtc!.Value
            });
        }

        if (result.Status is RegisterEmailVerificationStatus.TooManyAttempts)
        {
            return ApiProblemDetails.Create(StatusCodes.Status429TooManyRequests, "Too many invalid verification attempts.");
        }

        if (result.Status is RegisterEmailVerificationStatus.Expired)
        {
            return ApiProblemDetails.Create(StatusCodes.Status401Unauthorized, "Verification code has expired.");
        }

        return ApiProblemDetails.Create(StatusCodes.Status401Unauthorized, "Invalid verification code.");
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<RegisterResponse>> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authService.RegisterAsync(request, cancellationToken);
        if (result.Status is RegisterStatus.Created && result.User is not null)
        {
            return CreatedAtAction(nameof(Me), new { }, result.User);
        }

        return result.Status switch
        {
            RegisterStatus.DuplicateUserNameOrEmail =>
                ApiProblemDetails.Create(StatusCodes.Status409Conflict, "Username or email already exists."),
            RegisterStatus.EmailVerificationExpired =>
                ApiProblemDetails.Create(StatusCodes.Status401Unauthorized, "Email verification has expired."),
            RegisterStatus.EmailVerificationAlreadyUsed =>
                ApiProblemDetails.Create(StatusCodes.Status409Conflict, "Email verification has already been used."),
            RegisterStatus.EmailMismatch =>
                ApiProblemDetails.Create(StatusCodes.Status400BadRequest, "Email does not match the verified email."),
            RegisterStatus.EmailVerificationInvalid =>
                ApiProblemDetails.Create(StatusCodes.Status401Unauthorized, "Email verification is required."),
            _ => ApiProblemDetails.Create(StatusCodes.Status500InternalServerError, "Registration could not be completed.")
        };
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

        await playerDefaultDataRepairService.EnsureAsync(user.Id, cancellationToken);
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
