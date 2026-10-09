using Microsoft.Extensions.Options;
using StackExchange.Redis;
using zombie_survival_3Dgame_Server.Options;

namespace zombie_survival_3Dgame_Server.GameSession.Progress;

public sealed class RedisSessionConnection(IOptions<RedisSessionOptions> settings, IConfiguration configuration) : IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private ConnectionMultiplexer? connection;

    public async Task<IDatabase> GetDatabaseAsync(CancellationToken cancellationToken)
    {
        if (connection is not null) return connection.GetDatabase();
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (connection is null)
            {
                var options = ConfigurationOptions.Parse(settings.Value.Connection);
                configuration.GetSection("RedisSession:Credentials").Bind(options);
                options.IncludeDetailInExceptions = false;
                options.AbortOnConnectFail = false;
                options.ConnectTimeout = 3000;
                options.AsyncTimeout = 3000;
                options.ConnectRetry = 1;
                options.BacklogPolicy = BacklogPolicy.FailFast;
                options.ServiceName = settings.Value.ServiceName;
                connection = await ConnectionMultiplexer.ConnectAsync(options);
            }
            return connection.GetDatabase();
        }
        finally { gate.Release(); }
    }

    public void Dispose()
    {
        connection?.Dispose();
        gate.Dispose();
    }
}
