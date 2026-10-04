using System.ComponentModel.DataAnnotations;

namespace zombie_survival_3Dgame_Server.Contracts.Reward;

public sealed class RewardRequest : IValidatableObject
{
    [Required]
    [StringLength(32, MinimumLength = 32)]
    public required string SessionId { get; init; }

    public required float SurvivalTime { get; init; }

    [Range(0, int.MaxValue)]
    public required int ClearWave { get; init; }

    [Range(0, int.MaxValue)]
    public required int KillZombies { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!float.IsFinite(SurvivalTime) || SurvivalTime < 0)
        {
            yield return new ValidationResult(
                "Survival time must be finite and non-negative.", [nameof(SurvivalTime)]);
        }
    }
}
