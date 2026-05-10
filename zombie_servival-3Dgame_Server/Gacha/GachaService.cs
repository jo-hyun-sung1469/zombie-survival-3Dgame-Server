using Microsoft.EntityFrameworkCore;
using zombie_servival_3Dgame_Server.Contracts.Gacha;
using zombie_servival_3Dgame_Server.Data;
using zombie_servival_3Dgame_Server.Inventory;

namespace zombie_servival_3Dgame_Server.Gacha;

public sealed class GachaService(GameDbContext dbContext) : IGachaService
{
    private const int PullCost = 100;

    private static readonly (string RewardName, double Probability)[] RewardTable =
    [
        ("Shotgun", 0.35),
        ("SMG", 0.25),
        ("AssaultRifle", 0.2),
        ("SniperRifle", 0.12),
        ("RocketLauncher", 0.08)
    ];

    public async Task<GachaPoolResponse> GetPoolAsync(string playerId, CancellationToken cancellationToken)
    {
        var saveData = await dbContext.PlayerSaveData
            .AsNoTracking()
            .Include(x => x.WeaponStates)
            .SingleOrDefaultAsync(x => x.PlayerId == playerId, cancellationToken);

        var currentGold = saveData?.Gold ?? 0;
        var ownedWeapons = ToOwnedWeaponSet(saveData);
        var rewards = RewardTable
            .OrderByDescending(x => x.Probability)
            .Select(x => new GachaRewardProbabilityResponse
            {
                RewardName = x.RewardName,
                Probability = x.Probability,
                IsOwned = ownedWeapons.Contains(x.RewardName)
            })
            .ToList();

        return new GachaPoolResponse
        {
            PullCost = PullCost,
            CurrentGold = currentGold,
            RemainingRewardCount = rewards.Count(x => !x.IsOwned),
            Rewards = rewards
        };
    }

    public async Task<GachaPullResult> PullAsync(string playerId, CancellationToken cancellationToken)
    {
        var saveData = await dbContext.PlayerSaveData
            .Include(x => x.WeaponStates)
            .SingleOrDefaultAsync(x => x.PlayerId == playerId, cancellationToken);

        if (saveData is null)
        {
            saveData = new PlayerSaveData
            {
                PlayerId = playerId,
                UpdatedAtUtc = DateTime.UtcNow
            };
            dbContext.PlayerSaveData.Add(saveData);
        }

        if (saveData.Gold < PullCost)
        {
            return new GachaPullResult
            {
                Status = GachaPullStatus.InsufficientGold,
                RequiredGold = PullCost,
                CurrentGold = saveData.Gold
            };
        }

        var ownedWeapons = ToOwnedWeaponSet(saveData);
        var availableRewards = RewardTable
            .Where(x => !ownedWeapons.Contains(x.RewardName))
            .ToList();

        if (availableRewards.Count == 0)
        {
            return new GachaPullResult
            {
                Status = GachaPullStatus.Completed,
                RequiredGold = PullCost,
                CurrentGold = saveData.Gold
            };
        }

        var rewardName = SelectReward(availableRewards);
        saveData.Gold -= PullCost;
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
            RequiredGold = PullCost,
            CurrentGold = saveData.Gold,
            Response = new GachaPullResponse
            {
                RewardName = rewardName,
                CurrentGold = saveData.Gold,
                RemainingRewardCount = RewardTable.Count(x => !string.Equals(x.RewardName, rewardName, StringComparison.OrdinalIgnoreCase) && !ownedWeapons.Contains(x.RewardName)),
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

    private static string SelectReward(IReadOnlyList<(string RewardName, double Probability)> rewards)
    {
        var totalWeight = rewards.Sum(x => x.Probability);
        var roll = Random.Shared.NextDouble() * totalWeight;

        foreach (var reward in rewards)
        {
            roll -= reward.Probability;
            if (roll <= 0)
            {
                return reward.RewardName;
            }
        }

        return rewards[^1].RewardName;
    }
}
