using FluentAssertions;
using zombie_survival_3Dgame_Server.Options;

namespace zombie_survival_3Dgame_Server.Tests.Options;

public sealed class ServerOptionsValidatorTests
{
    [Fact]
    public void Validate_NonPositiveGachaCost_Fails()
    {
        // Given
        var validator = new GachaOptionsValidator();
        var options = new GachaOptions { PullCost = 0 };

        // When
        var result = validator.Validate(null, options);

        // Then
        result.Failed.Should().BeTrue();
    }

    [Fact]
    public void Validate_MissingJwtSettings_Fails()
    {
        // Given
        var validator = new JwtOptionsValidator();
        var options = new JwtOptions();

        // When
        var result = validator.Validate(null, options);

        // Then
        result.Failed.Should().BeTrue();
    }

    [Fact]
    public void Validate_UnencryptedSmtp_Fails()
    {
        // Given
        var validator = new SmtpEmailOptionsValidator();
        var options = new SmtpEmailOptions
        {
            Host = "smtp.example.invalid",
            Port = 587,
            EnableSsl = false,
            FromAddress = "server@example.invalid"
        };

        // When
        var result = validator.Validate(null, options);

        // Then
        result.Failed.Should().BeTrue();
    }
}
