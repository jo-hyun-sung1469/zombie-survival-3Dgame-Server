using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using zombie_survival_3Dgame_Server.Common;
using zombie_survival_3Dgame_Server.Contracts.Inventory;

namespace zombie_survival_3Dgame_Server.Inventory;

[ApiController]
[Route("api/player-data")]
[Authorize]
public sealed class InventoryController(IInventoryService inventoryService) : ControllerBase
{
    [HttpPost("save")]
    public async Task<ActionResult<PlayerSaveResponse>> SaveAsync(
        [FromBody] SavePlayerDataRequest request,
        CancellationToken cancellationToken)
    {
        var playerId = User.FindFirst("userId")?.Value;
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return ApiProblemDetails.Create(StatusCodes.Status401Unauthorized, "Token does not contain a valid user id.");
        }

        var savedData = await inventoryService.SaveAsync(playerId, request, cancellationToken);
        return Ok(savedData);
    }

    [HttpGet("me")]
    public async Task<ActionResult<PlayerSaveResponse>> GetMyDataAsync(CancellationToken cancellationToken)
    {
        var playerId = User.FindFirst("userId")?.Value;
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return ApiProblemDetails.Create(StatusCodes.Status401Unauthorized, "Token does not contain a valid user id.");
        }

        var saveData = await inventoryService.GetByPlayerIdAsync(playerId, cancellationToken);
        if (saveData is null)
        {
            return ApiProblemDetails.Create(StatusCodes.Status404NotFound, "No save data found for this player.");
        }

        return Ok(saveData);
    }
}
