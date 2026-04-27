using Microsoft.EntityFrameworkCore;
using zombie_servival_3Dgame_Server.Contracts.Gacha;
using zombie_servival_3Dgame_Server.Data;
using zombie_servival_3Dgame_Server.Inventory;

namespace zombie_servival_3Dgame_Server.Gacha;

public sealed class GachaService(GameDbContext dbContext, ILogger<GachaService> logger) : IGachaService
{
    private const int PullCostGold = 500;

    private static readonly IReadOnlyList<GachaRewardDefinition> RewardCatalog =
    [
        new("RustySword", "Common", 40),
        new("Shotgun", "Rare", 30),
        new("AssaultRifle", "Epic", 20),
        new("PlasmaCannon", "Legendary", 10)
    ];

    public async Task<GachaPoolResponse> GetPoolAsync(string playerId, CancellationToken cancellationToken)
    {
        var saveData = await GetOrCreateSaveDataAsync(playerId, cancellationToken);
        var availableRewards = GetAvailableRewards(saveData);

        return new GachaPoolResponse
        {
            IsCompleted = availableRewards.Count == 0,
            RemainingRewardCount = availableRewards.Count,
            Rewards = availableRewards
                .Select(x => new GachaRewardProbabilityResponse
                {
                    RewardName = x.Definition.RewardName,
                    Rarity = x.Definition.Rarity
                })
                .ToList()
        };
    }

    public async Task<GachaPullResult> PullAsync(string playerId, CancellationToken cancellationToken)
    {
        var saveData = await GetOrCreateSaveDataAsync(playerId, cancellationToken);
        var availableRewards = GetAvailableRewards(saveData);
        if (availableRewards.Count == 0)
        {
            return new GachaPullResult
            {
                Status = GachaPullStatus.Completed,
                CurrentGold = saveData.Gold,
                RequiredGold = PullCostGold
            };
        }

        if (saveData.Gold < PullCostGold)
        {
            return new GachaPullResult
            {
                Status = GachaPullStatus.InsufficientGold,
                CurrentGold = saveData.Gold,
                RequiredGold = PullCostGold
            };
        }

        var selectedReward = SelectReward(availableRewards);
        var existingState = saveData.WeaponStates.SingleOrDefault(
            x => string.Equals(x.WeaponName, selectedReward.Definition.RewardName, StringComparison.OrdinalIgnoreCase));

        if (existingState is null)
        {
            saveData.WeaponStates.Add(new PlayerWeaponState
            {
                WeaponName = selectedReward.Definition.RewardName,
                IsOwned = true
            });
        }
        else
        {
            existingState.IsOwned = true;
        }

        saveData.Gold -= PullCostGold;
        saveData.UpdatedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Recorded gacha pull for player {PlayerId} with reward {RewardName} and rarity {Rarity}.",
            playerId,
            selectedReward.Definition.RewardName,
            selectedReward.Definition.Rarity);

        return new GachaPullResult
        {
            Status = GachaPullStatus.Success,
            RequiredGold = PullCostGold,
            CurrentGold = saveData.Gold,
            Response = new GachaPullResponse
            {
                RewardName = selectedReward.Definition.RewardName,
                Rarity = selectedReward.Definition.Rarity,
                RemainingRewardCount = availableRewards.Count - 1,
                SpentGold = PullCostGold,
                RemainingGold = saveData.Gold
            }
        };
    }

    private async Task<PlayerSaveData> GetOrCreateSaveDataAsync(string playerId, CancellationToken cancellationToken)
    {
        var saveData = await dbContext.PlayerSaveData
            .Include(x => x.WeaponStates)
            .SingleOrDefaultAsync(x => x.PlayerId == playerId, cancellationToken);

        if (saveData is not null)
        {
            return saveData;
        }

        saveData = new PlayerSaveData
        {
            PlayerId = playerId,
            UpdatedAtUtc = DateTime.UtcNow
        };

        dbContext.PlayerSaveData.Add(saveData);
        await dbContext.SaveChangesAsync(cancellationToken);
        return saveData;
    }

    private static List<AvailableReward> GetAvailableRewards(PlayerSaveData saveData)
    {
        var ownedRewards = saveData.WeaponStates
            .Where(x => x.IsOwned)
            .Select(x => x.WeaponName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var remainingRewards = RewardCatalog
            .Where(x => !ownedRewards.Contains(x.RewardName))
            .ToList();

        if (remainingRewards.Count == 0)
        {
            return [];
        }

        var totalWeight = remainingRewards.Sum(x => x.Weight);
        return remainingRewards
            .Select(x => new AvailableReward(x, x.Weight / totalWeight))
            .ToList();
    }

    private static AvailableReward SelectReward(IReadOnlyList<AvailableReward> rewards)
    {
        var roll = Random.Shared.NextDouble();
        var cumulativeProbability = 0d;

        foreach (var reward in rewards)
        {
            cumulativeProbability += reward.Probability;
            if (roll <= cumulativeProbability)
            {
                return reward;
            }
        }

        return rewards[^1];
    }

    private sealed record GachaRewardDefinition(string RewardName, string Rarity, double Weight);

    private sealed record AvailableReward(GachaRewardDefinition Definition, double Probability);
}
