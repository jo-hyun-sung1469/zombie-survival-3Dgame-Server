using System.ComponentModel.DataAnnotations;

namespace zombie_survival_3Dgame_Server.Contracts.Auth;

public sealed class SendRegisterEmailCodeRequest
{
    [Required]
    [EmailAddress]
    [StringLength(254)]
    public string Email { get; init; } = string.Empty;
}
