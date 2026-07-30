using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using zombie_survival_3Dgame_Server.Common;
using zombie_survival_3Dgame_Server.Contracts.Player;

namespace zombie_survival_3Dgame_Server.Player;

[ApiController]
[Route("api/player/stats")]
[Authorize]
public sealed class PlayerController(
    IPlayerService playerService,
    IPlayerStatUpgradeService playerStatUpgradeService) : ControllerBase
{
    [HttpGet("me")]
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

    [HttpPost("upgrades")]
    [EnableRateLimiting(RateLimitPolicyNames.PlayerMutation)]
    public async Task<ActionResult<UpgradePlayerStatResponse>> UpgradeStatAsync(
        [FromBody] UpgradePlayerStatRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.StatName))
        {
            return ApiProblemDetails.Create(StatusCodes.Status400BadRequest, "Stat name is required.");
        }

        var playerId = User.FindFirst("userId")?.Value;
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return ApiProblemDetails.Create(StatusCodes.Status401Unauthorized, "Token does not contain a valid user id.");
        }

        var result = await playerStatUpgradeService.UpgradeAsync(playerId, request.StatName, cancellationToken);
        return result.Status switch
        {
            PlayerStatUpgradeStatus.Success => Ok(result.Response),
            PlayerStatUpgradeStatus.InvalidStat => ApiProblemDetails.Create(
                StatusCodes.Status400BadRequest,
                "Player stat is not upgradeable."),
            PlayerStatUpgradeStatus.InsufficientGold => ApiProblemDetails.Create(
                StatusCodes.Status409Conflict,
                "Not enough gold to upgrade this stat.",
                extensions: new Dictionary<string, object?>
                {
                    ["requiredGold"] = result.RequiredGold,
                    ["currentGold"] = result.CurrentGold
                }),
            PlayerStatUpgradeStatus.MaxLevelReached => ApiProblemDetails.Create(
                StatusCodes.Status409Conflict,
                "Player stat is already at max level.",
                extensions: new Dictionary<string, object?>
                {
                    ["maxLevel"] = result.MaxLevel,
                    ["currentUpgradeLevel"] = result.CurrentUpgradeLevel,
                    ["currentGold"] = result.CurrentGold
                }),
            _ => ApiProblemDetails.Create(StatusCodes.Status500InternalServerError, "Unexpected player stat upgrade status.")
        };
    }
}
