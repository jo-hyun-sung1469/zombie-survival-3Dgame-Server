using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using zombie_survival_3Dgame_Server.Contracts.Auth;

namespace zombie_survival_3Dgame_Server.Tests.Auth;

public sealed class AuthInputValidationTests
{
    [Theory]
    [InlineData("abc")]
    [InlineData("123")]
    [InlineData("player123")]
    [InlineData("생존자")]
    [InlineData("ㅋㅋㅋ")]
    [InlineData("ㅏㅑㅓ")]
    [InlineData("a1한ㄱㅏ")]
    public void Validate_AllowedUserName_AcceptsRegistrationAndAvailability(string userName)
    {
        // Given
        var registration = CreateRegistration(userName: userName);
        var availability = new UserNameAvailabilityRequest { UserName = userName };

        // When
        var registrationErrors = Validate(registration);
        var availabilityErrors = Validate(availability);

        // Then
        registrationErrors.Should().BeEmpty();
        availabilityErrors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Player1")]
    [InlineData(" abc")]
    [InlineData("abc ")]
    [InlineData("a bc")]
    [InlineData("abc\n")]
    [InlineData("ab\tc")]
    [InlineData("ab\0c")]
    [InlineData("ab\u200Bc")]
    [InlineData("abc\u00A0")]
    [InlineData("\u1100\u1161\u1102\u1161")]
    [InlineData("ａｂｃ")]
    [InlineData("аbc")]
    [InlineData("ab😀")]
    [InlineData("ab_c")]
    [InlineData("ab-c")]
    [InlineData("<abc>")]
    public void Validate_InvalidUserName_RejectsBothDtos(string userName)
    {
        // Given
        var registration = CreateRegistration(userName: userName);
        var availability = new UserNameAvailabilityRequest { UserName = userName };

        // When
        var registrationErrors = Validate(registration);
        var availabilityErrors = Validate(availability);

        // Then
        registrationErrors.Should().Contain(x => x.MemberNames.Contains("UserName"));
        availabilityErrors.Should().Contain(x => x.MemberNames.Contains("UserName"));
    }

    [Theory]
    [InlineData(2, false)]
    [InlineData(3, true)]
    [InlineData(30, true)]
    [InlineData(31, false)]
    public void Validate_UserNameLength_UsesSameBounds(int length, bool expectedValid)
    {
        // Given
        var userName = new string('가', length);

        // When
        var registrationErrors = Validate(CreateRegistration(userName: userName));
        var availabilityErrors = Validate(new UserNameAvailabilityRequest { UserName = userName });

        // Then
        registrationErrors.Count.Should().Be(expectedValid ? 0 : 1);
        availabilityErrors.Count.Should().Be(expectedValid ? 0 : 1);
    }

    [Theory]
    [InlineData("Abc12/")]
    [InlineData("!@#$%^*-=+?/")]
    [InlineData("abcdef")]
    [InlineData("ABCDEF")]
    [InlineData("123456")]
    public void Validate_AllowedPassword_AcceptsWithoutCompositionRequirements(string password)
    {
        // Given
        var request = CreateRegistration(examplePassword: password);

        // When
        var errors = Validate(request);

        // Then
        errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("      ")]
    [InlineData(" Abc12")]
    [InlineData("Abc12 ")]
    [InlineData("Ab c12")]
    [InlineData("Abc12\n")]
    [InlineData("Abc12\r\n")]
    [InlineData("Abc12\t")]
    [InlineData("Abc12\0")]
    [InlineData("Abc12\u200B")]
    [InlineData("Abc12한")]
    [InlineData("Abc12é")]
    [InlineData("Abc12😀")]
    [InlineData("Abc12.")]
    [InlineData("Abc12,")]
    [InlineData("Abc12_")]
    [InlineData("Abc12(")]
    [InlineData("Abc12)")]
    [InlineData("Abc12<")]
    [InlineData("Abc12>")]
    [InlineData("Abc12&")]
    [InlineData("Abc12'")]
    [InlineData("Abc12\"")]
    [InlineData("Abc12`")]
    [InlineData("Abc12\\")]
    [InlineData("Abc12:")]
    [InlineData("Abc12;")]
    [InlineData("Abc12[")]
    [InlineData("Abc12]")]
    [InlineData("Abc12{")]
    [InlineData("Abc12}")]
    [InlineData("Abc12|")]
    [InlineData("Abc12~")]
    public void Validate_DisallowedPassword_ReportsPasswordError(string password)
    {
        // Given
        var request = CreateRegistration(examplePassword: password);

        // When
        var errors = Validate(request);

        // Then
        errors.Should().Contain(x => x.MemberNames.Contains("Password"));
    }

    [Theory]
    [InlineData(5, false)]
    [InlineData(6, true)]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void Validate_PasswordLength_UsesExistingBounds(int length, bool expectedValid)
    {
        // Given
        var request = CreateRegistration(examplePassword: new string('a', length));

        // When
        var errors = Validate(request);

        // Then
        errors.Count.Should().Be(expectedValid ? 0 : 1);
    }

    [Fact]
    public void Validate_LegacyLogin_DoesNotApplyRegistrationCharacterRules()
    {
        // Given
        var request = new LoginRequest { UserName = "Legacy_Player", Password = "example 기존 password._()" };

        // When
        var errors = Validate(request);

        // Then
        errors.Should().BeEmpty();
    }

    private static RegisterRequest CreateRegistration(string userName = "player1", string examplePassword = "Abc12/") => new()
    {
        UserName = userName,
        Password = examplePassword,
        Email = "player@example.com",
        EmailVerificationId = "verified-id"
    };

    private static List<ValidationResult> Validate(object request)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);
        return results;
    }
}
