using System.ComponentModel.DataAnnotations;

namespace zombie_survival_3Dgame_Server.Contracts.Auth;

public sealed class UserNameAvailabilityRequest
{
    [Required]
    [StringLength(AuthInputRules.UserNameMaxLength, MinimumLength = AuthInputRules.UserNameMinLength)]
    [RegularExpression(AuthInputRules.UserNamePattern, ErrorMessage = AuthInputRules.UserNameError)]
    public string UserName { get; init; } = string.Empty;
}
