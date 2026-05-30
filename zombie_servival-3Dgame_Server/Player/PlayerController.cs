using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using zombie_survival_3Dgame_Server.Common;
using zombie_survival_3Dgame_Server.Contracts.Player;

namespace zombie_survival_3Dgame_Server.Player;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class PlayerController(IPlayerService playerService) : ControllerBase
{
    [HttpGet("stats/me")]
    public async Task<ActionResult<PlayerStatsResponse>> GetMyStatsAsync(CancellationToken cancellationToken)
    {
        var playerId = User.FindFirst("userId")?.Value;
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return ApiProblemDetails.Create(StatusCodes.Status401Unauthorized, "Token does not contain a valid user id.");
        }

        var response = await playerService.GetStatsAsync(playerId, cancellationToken);
        return Ok(response);
    }
}
