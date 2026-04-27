using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using zombie_servival_3Dgame_Server.Contracts.Auth;
using zombie_servival_3Dgame_Server.Data;

namespace zombie_servival_3Dgame_Server.Auth;

public sealed class DbAuthService(GameDbContext dbContext) : IAuthService
{
    private readonly PasswordHasher<AppUser> _passwordHasher = new();

    public async Task<AuthenticatedUser?> ValidateCredentialsAsync(
        string userName,
        string password,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .SingleOrDefaultAsync(x => x.UserName.ToLower() == userName.ToLower(), cancellationToken);

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

    public async Task<RegisterResponse?> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var normalizedUserName = request.UserName.Trim();
        var exists = await dbContext.Users.AnyAsync(
            x => x.UserName.ToLower() == normalizedUserName.ToLower(),
            cancellationToken);

        if (exists)
        {
            return null;
        }

        var user = new AppUser
        {
            UserName = normalizedUserName,
            Role = "Player",
            CreatedAtUtc = DateTime.UtcNow
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new RegisterResponse
        {
            UserId = user.Id,
            UserName = user.UserName,
            Role = user.Role,
            CreatedAtUtc = user.CreatedAtUtc
        };
    }
}
