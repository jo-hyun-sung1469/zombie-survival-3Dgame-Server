using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using zombie_survival_3Dgame_Server.Common;
using zombie_survival_3Dgame_Server.Contracts.Gacha;

namespace zombie_survival_3Dgame_Server.Gacha;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class GachaController(IGachaService gachaService) : ControllerBase
{
    [HttpGet("pool")]
    public async Task<ActionResult<GachaPoolResponse>> GetPoolAsync(CancellationToken cancellationToken)
    {
        var playerId = User.FindFirst("userId")?.Value;
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return ApiProblemDetails.Create(StatusCodes.Status401Unauthorized, "Token does not contain a valid user id.");
        }

        var pool = await gachaService.GetPoolAsync(playerId, cancellationToken);
        return Ok(pool);
    }

    [HttpPost("pull")]
    [EnableRateLimiting(RateLimitPolicyNames.PlayerMutation)]
    public async Task<ActionResult<GachaPullResponse>> PullAsync(CancellationToken cancellationToken)
    {
        var playerId = User.FindFirst("userId")?.Value;
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return ApiProblemDetails.Create(StatusCodes.Status401Unauthorized, "Token does not contain a valid user id.");
        }

        var result = await gachaService.PullAsync(playerId, cancellationToken);
        if (result.Status == GachaPullStatus.Completed)
        {
            return ApiProblemDetails.Create(StatusCodes.Status409Conflict, "All gacha rewards have already been obtained.");
        }

        if (result.Status == GachaPullStatus.InsufficientGold)
        {
            return ApiProblemDetails.Create(
                StatusCodes.Status400BadRequest,
                "Not enough gold to use the gacha.",
                extensions: new Dictionary<string, object?>
                {
                    ["requiredGold"] = result.RequiredGold,
                    ["currentGold"] = result.CurrentGold
                });
        }

        return Ok(result.Response);
    }
}
