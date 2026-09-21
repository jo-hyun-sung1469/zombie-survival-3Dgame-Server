using System.ComponentModel.DataAnnotations;

namespace zombie_survival_3Dgame_Server.Contracts.Auth;

public sealed class RegisterRequest
{
    [Required]
    [StringLength(AuthInputRules.UserNameMaxLength, MinimumLength = AuthInputRules.UserNameMinLength)]
    [RegularExpression(AuthInputRules.UserNamePattern, ErrorMessage = AuthInputRules.UserNameError)]
    public string UserName { get; init; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(254)]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string EmailVerificationId { get; init; } = string.Empty;

    [Required]
    [StringLength(AuthInputRules.PasswordMaxLength, MinimumLength = AuthInputRules.PasswordMinLength)]
    [RegularExpression(AuthInputRules.PasswordPattern, ErrorMessage = AuthInputRules.PasswordError)]
    public string Password { get; init; } = string.Empty;
}
