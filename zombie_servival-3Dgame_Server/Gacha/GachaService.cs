using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using zombie_servival_3Dgame_Server.Contracts.Gacha;
using zombie_servival_3Dgame_Server.Data;
using zombie_servival_3Dgame_Server.Inventory;
using zombie_servival_3Dgame_Server.Options;

namespace zombie_servival_3Dgame_Server.Gacha;

public sealed class GachaService(
    GameDbContext dbContext,
    IPlayerSaveDataStore playerSaveDataStore,
    IOptions<GachaOptions> gachaOptions,
    IOptions<FirearmOptions> firearmOptions) : IGachaService
{
    private readonly GachaOptions _gachaOptions = gachaOptions.Value;
    private readonly IReadOnlyList<FirearmDefinitionOption> _firearms = firearmOptions.Value.Weapons;

    public async Task<GachaPoolResponse> GetPoolAsync(string playerId, CancellationToken cancellationToken)
    {
        var saveData = await dbContext.PlayerSaveData
            .AsNoTracking()
            .Include(x => x.WeaponStates)
            .SingleOrDefaultAsync(x => x.PlayerId == playerId, cancellationToken);

        var currentGold = saveData?.Gold ?? 0;
        var ownedWeapons = ToOwnedWeaponSet(saveData);
        var rewards = _firearms
            .Where(x => x.GachaProbability > 0)
            .OrderByDescending(x => x.GachaProbability)
            .Select(x => new GachaRewardProbabilityResponse
            {
                RewardName = x.Name,
                Probability = x.GachaProbability,
                IsOwned = ownedWeapons.Contains(x.Name)
            })
            .ToList();

        return new GachaPoolResponse
        {
            PullCost = _gachaOptions.PullCost,
            CurrentGold = currentGold,
            RemainingRewardCount = rewards.Count(x => !x.IsOwned),
            Rewards = rewards
        };
    }

    public async Task<GachaPullResult> PullAsync(string playerId, CancellationToken cancellationToken)
    {
        var saveData = await playerSaveDataStore.GetOrCreateAsync(playerId, cancellationToken);

        if (saveData.Gold < _gachaOptions.PullCost)
        {
            return new GachaPullResult
            {
                Status = GachaPullStatus.InsufficientGold,
                RequiredGold = _gachaOptions.PullCost,
                CurrentGold = saveData.Gold
            };
        }

        var ownedWeapons = ToOwnedWeaponSet(saveData);
        var availableRewards = _firearms
            .Where(x => x.GachaProbability > 0 && !ownedWeapons.Contains(x.Name))
            .ToList();

        if (availableRewards.Count == 0)
        {
            return new GachaPullResult
            {
                Status = GachaPullStatus.Completed,
                RequiredGold = _gachaOptions.PullCost,
                CurrentGold = saveData.Gold
            };
        }

        var rewardName = SelectReward(availableRewards);
        saveData.Gold -= _gachaOptions.PullCost;
        saveData.UpdatedAtUtc = DateTime.UtcNow;

        var weaponState = saveData.WeaponStates
            .SingleOrDefault(x => string.Equals(x.WeaponName, rewardName, StringComparison.OrdinalIgnoreCase));

        if (weaponState is null)
        {
            saveData.WeaponStates.Add(new PlayerWeaponState
            {
                WeaponName = rewardName,
                IsOwned = true
            });
        }
        else
        {
            weaponState.IsOwned = true;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new GachaPullResult
        {
            Status = GachaPullStatus.Success,
            RequiredGold = _gachaOptions.PullCost,
            CurrentGold = saveData.Gold,
            Response = new GachaPullResponse
            {
                RewardName = rewardName,
                CurrentGold = saveData.Gold,
                RemainingRewardCount = availableRewards.Count - 1,
                UpdatedAtUtc = saveData.UpdatedAtUtc
            }
        };
    }

    private static HashSet<string> ToOwnedWeaponSet(PlayerSaveData? saveData)
    {
        return saveData?.WeaponStates
            .Where(x => x.IsOwned)
            .Select(x => x.WeaponName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase)
            ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    private static string SelectReward(IReadOnlyList<FirearmDefinitionOption> rewards)
    {
        var totalWeight = rewards.Sum(x => x.GachaProbability);
        var roll = Random.Shared.NextDouble() * totalWeight;

        foreach (var reward in rewards)
        {
            roll -= reward.GachaProbability;
            if (roll <= 0)
            {
                return reward.Name;
            }
        }

        return rewards[^1].Name;
    }
}
