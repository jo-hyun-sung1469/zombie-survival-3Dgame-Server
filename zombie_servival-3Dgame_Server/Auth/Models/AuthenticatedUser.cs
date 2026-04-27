namespace zombie_servival_3Dgame_Server.Auth;

public sealed class AuthenticatedUser
{
    public required string Id { get; init; }
    public required string UserName { get; init; }
    public required string Role { get; init; }
}
