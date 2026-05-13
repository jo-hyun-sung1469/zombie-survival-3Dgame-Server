using Microsoft.AspNetCore.Mvc;

namespace zombie_servival_3Dgame_Server.Common;

public static class ApiProblemDetails
{
    public static ObjectResult Create(
        int statusCode,
        string title,
        string? detail = null,
        IDictionary<string, object?>? extensions = null)
    {
        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail
        };

        if (extensions is not null)
        {
            foreach (var extension in extensions)
            {
                problemDetails.Extensions[extension.Key] = extension.Value;
            }
        }

        return new ObjectResult(problemDetails)
        {
            StatusCode = statusCode
        };
    }
}
