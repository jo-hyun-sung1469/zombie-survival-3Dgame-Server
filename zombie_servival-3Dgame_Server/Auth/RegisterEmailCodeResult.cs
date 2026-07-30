namespace zombie_survival_3Dgame_Server.Auth;

public enum RegisterEmailCodeStatus
{
    Sent,
    EmailAlreadyExists,
    EmailDeliveryFailed
}

public sealed class RegisterEmailCodeResult
{
    public required RegisterEmailCodeStatus Status { get; init; }
    public string? EmailVerificationId { get; init; }
    public DateTime? ExpiresAtUtc { get; init; }
}
