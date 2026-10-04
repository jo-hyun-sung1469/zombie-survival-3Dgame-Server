using FluentAssertions;
using zombie_survival_3Dgame_Server.Reward;

namespace zombie_survival_3Dgame_Server.Tests.Reward;

public sealed class RewardCalculatorTests
{
    [Theory]
    [InlineData(0f, 0, 0, 0)]
    [InlineData(100f, 2, 10, 550)]
    [InlineData(1.25f, 1, 1, 108)]
    [InlineData(0f, 21474836, 9, 2147483645)]
    [InlineData(0.75f, 21474836, 9, int.MaxValue)]
    public void Calculate_ValidInputs_ReturnsTruncatedGold(
        float survivalTime, int clearWave, int killZombies, int expectedGold)
    {
        // Given: survival time, waves and kills are supplied by the theory.

        // When
        var result = RewardCalculator.Calculate(survivalTime, clearWave, killZombies);

        // Then
        result.Should().Be(expectedGold);
    }

    [Theory]
    [InlineData(2000f)]
    [InlineData(2000.5f)]
    [InlineData(float.MaxValue)]
    public void Calculate_SurvivalTimeAtOrAboveLimit_CapsTimeContribution(float survivalTime)
    {
        // Given
        const int clearWave = 2;
        const int killZombies = 10;

        // When
        var result = RewardCalculator.Calculate(survivalTime, clearWave, killZombies);

        // Then
        result.Should().Be(6250);
    }

    [Theory]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void Calculate_InvalidSurvivalTime_ThrowsArgumentOutOfRangeException(float survivalTime)
    {
        // Given
        const int clearWave = 0;
        const int killZombies = 0;

        // When
        var act = () => RewardCalculator.Calculate(survivalTime, clearWave, killZombies);

        // Then
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("survivalTime");
    }

    [Theory]
    [InlineData(-1, 0, "clearWave")]
    [InlineData(0, -1, "killZombies")]
    public void Calculate_NegativeCount_ThrowsArgumentOutOfRangeException(
        int clearWave, int killZombies, string parameterName)
    {
        // Given
        const float survivalTime = 0;

        // When
        var act = () => RewardCalculator.Calculate(survivalTime, clearWave, killZombies);

        // Then
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName(parameterName);
    }

    [Theory]
    [InlineData(0f, int.MaxValue, 0)]
    [InlineData(0f, 0, int.MaxValue)]
    [InlineData(0f, 21474836, 10)]
    [InlineData(1f, 21474836, 9)]
    [InlineData(2000f, int.MaxValue, int.MaxValue)]
    public void Calculate_RewardExceedsIntRange_ThrowsOverflowException(
        float survivalTime, int clearWave, int killZombies)
    {
        // Given: inputs exercise individual products and the combined reward limit.

        // When
        var act = () => RewardCalculator.Calculate(survivalTime, clearWave, killZombies);

        // Then
        act.Should().Throw<OverflowException>();
    }
}
