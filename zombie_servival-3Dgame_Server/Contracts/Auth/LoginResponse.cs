namespace zombie_servival_3Dgame_Server.Contracts.Auth;

public sealed class LoginResponse
{
    public required string AccessToken { get; init; }
    public required string TokenType { get; init; }
    public required DateTime ExpiresAtUtc { get; init; }
    public required string UserName { get; init; }
    public required string Role { get; init; }
}
