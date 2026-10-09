using System.ComponentModel.DataAnnotations;

namespace zombie_survival_3Dgame_Server.Contracts.GameSession;

public sealed class WaveProgressRequest : IValidatableObject
{
    [Range(1, int.MaxValue)]
    public required int ClearWave { get; init; }
    [Range(0, int.MaxValue)]
    public required int KillZombies { get; init; }
    public required float SurvivalTimeSeconds { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!float.IsFinite(SurvivalTimeSeconds) || SurvivalTimeSeconds < 0)
            yield return new ValidationResult("Survival time must be finite and non-negative.", [nameof(SurvivalTimeSeconds)]);
    }
}
