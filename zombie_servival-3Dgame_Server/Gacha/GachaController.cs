using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using zombie_servival_3Dgame_Server.Contracts.Gacha;

namespace zombie_servival_3Dgame_Server.Gacha;

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
            return Unauthorized(new { message = "Token does not contain a valid user id." });
        }

        var pool = await gachaService.GetPoolAsync(playerId, cancellationToken);
        return Ok(pool);
    }

    [HttpPost("pull")]
    public async Task<ActionResult<GachaPullResponse>> PullAsync(CancellationToken cancellationToken)
    {
        var playerId = User.FindFirst("userId")?.Value;
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return Unauthorized(new { message = "Token does not contain a valid user id." });
        }

        var result = await gachaService.PullAsync(playerId, cancellationToken);
        if (result.Status == GachaPullStatus.Completed)
        {
            return Conflict(new { message = "All gacha rewards have already been obtained." });
        }

        if (result.Status == GachaPullStatus.InsufficientGold)
        {
            return BadRequest(new
            {
                message = "Not enough gold to use the gacha.",
                requiredGold = result.RequiredGold,
                currentGold = result.CurrentGold
            });
        }

        return Ok(result.Response);
    }
}
