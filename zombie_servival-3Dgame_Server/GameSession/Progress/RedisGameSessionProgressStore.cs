using System.Globalization;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using zombie_survival_3Dgame_Server.GameSession.Models;
using zombie_survival_3Dgame_Server.Options;

namespace zombie_survival_3Dgame_Server.GameSession.Progress;

public sealed class RedisGameSessionProgressStore(RedisSessionConnection connection,
    IOptions<RedisSessionOptions> options) : IGameSessionProgressStore
{
    internal const string InitializeScript = """
        if redis.call('EXISTS', KEYS[1]) == 1 then return 0 end
        redis.call('HSET', KEYS[1], 'StartedAt', ARGV[1], 'ClearWave', 0, 'KillZombies', 0,
          'SurvivalTimeSeconds', 0, 'RecordedAt', ARGV[1], 'Ended', 0)
        redis.call('EXPIRE', KEYS[1], ARGV[2])
        return 1
        """;

    internal const string UpdateScript = """
        if redis.call('EXISTS', KEYS[1]) == 0 then return {4} end
        if redis.call('HGET', KEYS[1], 'StartedAt') ~= ARGV[1] then return {3} end
        local old = redis.call('HMGET', KEYS[1], 'ClearWave', 'KillZombies', 'SurvivalTimeSeconds', 'RecordedAt', 'Ended')
        if old[5] == '1' then
          if ARGV[6] == '1' then return {0, old[3], old[1], old[2], old[4]} end
          return {3}
        end
        local wave = tonumber(ARGV[2])
        if (ARGV[6] == '0' and wave ~= tonumber(old[1]) + 1)
          or (ARGV[6] == '1' and wave ~= tonumber(old[1]))
          or tonumber(ARGV[3]) < tonumber(old[2])
          or tonumber(ARGV[4]) < tonumber(old[3]) then return {3} end
        redis.call('HSET', KEYS[1], 'ClearWave', ARGV[2], 'KillZombies', ARGV[3],
          'SurvivalTimeSeconds', ARGV[4], 'RecordedAt', ARGV[5], 'Ended', ARGV[6])
        if ARGV[6] == '1' then redis.call('EXPIRE', KEYS[1], ARGV[7]) end
        return {0, ARGV[4], ARGV[2], ARGV[3], ARGV[5]}
        """;

    private static RedisKey Key(string playerId, string sessionId) => $"game-session:{playerId}:{sessionId}";
    private static string Timestamp(DateTime value) => value.Ticks.ToString(CultureInfo.InvariantCulture);

    public async Task InitializeAsync(SurvivalGameSession session, CancellationToken cancellationToken)
    {
        await ExecuteAsync(async db => await db.ScriptEvaluateAsync(InitializeScript,
            [Key(session.PlayerId, session.Id)], [Timestamp(session.StartedAtUtc), options.Value.RetentionDays * 86400]), cancellationToken);
    }

    public async Task<ProgressResult> UpdateAsync(SurvivalGameSession session, SessionProgress progress, bool final,
        CancellationToken cancellationToken)
    {
        var result = await ExecuteAsync(async db => await db.ScriptEvaluateAsync(UpdateScript,
            [Key(session.PlayerId, session.Id)],
            [Timestamp(session.StartedAtUtc), progress.ClearWave, progress.KillZombies,
             progress.SurvivalTimeSeconds.ToString("R", CultureInfo.InvariantCulture), Timestamp(progress.RecordedAtUtc),
             final ? 1 : 0, options.Value.RetentionDays * 86400]), cancellationToken);
        var values = (RedisResult[])result!;
        var status = (ProgressStatus)(int)values[0];
        if (status != ProgressStatus.Success) return new(status);
        return new(status, new SessionProgress(
            float.Parse((string)values[1]!, CultureInfo.InvariantCulture), (int)values[2], (int)values[3],
            new DateTime(long.Parse((string)values[4]!, CultureInfo.InvariantCulture), DateTimeKind.Utc)));
    }

    public async Task<SessionProgress?> GetFinalAsync(SurvivalGameSession session, CancellationToken cancellationToken)
        => await ReadAsync(session, true, cancellationToken);

    public async Task<SessionProgress?> GetProgressAsync(SurvivalGameSession session, CancellationToken cancellationToken)
        => await ReadAsync(session, false, cancellationToken);

    private async Task<SessionProgress?> ReadAsync(SurvivalGameSession session, bool finalOnly, CancellationToken cancellationToken)
    {
        var values = await ExecuteAsync(db => db.HashGetAsync(Key(session.PlayerId, session.Id),
            ["StartedAt", "Ended", "SurvivalTimeSeconds", "ClearWave", "KillZombies", "RecordedAt"]), cancellationToken);
        if (values[0] != Timestamp(session.StartedAtUtc) || (finalOnly && values[1] != "1")) return null;
        return new SessionProgress(float.Parse(values[2].ToString(), CultureInfo.InvariantCulture),
            (int)values[3], (int)values[4], new DateTime((long)values[5], DateTimeKind.Utc));
    }

    public async Task RemoveAsync(string playerId, string sessionId, CancellationToken cancellationToken)
    {
        await ExecuteAsync(db => db.KeyDeleteAsync(Key(playerId, sessionId)), cancellationToken);
    }

    public async Task FinalizeTtlAsync(SurvivalGameSession session, CancellationToken cancellationToken)
    {
        if (!session.CompletedAtUtc.HasValue) throw new InvalidOperationException("Only durable final results can expire.");
        await ExecuteAsync(db => db.KeyExpireAsync(Key(session.PlayerId, session.Id),
            DateTime.SpecifyKind(session.CompletedAtUtc.Value, DateTimeKind.Utc)
                .AddSeconds(options.Value.EndedTtlSeconds)), cancellationToken);
    }

    private async Task<T> ExecuteAsync<T>(Func<IDatabase, Task<T>> operation, CancellationToken cancellationToken)
    {
        try
        {
            var db = await connection.GetDatabaseAsync(cancellationToken);
            return await operation(db).WaitAsync(cancellationToken);
        }
        catch (RedisException exception) { throw new RedisSessionUnavailableException(exception); }
    }
}
