using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using zombie_servival_3Dgame_Server.Contracts.Firearm;

namespace zombie_servival_3Dgame_Server.Firearm;

[ApiController]
[Route("api/firearms")]
[Authorize]
public sealed class FirearmController(IFirearmService firearmService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<FirearmCollectionResponse>> GetAllAsync(CancellationToken cancellationToken)
    {
        var response = await firearmService.GetAllAsync(cancellationToken);
        return Ok(response);
    }

    [HttpGet("{weaponName}")]
    public async Task<ActionResult<FirearmStatsResponse>> GetByNameAsync(
        string weaponName,
        CancellationToken cancellationToken)
    {
        var response = await firearmService.GetByNameAsync(weaponName, cancellationToken);
        if (response is null)
        {
            return NotFound(new { message = "Firearm stats were not found." });
        }

        return Ok(response);
    }
}
