using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using zombie_survival_3Dgame_Server.Common;
using zombie_survival_3Dgame_Server.Contracts.GameSession;
using zombie_survival_3Dgame_Server.GameSession.Progress;

namespace zombie_survival_3Dgame_Server.GameSession;

[ApiController]
[Route("api/game-sessions")]
[Authorize]
public sealed class GameSessionController(IGameSessionService sessionService, ISessionProgressService progressService) : ControllerBase
{
    [HttpPost("{sessionId}/waves")]
    [EnableRateLimiting(RateLimitPolicyNames.PlayerMutation)]
    public async Task<ActionResult<WaveProgressResponse>> RecordWaveAsync(string sessionId,
        [FromBody] WaveProgressRequest request, CancellationToken cancellationToken)
    {
        var playerId = User.FindFirst("userId")?.Value;
        if (string.IsNullOrWhiteSpace(playerId))
            return ApiProblemDetails.Create(StatusCodes.Status401Unauthorized, "Token does not contain a valid user id.");
        var result = await progressService.RecordWaveAsync(playerId, sessionId, request, cancellationToken);
        return result.Status switch
        {
            ProgressStatus.Success => Ok(new WaveProgressResponse(sessionId, result.Progress!.ClearWave,
                result.Progress.KillZombies, result.Progress.SurvivalTimeSeconds)),
            ProgressStatus.NotFound => ApiProblemDetails.Create(404, "No active session found for this player."),
            ProgressStatus.InvalidInput => ApiProblemDetails.Create(400, "Invalid statistics or survival time differs from server time."),
            _ => ApiProblemDetails.Create(409, "Progress is missing or inconsistent. Waves must advance by exactly one.")
        };
    }

    [HttpPost]
    [EnableRateLimiting(RateLimitPolicyNames.PlayerMutation)]
    public async Task<ActionResult<GameSessionResponse>> StartAsync(CancellationToken cancellationToken)
    {
        var playerId = User.FindFirst("userId")?.Value;
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return ApiProblemDetails.Create(StatusCodes.Status401Unauthorized, "Token does not contain a valid user id.");
        }

        var response = await sessionService.StartAsync(playerId, cancellationToken);
        if (response is null)
        {
            return ApiProblemDetails.Create(StatusCodes.Status404NotFound, "No account found for this player.");
        }

        return Ok(response);
    }

    [HttpGet("{sessionId}")]
    public async Task<ActionResult<GameSessionResponse>> GetAsync(string sessionId, CancellationToken cancellationToken)
    {
        var playerId = User.FindFirst("userId")?.Value;
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return ApiProblemDetails.Create(StatusCodes.Status401Unauthorized, "Token does not contain a valid user id.");
        }

        var response = await sessionService.GetAsync(playerId, sessionId, cancellationToken);
        if (response is null)
        {
            return ApiProblemDetails.Create(StatusCodes.Status404NotFound, "No session found for this player.");
        }

        return Ok(response);
    }
}
