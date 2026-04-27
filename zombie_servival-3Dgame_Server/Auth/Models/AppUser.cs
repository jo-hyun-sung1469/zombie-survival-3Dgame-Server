namespace zombie_servival_3Dgame_Server.Auth;

public sealed class AppUser
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string UserName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "Player";
    public DateTime CreatedAtUtc { get; set; }
}
