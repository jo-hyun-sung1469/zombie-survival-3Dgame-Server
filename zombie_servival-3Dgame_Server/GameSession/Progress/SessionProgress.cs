namespace zombie_survival_3Dgame_Server.GameSession.Progress;

public sealed record SessionProgress(float SurvivalTimeSeconds, int ClearWave, int KillZombies, DateTime RecordedAtUtc);

public enum ProgressStatus { Success, NotFound, InvalidInput, Conflict, Missing }

public sealed record ProgressResult(ProgressStatus Status, SessionProgress? Progress = null);
