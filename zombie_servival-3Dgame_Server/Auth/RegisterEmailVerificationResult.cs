namespace zombie_survival_3Dgame_Server.Auth;

public enum RegisterEmailVerificationStatus
{
    Success,
    InvalidCode,
    Expired,
    TooManyAttempts
}

public sealed class RegisterEmailVerificationResult
{
    public required RegisterEmailVerificationStatus Status { get; init; }
    public string? EmailVerificationId { get; init; }
    public DateTime? VerifiedAtUtc { get; init; }
}
