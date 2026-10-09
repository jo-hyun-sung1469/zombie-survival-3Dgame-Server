namespace zombie_survival_3Dgame_Server.GameSession.Progress;

public sealed class RedisSessionUnavailableException(Exception innerException)
    : Exception("Game session storage is temporarily unavailable.", innerException);
