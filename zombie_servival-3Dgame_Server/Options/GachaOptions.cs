namespace zombie_servival_3Dgame_Server.Options;

public sealed class GachaOptions
{
    public const string SectionName = "Gacha";

    public int PullCost { get; init; } = 100;
    public List<GachaRewardOption> Rewards { get; init; } = [];
}

public sealed class GachaRewardOption
{
    public string RewardName { get; init; } = string.Empty;
    public double Probability { get; init; }
}
