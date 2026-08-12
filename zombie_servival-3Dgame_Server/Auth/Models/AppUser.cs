namespace zombie_survival_3Dgame_Server.Auth.Models;

public sealed class AppUser
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "Player";
    public DateTime CreatedAtUtc { get; set; }
}
