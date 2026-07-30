namespace zombie_survival_3Dgame_Server.Contracts.Auth;

public sealed class RegisterResponse
{
    public required string UserId { get; init; }
    public required string UserName { get; init; }
    public required string Email { get; init; }
    public required string Role { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
}
