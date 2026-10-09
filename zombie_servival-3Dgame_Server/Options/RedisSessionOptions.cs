namespace zombie_survival_3Dgame_Server.Options;

public sealed class RedisSessionOptions
{
    public const string SectionName = "RedisSession";
    public string Connection { get; init; } = "localhost:6379";
    public string? ServiceName { get; init; }
    public double TimeToleranceSeconds { get; init; } = 5;
    public int EndedTtlSeconds { get; init; } = 30;
    public int RetentionDays { get; init; } = 3;
    public int FailureAlertSeconds { get; init; } = 20;
    public string? AlertWebhookUrl { get; init; }
}
