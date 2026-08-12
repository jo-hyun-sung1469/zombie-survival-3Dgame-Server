using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using zombie_survival_3Dgame_Server.Auth.Models;
using zombie_survival_3Dgame_Server.Contracts.Auth;
using zombie_survival_3Dgame_Server.Data;
using zombie_survival_3Dgame_Server.Options;

namespace zombie_survival_3Dgame_Server.Auth;

public sealed class DbAuthService(
    GameDbContext dbContext,
    IEmailSender emailSender,
    IOptions<EmailAuthOptions> emailAuthOptions,
    ILogger<DbAuthService> logger) : IAuthService
{
    private readonly PasswordHasher<AppUser> _passwordHasher = new();
    private readonly PasswordHasher<AuthVerificationCode> _codeHasher = new();
    private readonly EmailAuthOptions _emailAuthOptions = emailAuthOptions.Value;

    public async Task<RegisterEmailCodeResult> SendRegisterEmailCodeAsync(
        SendRegisterEmailCodeRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = (request.Email ?? string.Empty).Trim().ToLowerInvariant();
        var exists = await dbContext.Users.AnyAsync(x => x.Email == normalizedEmail, cancellationToken);
        if (exists)
        {
            return new RegisterEmailCodeResult { Status = RegisterEmailCodeStatus.EmailAlreadyExists };
        }

        var now = DateTime.UtcNow;
        var code = GenerateNumericCode(_emailAuthOptions.CodeLength);
        var verificationCode = new AuthVerificationCode
        {
            Email = normalizedEmail,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddMinutes(_emailAuthOptions.ExpirationMinutes)
        };
        verificationCode.CodeHash = _codeHasher.HashPassword(verificationCode, code);

        dbContext.AuthVerificationCodes.Add(verificationCode);
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            await emailSender.SendRegisterVerificationCodeAsync(normalizedEmail, code, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send registration verification email to {Email}", normalizedEmail);
            dbContext.AuthVerificationCodes.Remove(verificationCode);
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (Exception cleanupException)
            {
                logger.LogWarning(
                    cleanupException,
                    "Failed to remove an undelivered registration verification for {Email}.",
                    normalizedEmail);
            }

            return new RegisterEmailCodeResult { Status = RegisterEmailCodeStatus.EmailDeliveryFailed };
        }

        return new RegisterEmailCodeResult
        {
            Status = RegisterEmailCodeStatus.Sent,
            EmailVerificationId = verificationCode.Id,
            ExpiresAtUtc = verificationCode.ExpiresAtUtc
        };
    }

    public async Task<RegisterEmailVerificationResult> VerifyRegisterEmailCodeAsync(
        VerifyRegisterEmailCodeRequest request,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var verificationId = (request.EmailVerificationId ?? string.Empty).Trim();
        var verificationCode = await dbContext.AuthVerificationCodes
            .SingleOrDefaultAsync(x => x.Id == verificationId, cancellationToken);

        if (verificationCode is null || verificationCode.ConsumedAtUtc is not null)
        {
            return new RegisterEmailVerificationResult { Status = RegisterEmailVerificationStatus.InvalidCode };
        }

        if (verificationCode.AttemptCount >= _emailAuthOptions.MaxAttempts)
        {
            return new RegisterEmailVerificationResult { Status = RegisterEmailVerificationStatus.TooManyAttempts };
        }

        if (verificationCode.ExpiresAtUtc <= now)
        {
            return new RegisterEmailVerificationResult { Status = RegisterEmailVerificationStatus.Expired };
        }

        var codeResult = _codeHasher.VerifyHashedPassword(
            verificationCode,
            verificationCode.CodeHash,
            (request.Code ?? string.Empty).Trim());

        if (codeResult is PasswordVerificationResult.Failed)
        {
            var updatedRows = await dbContext.AuthVerificationCodes
                .Where(x => x.Id == verificationId
                            && x.ConsumedAtUtc == null
                            && x.VerifiedAtUtc == null
                            && x.ExpiresAtUtc > now
                            && x.AttemptCount < _emailAuthOptions.MaxAttempts)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(x => x.AttemptCount, x => x.AttemptCount + 1)
                        .SetProperty(x => x.Version, x => x.Version + 1),
                    cancellationToken);

            if (updatedRows == 0)
            {
                return new RegisterEmailVerificationResult
                {
                    Status = RegisterEmailVerificationStatus.TooManyAttempts
                };
            }

            var attemptCount = verificationCode.AttemptCount + 1;
            return attemptCount >= _emailAuthOptions.MaxAttempts
                ? new RegisterEmailVerificationResult { Status = RegisterEmailVerificationStatus.TooManyAttempts }
                : new RegisterEmailVerificationResult { Status = RegisterEmailVerificationStatus.InvalidCode };
        }

        var verifiedRows = await dbContext.AuthVerificationCodes
            .Where(x => x.Id == verificationId
                        && x.ConsumedAtUtc == null
                        && x.VerifiedAtUtc == null
                        && x.ExpiresAtUtc > now
                        && x.AttemptCount < _emailAuthOptions.MaxAttempts)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.VerifiedAtUtc, now)
                    .SetProperty(x => x.Version, x => x.Version + 1),
                cancellationToken);

        if (verifiedRows == 0)
        {
            return new RegisterEmailVerificationResult
            {
                Status = RegisterEmailVerificationStatus.InvalidCode
            };
        }

        return new RegisterEmailVerificationResult
        {
            Status = RegisterEmailVerificationStatus.Success,
            EmailVerificationId = verificationCode.Id,
            VerifiedAtUtc = now
        };
    }

    public async Task<AuthenticatedUser?> ValidateCredentialsAsync(
        string userName,
        string password,
        CancellationToken cancellationToken)
    {
        var normalizedUserName = userName.Trim();
        var user = await dbContext.Users
            .SingleOrDefaultAsync(x => x.UserName == normalizedUserName, cancellationToken);

        if (user is null)
        {
            return null;
        }

        var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (result is PasswordVerificationResult.Failed)
        {
            return null;
        }

        return new AuthenticatedUser
        {
            Id = user.Id,
            UserName = user.UserName,
            Role = user.Role
        };
    }

    public async Task<RegisterResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var normalizedUserName = (request.UserName ?? string.Empty).Trim();
        var normalizedEmail = (request.Email ?? string.Empty).Trim().ToLowerInvariant();
        var exists = await dbContext.Users.AnyAsync(
            x => x.UserName == normalizedUserName || x.Email == normalizedEmail, cancellationToken);

        if (exists)
        {
            return new RegisterResult { Status = RegisterStatus.DuplicateUserNameOrEmail };
        }

        var now = DateTime.UtcNow;
        var verificationId = (request.EmailVerificationId ?? string.Empty).Trim();
        var verificationCode = await dbContext.AuthVerificationCodes
            .SingleOrDefaultAsync(x => x.Id == verificationId, cancellationToken);

        if (verificationCode is null || verificationCode.VerifiedAtUtc is null)
        {
            return new RegisterResult { Status = RegisterStatus.EmailVerificationInvalid };
        }

        if (verificationCode.ConsumedAtUtc is not null)
        {
            return new RegisterResult { Status = RegisterStatus.EmailVerificationAlreadyUsed };
        }

        if (verificationCode.ExpiresAtUtc <= now)
        {
            return new RegisterResult { Status = RegisterStatus.EmailVerificationExpired };
        }

        if (!string.Equals(verificationCode.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase))
        {
            return new RegisterResult { Status = RegisterStatus.EmailMismatch };
        }

        var user = new AppUser
        {
            UserName = normalizedUserName,
            Email = normalizedEmail,
            Role = "Player",
            CreatedAtUtc = now
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);
        verificationCode.ConsumedAtUtc = now;
        verificationCode.MarkChanged();

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new RegisterResult
        {
            Status = RegisterStatus.Created,
            User = new RegisterResponse
            {
                UserId = user.Id,
                UserName = user.UserName,
                Email = user.Email,
                Role = user.Role,
                CreatedAtUtc = user.CreatedAtUtc
            }
        };
    }

    private static string GenerateNumericCode(int length)
    {
        var codeLength = Math.Clamp(length, 4, 12);
        var digits = new char[codeLength];
        for (var i = 0; i < digits.Length; i++)
        {
            digits[i] = (char)('0' + RandomNumberGenerator.GetInt32(10));
        }

        return new string(digits);
    }
}
