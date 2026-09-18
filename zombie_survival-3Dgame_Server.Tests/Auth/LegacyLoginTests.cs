using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using zombie_survival_3Dgame_Server.Auth;
using zombie_survival_3Dgame_Server.Auth.Models;
using zombie_survival_3Dgame_Server.Data;
using zombie_survival_3Dgame_Server.Options;

namespace zombie_survival_3Dgame_Server.Tests.Auth;

public sealed class LegacyLoginTests
{
    [Fact]
    public async Task ValidateCredentialsAsync_LegacyCharacters_StillVerifiesExistingHash()
    {
        // Given
        using var cancellation = new CancellationTokenSource();
        var options = new DbContextOptionsBuilder<GameDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
        await using var context = new GameDbContext(options);
        var examplePassword = "기존 password._()";
        var user = new AppUser { UserName = "Legacy_Player", Email = "legacy@example.com" };
        user.PasswordHash = new PasswordHasher<AppUser>().HashPassword(user, examplePassword);
        context.Users.Add(user);
        await context.SaveChangesAsync(cancellation.Token);
        var service = new DbAuthService(context, Substitute.For<IEmailSender>(),
            Microsoft.Extensions.Options.Options.Create(new EmailAuthOptions()), NullLogger<DbAuthService>.Instance);

        // When
        var result = await service.ValidateCredentialsAsync("Legacy_Player", examplePassword, cancellation.Token);

        // Then
        result.Should().BeEquivalentTo(new AuthenticatedUser
        {
            Id = user.Id, UserName = "Legacy_Player", Role = user.Role
        });
    }
}
