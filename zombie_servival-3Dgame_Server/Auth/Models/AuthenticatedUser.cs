namespace zombie_survival_3Dgame_Server.Auth.Models;

public sealed class AuthenticatedUser
{
    public required string Id { get; init; }
    public required string UserName { get; init; }
    public required string Role { get; init; }
}
