using zombie_survival_3Dgame_Server.Contracts.Auth;

namespace zombie_survival_3Dgame_Server.Auth;

public enum RegisterStatus
{
    Created,
    DuplicateUserNameOrEmail,
    EmailVerificationInvalid,
    EmailVerificationExpired,
    EmailVerificationAlreadyUsed,
    EmailMismatch
}

public sealed class RegisterResult
{
    public required RegisterStatus Status { get; init; }
    public RegisterResponse? User { get; init; }
}
