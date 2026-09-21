using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using zombie_survival_3Dgame_Server.Auth;
using zombie_survival_3Dgame_Server.Auth.Models;
using zombie_survival_3Dgame_Server.Contracts.Auth;
using zombie_survival_3Dgame_Server.Data;
using zombie_survival_3Dgame_Server.Options;

namespace zombie_survival_3Dgame_Server.Tests.Auth;

public sealed class RegistrationTests
{
    [Fact]
    public async Task RegisterAsync_ExistingUserName_RejectsWithoutSavingOrChangingVerification()
    {
        // Given
        using var cancellation = new CancellationTokenSource();
        var saves = new SaveCounter();
        await using var context = CreateContext(saves);
        var proof = await SeedAsync(context, cancellation.Token);
        var originalVersion = proof.Version;
        var originalSaveCount = saves.Count;
        var service = CreateService(context);

        // When
        var result = await service.RegisterAsync(CreateRequest("takenname"), cancellation.Token);

        // Then
        result.Status.Should().Be(RegisterStatus.DuplicateUserName);
        saves.Count.Should().Be(originalSaveCount);
        proof.ConsumedAtUtc.Should().BeNull();
        proof.Version.Should().Be(originalVersion);
        context.ChangeTracker.HasChanges().Should().BeFalse();
    }

    [Fact]
    public async Task RegisterAsync_NewUserNameAfterDuplicate_ReusesVerification()
    {
        // Given
        using var cancellation = new CancellationTokenSource();
        await using var context = CreateContext();
        var proof = await SeedAsync(context, cancellation.Token);
        var originalVersion = proof.Version;
        var service = CreateService(context);

        // When
        var duplicate = await service.RegisterAsync(CreateRequest("takenname"), cancellation.Token);
        var retry = await service.RegisterAsync(CreateRequest("newname"), cancellation.Token);

        // Then
        duplicate.Status.Should().Be(RegisterStatus.DuplicateUserName);
        retry.Status.Should().Be(RegisterStatus.Created);
        proof.ConsumedAtUtc.Should().NotBeNull();
        proof.Version.Should().Be(originalVersion + 1);
        (await context.Users.CountAsync(cancellation.Token)).Should().Be(2);
    }

    [Fact]
    public async Task RegisterAsync_BothFieldsDuplicate_PreservesEmailConflictPriority()
    {
        // Given
        using var cancellation = new CancellationTokenSource();
        await using var context = CreateContext();
        await SeedAsync(context, cancellation.Token);
        var service = CreateService(context);

        // When
        var result = await service.RegisterAsync(new RegisterRequest
        {
            UserName = "takenname", Email = "owner@example.com",
            EmailVerificationId = "missing", Password = "test-password"
        }, cancellation.Token);

        // Then
        result.Status.Should().Be(RegisterStatus.DuplicateEmail);
        context.ChangeTracker.HasChanges().Should().BeFalse();
    }

    [Theory]
    [InlineData(false, RegisterStatus.EmailVerificationInvalid)]
    [InlineData(true, RegisterStatus.EmailVerificationExpired)]
    public async Task RegisterAsync_DuplicateNameWithInvalidVerification_PreservesVerificationFailurePriority(
        bool expired, RegisterStatus expectedStatus)
    {
        // Given
        using var cancellation = new CancellationTokenSource();
        await using var context = CreateContext();
        var proof = await SeedAsync(context, cancellation.Token);
        if (expired)
            proof.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1);
        else
            proof.VerifiedAtUtc = null;
        await context.SaveChangesAsync(cancellation.Token);
        var service = CreateService(context);

        // When
        var result = await service.RegisterAsync(CreateRequest("takenname"), cancellation.Token);

        // Then
        result.Status.Should().Be(expectedStatus);
        proof.ConsumedAtUtc.Should().BeNull();
        context.ChangeTracker.HasChanges().Should().BeFalse();
    }

    private static GameDbContext CreateContext(SaveCounter? saves = null)
    {
        var options = new DbContextOptionsBuilder<GameDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"));
        if (saves is not null)
            options.AddInterceptors(saves);
        return new GameDbContext(options.Options);
    }

    private static async Task<AuthVerificationCode> SeedAsync(GameDbContext context, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var proof = new AuthVerificationCode
        {
            Id = "proof", Email = "applicant@example.com", CreatedAtUtc = now,
            VerifiedAtUtc = now, ExpiresAtUtc = now.AddMinutes(10)
        };
        proof.MarkChanged();
        context.Users.Add(new AppUser { UserName = "takenname", Email = "owner@example.com" });
        context.AuthVerificationCodes.Add(proof);
        await context.SaveChangesAsync(cancellationToken);
        return proof;
    }

    private static RegisterRequest CreateRequest(string userName) => new()
    {
        UserName = userName, Email = "applicant@example.com",
        EmailVerificationId = "proof", Password = "test-password"
    };

    private static DbAuthService CreateService(GameDbContext context) => new(
        context, Substitute.For<IEmailSender>(),
        Microsoft.Extensions.Options.Options.Create(new EmailAuthOptions()), NullLogger<DbAuthService>.Instance);

    private sealed class SaveCounter : SaveChangesInterceptor
    {
        public int Count { get; private set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            Count++;
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
