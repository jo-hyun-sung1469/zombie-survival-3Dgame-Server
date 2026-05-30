using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using zombie_survival_3Dgame_Server.Contracts.Gacha;
using zombie_survival_3Dgame_Server.Data;
using zombie_survival_3Dgame_Server.Firearm.Models;
using zombie_survival_3Dgame_Server.Inventory;
using zombie_survival_3Dgame_Server.Options;

namespace zombie_survival_3Dgame_Server.Gacha;

public sealed class GachaService(
    GameDbContext dbContext,
    IPlayerSaveDataStore playerSaveDataStore,
    IOptions<GachaOptions> gachaOptions) : IGachaService
{
    private readonly GachaOptions _gachaOptions = gachaOptions.Value;

    public async Task<GachaPoolResponse> GetPoolAsync(string playerId, CancellationToken cancellationToken)
    {
        var saveData = await dbContext.PlayerSaveData
            .AsNoTracking()
            .Include(x => x.WeaponStates)
            .SingleOrDefaultAsync(x => x.PlayerId == playerId, cancellationToken);

        var currentGold = saveData?.Gold ?? 0;
        var ownedWeaponIds = ToOwnedWeaponIdSet(saveData);
        var firearms = await dbContext.FirearmDefinitions
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var rewards = firearms
            .Where(x => x.GachaProbability > 0)
            .OrderByDescending(x => x.GachaProbability)
            .Select(x => new GachaRewardProbabilityResponse
            {
                RewardName = x.Name,
                Probability = x.GachaProbability,
                IsOwned = ownedWeaponIds.Contains(x.Id)
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

        var ownedWeaponIds = ToOwnedWeaponIdSet(saveData);
        var firearms = await dbContext.FirearmDefinitions
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var availableRewards = firearms
            .Where(x => x.GachaProbability > 0 && !ownedWeaponIds.Contains(x.Id))
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

        var reward = SelectReward(availableRewards);
        saveData.Gold -= _gachaOptions.PullCost;
        saveData.UpdatedAtUtc = DateTime.UtcNow;

        var weaponState = saveData.WeaponStates
            .SingleOrDefault(x => x.FirearmDefinitionId == reward.Id);

        if (weaponState is null)
        {
            saveData.WeaponStates.Add(new PlayerWeaponState
            {
                FirearmDefinitionId = reward.Id,
                WeaponName = reward.Name,
                IsOwned = true,
                WeaponLevel = 1,
                Damage = reward.Damage,
                FireRate = reward.FireRate,
                MagazineSize = reward.MagazineSize,
                ReloadTimeSeconds = reward.ReloadTimeSeconds,
                RangeMeters = reward.RangeMeters,
                CriticalMultiplier = reward.CriticalMultiplier
            });
        }
        else
        {
            weaponState.IsOwned = true;
            weaponState.WeaponName = reward.Name;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new GachaPullResult
        {
            Status = GachaPullStatus.Success,
            RequiredGold = _gachaOptions.PullCost,
            CurrentGold = saveData.Gold,
            Response = new GachaPullResponse
            {
                RewardName = reward.Name,
                CurrentGold = saveData.Gold,
                RemainingRewardCount = availableRewards.Count - 1,
                UpdatedAtUtc = saveData.UpdatedAtUtc
            }
        };
    }

    private static HashSet<int> ToOwnedWeaponIdSet(PlayerSaveData? saveData)
    {
        return saveData?.WeaponStates
            .Where(x => x.IsOwned)
            .Select(x => x.FirearmDefinitionId)
            .ToHashSet()
            ?? [];
    }

    private static FirearmDefinition SelectReward(IReadOnlyList<FirearmDefinition> rewards)
    {
        var totalWeight = rewards.Sum(x => x.GachaProbability);
        var roll = Random.Shared.NextDouble() * totalWeight;

        foreach (var reward in rewards)
        {
            roll -= reward.GachaProbability;
            if (roll <= 0)
            {
                return reward;
            }
        }

        return rewards[^1];
    }
}
