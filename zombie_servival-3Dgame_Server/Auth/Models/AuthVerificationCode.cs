namespace zombie_survival_3Dgame_Server.Auth.Models;

public sealed class AuthVerificationCode
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Email { get; set; } = string.Empty;
    public string CodeHash { get; set; } = string.Empty;
    public int AttemptCount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? VerifiedAtUtc { get; set; }
    public DateTime? ConsumedAtUtc { get; set; }
    public long Version { get; private set; }

    public void MarkChanged()
    {
        Version = checked(Version + 1);
    }
}
