using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using zombie_servival_3Dgame_Server.Common;
using zombie_servival_3Dgame_Server.Contracts.WeaponUpgrade;

namespace zombie_servival_3Dgame_Server.WeaponUpgrade;

[ApiController]
[Route("api/weapon-upgrades")]
[Authorize]
public sealed class WeaponUpgradeController(IWeaponUpgradeService weaponUpgradeService) : ControllerBase
{
    [HttpPost("{weaponName}")]
    public async Task<ActionResult<UpgradeWeaponResponse>> UpgradeAsync(
        string weaponName,
        CancellationToken cancellationToken)
    {
        var playerId = User.FindFirst("userId")?.Value;
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return ApiProblemDetails.Create(StatusCodes.Status401Unauthorized, "Token does not contain a valid user id.");
        }

        var result = await weaponUpgradeService.UpgradeAsync(playerId, weaponName, cancellationToken);
        return result.Status switch
        {
            WeaponUpgradeStatus.Success => Ok(result.Response),
            WeaponUpgradeStatus.WeaponNotFound => ApiProblemDetails.Create(
                StatusCodes.Status404NotFound,
                "Weapon was not found."),
            WeaponUpgradeStatus.WeaponNotOwned => ApiProblemDetails.Create(
                StatusCodes.Status404NotFound,
                "Player does not own this weapon."),
            WeaponUpgradeStatus.InsufficientGold => ApiProblemDetails.Create(
                StatusCodes.Status409Conflict,
                "Not enough gold to upgrade this weapon.",
                extensions: new Dictionary<string, object?>
                {
                    ["requiredGold"] = result.RequiredGold,
                    ["currentGold"] = result.CurrentGold
                }),
            WeaponUpgradeStatus.MaxLevelReached => ApiProblemDetails.Create(
                StatusCodes.Status409Conflict,
                "Weapon is already at max level.",
                extensions: new Dictionary<string, object?>
                {
                    ["maxLevel"] = result.MaxLevel,
                    ["currentGold"] = result.CurrentGold
                }),
            _ => ApiProblemDetails.Create(StatusCodes.Status500InternalServerError, "Unexpected weapon upgrade status.")
        };
    }
}
