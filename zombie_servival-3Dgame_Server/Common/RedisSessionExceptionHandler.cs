using Microsoft.AspNetCore.Diagnostics;
using zombie_survival_3Dgame_Server.GameSession.Progress;

namespace zombie_survival_3Dgame_Server.Common;

public sealed class RedisSessionExceptionHandler(ILogger<RedisSessionExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not RedisSessionUnavailableException) return false;
        logger.LogWarning(exception, "Redis session operation failed for {Path}.", httpContext.Request.Path);
        httpContext.Response.Headers.RetryAfter = "5";
        await Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "Game session verification is temporarily unavailable.",
            detail: "Retry with the same session id; no client-only reward fallback is allowed.").ExecuteAsync(httpContext);
        return true;
    }
}
