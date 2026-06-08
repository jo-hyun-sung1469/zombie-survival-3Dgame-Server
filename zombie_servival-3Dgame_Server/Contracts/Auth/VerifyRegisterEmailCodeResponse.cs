namespace zombie_survival_3Dgame_Server.Contracts.Auth;

public sealed class VerifyRegisterEmailCodeResponse
{
    public required string EmailVerificationId { get; init; }
    public required DateTime VerifiedAtUtc { get; init; }
}
