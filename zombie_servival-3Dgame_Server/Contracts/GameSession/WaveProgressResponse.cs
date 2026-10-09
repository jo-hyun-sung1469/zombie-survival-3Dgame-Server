namespace zombie_survival_3Dgame_Server.Contracts.GameSession;

public sealed record WaveProgressResponse(string SessionId, int ClearWave, int KillZombies, float SurvivalTimeSeconds);
