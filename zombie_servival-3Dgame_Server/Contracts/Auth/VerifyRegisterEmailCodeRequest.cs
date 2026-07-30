using System.ComponentModel.DataAnnotations;

namespace zombie_survival_3Dgame_Server.Contracts.Auth;

public sealed class VerifyRegisterEmailCodeRequest
{
    [Required]
    public string EmailVerificationId { get; init; } = string.Empty;

    [Required]
    [StringLength(12, MinimumLength = 4)]
    public string Code { get; init; } = string.Empty;
}
