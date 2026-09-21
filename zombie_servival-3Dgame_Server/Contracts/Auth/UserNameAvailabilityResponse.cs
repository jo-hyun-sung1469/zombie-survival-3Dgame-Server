namespace zombie_survival_3Dgame_Server.Contracts.Auth;

public sealed class UserNameAvailabilityResponse
{
    public required string UserName { get; init; }
    public required bool IsAvailable { get; init; }
}
