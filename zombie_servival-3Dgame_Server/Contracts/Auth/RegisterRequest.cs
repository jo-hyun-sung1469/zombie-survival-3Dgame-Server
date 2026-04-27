using System.ComponentModel.DataAnnotations;

namespace zombie_servival_3Dgame_Server.Contracts.Auth;

public sealed class RegisterRequest
{
    [Required]
    [StringLength(30, MinimumLength = 3)]
    public string UserName { get; init; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 6)]
    public string Password { get; init; } = string.Empty;
}
