using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using zombie_survival_3Dgame_Server.Common;
using zombie_survival_3Dgame_Server.Contracts.Reward;

namespace zombie_survival_3Dgame_Server.Reward;

[ApiController]
[Route("api/rewards")]
[Authorize]
public sealed class RewardController(ISurvivalRewardService rewardService) : ControllerBase
{
    [HttpPost("survival")]
    [EnableRateLimiting(RateLimitPolicyNames.PlayerMutation)]
    public async Task<ActionResult<RewardResponse>> SurvivalRewardAsync(
        [FromBody] RewardRequest request, CancellationToken cancellationToken)
    {
        var playerId = User.FindFirst("userId")?.Value;
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return ApiProblemDetails.Create(StatusCodes.Status401Unauthorized, "Token does not contain a valid user id.");
        }

        var result = await rewardService.ClaimAsync(playerId, request, cancellationToken);
        return result.Status switch
        {
            RewardClaimStatus.Success => Ok(result.Response),
            RewardClaimStatus.NotFound => ApiProblemDetails.Create(
                StatusCodes.Status404NotFound, "No session found for this player."),
            RewardClaimStatus.InvalidInput => ApiProblemDetails.Create(
                StatusCodes.Status400BadRequest, "The submitted game statistics are invalid or exceed the reward limit."),
            RewardClaimStatus.GoldLimitExceeded => ApiProblemDetails.Create(
                StatusCodes.Status409Conflict, "The reward would exceed the gold limit."),
            RewardClaimStatus.ProgressConflict => ApiProblemDetails.Create(
                StatusCodes.Status409Conflict, "Progress is missing or inconsistent with the recorded waves."),
            _ => throw new InvalidOperationException("Unknown reward claim status.")
        };
    }
}
