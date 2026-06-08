namespace zombie_survival_3Dgame_Server.Contracts.Auth;

public sealed class RegisterEmailCodeResponse
{
    public required string EmailVerificationId { get; init; }
    public required DateTime ExpiresAtUtc { get; init; }
}
