namespace zombie_survival_3Dgame_Server.Options;

public sealed class EmailAuthOptions
{
    public const string SectionName = "EmailAuth";

    public int CodeLength { get; init; } = 6;
    public int ExpirationMinutes { get; init; } = 10;
    public int MaxAttempts { get; init; } = 5;
}
