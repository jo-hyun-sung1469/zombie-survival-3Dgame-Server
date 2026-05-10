using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using zombie_servival_3Dgame_Server.Contracts.Inventory;

namespace zombie_servival_3Dgame_Server.Inventory;

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
            return Unauthorized(new { message = "Token does not contain a valid user id." });
        }

        try
        {
            var savedData = await inventoryService.SaveAsync(playerId, request, cancellationToken);
            return Ok(savedData);
        }
        catch (InvalidWeaponStateException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpGet("me")]
    public async Task<ActionResult<PlayerSaveResponse>> GetMyDataAsync(CancellationToken cancellationToken)
    {
        var playerId = User.FindFirst("userId")?.Value;
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return Unauthorized(new { message = "Token does not contain a valid user id." });
        }

        var saveData = await inventoryService.GetByPlayerIdAsync(playerId, cancellationToken);
        if (saveData is null)
        {
            return NotFound(new { message = "No save data found for this player." });
        }

        return Ok(saveData);
    }
}
