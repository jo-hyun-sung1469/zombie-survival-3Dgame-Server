using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace zombie_survival_3Dgame_Server.Common;

public sealed class PersistenceConflictExceptionHandler(
    ILogger<PersistenceConflictExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (!IsPersistenceConflict(exception))
        {
            return false;
        }

        logger.LogWarning(
            exception,
            "A concurrent or duplicate persistence operation was rejected for {Path}.",
            httpContext.Request.Path);

        await Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "The requested state changed concurrently.",
                detail: "Refresh the current state and retry the request.")
            .ExecuteAsync(httpContext);

        return true;
    }

    private static bool IsPersistenceConflict(Exception exception)
    {
        if (exception is DbUpdateConcurrencyException)
        {
            return true;
        }

        return PersistenceErrors.IsDuplicateKey(exception);
    }
}
