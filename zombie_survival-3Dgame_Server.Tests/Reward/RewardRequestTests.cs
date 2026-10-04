using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using FluentAssertions;
using zombie_survival_3Dgame_Server.Contracts.Reward;

namespace zombie_survival_3Dgame_Server.Tests.Reward;

public sealed class RewardRequestTests
{
    [Theory]
    [InlineData("{}")]
    [InlineData("{\"survivalTime\":0,\"clearWave\":0,\"killZombies\":0}")]
    [InlineData("{\"sessionId\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"clearWave\":0,\"killZombies\":0}")]
    [InlineData("{\"sessionId\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"survivalTime\":0,\"killZombies\":0}")]
    [InlineData("{\"sessionId\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"survivalTime\":0,\"clearWave\":0}")]
    public void Deserialize_MissingRequiredField_RejectsRequest(string json)
    {
        // Given
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        // When
        var act = () => JsonSerializer.Deserialize<RewardRequest>(json, options);

        // Then
        act.Should().Throw<JsonException>();
    }

    [Theory]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", 0f, 0, 0, true)]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", 3000f, 1, 1, true)]
    [InlineData("", 0f, 0, 0, false)]
    [InlineData("unknown", 0f, 0, 0, false)]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", -1f, 0, 0, false)]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", float.NaN, 0, 0, false)]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", float.PositiveInfinity, 0, 0, false)]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", 0f, -1, 0, false)]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", 0f, 0, -1, false)]
    public void Validate_ReportFields_EnforcesInputRules(
        string sessionId, float survivalTime, int clearWave, int killZombies, bool expectedValidity)
    {
        // Given
        var request = new RewardRequest
        {
            SessionId = sessionId, SurvivalTime = survivalTime, ClearWave = clearWave, KillZombies = killZombies
        };

        // When
        var valid = Validator.TryValidateObject(request, new ValidationContext(request), [], validateAllProperties: true);

        // Then
        valid.Should().Be(expectedValidity);
    }
}
