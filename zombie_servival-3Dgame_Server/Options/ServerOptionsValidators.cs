using System.Text;
using Microsoft.Extensions.Options;
using static zombie_survival_3Dgame_Server.Options.OptionsValidation;

namespace zombie_survival_3Dgame_Server.Options;

public sealed class JwtOptionsValidator : IValidateOptions<JwtOptions>
{
    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        var failures = new List<string>();
        RequireText(options.Issuer, "Jwt:Issuer", failures);
        RequireText(options.Audience, "Jwt:Audience", failures);

        if (Encoding.UTF8.GetByteCount(options.SecretKey ?? string.Empty) < 32)
        {
            failures.Add("Jwt:SecretKey must contain at least 32 UTF-8 bytes.");
        }

        RequireRange(options.ExpirationMinutes, 1, 1440, "Jwt:ExpirationMinutes", failures);
        return BuildResult(failures);
    }
}

public sealed class EmailAuthOptionsValidator : IValidateOptions<EmailAuthOptions>
{
    public ValidateOptionsResult Validate(string? name, EmailAuthOptions options)
    {
        var failures = new List<string>();
        RequireRange(options.CodeLength, 4, 12, "EmailAuth:CodeLength", failures);
        RequireRange(options.ExpirationMinutes, 1, 60, "EmailAuth:ExpirationMinutes", failures);
        RequireRange(options.MaxAttempts, 1, 20, "EmailAuth:MaxAttempts", failures);
        return BuildResult(failures);
    }
}

public sealed class SmtpEmailOptionsValidator : IValidateOptions<SmtpEmailOptions>
{
    public ValidateOptionsResult Validate(string? name, SmtpEmailOptions options)
    {
        var failures = new List<string>();
        RequireText(options.Host, "SmtpEmail:Host", failures);
        RequireText(options.FromAddress, "SmtpEmail:FromAddress", failures);
        RequireRange(options.Port, 1, 65535, "SmtpEmail:Port", failures);

        if (!options.EnableSsl)
        {
            failures.Add("SmtpEmail:EnableSsl must be true.");
        }

        if (!string.IsNullOrWhiteSpace(options.UserName) && string.IsNullOrWhiteSpace(options.Password))
        {
            failures.Add("SmtpEmail:Password is required when SmtpEmail:UserName is configured.");
        }

        return BuildResult(failures);
    }
}

public sealed class GachaOptionsValidator : IValidateOptions<GachaOptions>
{
    public ValidateOptionsResult Validate(string? name, GachaOptions options)
    {
        return options.PullCost > 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail("Gacha:PullCost must be greater than zero.");
    }
}

public sealed class WeaponUpgradeOptionsValidator : IValidateOptions<WeaponUpgradeOptions>
{
    public ValidateOptionsResult Validate(string? name, WeaponUpgradeOptions options)
    {
        var failures = new List<string>();
        RequireRange(options.MaxLevel, 2, 1000, "WeaponUpgrade:MaxLevel", failures);
        RequireFiniteRange(options.CostIncreaseRate, 0, 10, "WeaponUpgrade:CostIncreaseRate", failures);
        RequireFiniteRange(options.StatIncreaseRate, 0, 10, "WeaponUpgrade:StatIncreaseRate", failures);

        if (options.BaseCostsByRarity is null || options.BaseCostsByRarity.Count == 0)
        {
            failures.Add("WeaponUpgrade:BaseCostsByRarity must contain at least one entry.");
        }
        else if (options.BaseCostsByRarity.Any(x => string.IsNullOrWhiteSpace(x.Key) || x.Value <= 0))
        {
            failures.Add("WeaponUpgrade:BaseCostsByRarity keys must be non-empty and costs must be greater than zero.");
        }

        return BuildResult(failures);
    }
}

public sealed class PlayerOptionsValidator : IValidateOptions<PlayerOptions>
{
    public ValidateOptionsResult Validate(string? name, PlayerOptions options)
    {
        var failures = new List<string>();
        if (options.BaseStats.MaxHealth <= 0
            || options.BaseStats.AttackPower < 0
            || options.BaseStats.Defense < 0)
        {
            failures.Add("Player base health must be positive and combat stats must be non-negative.");
        }

        if (!double.IsFinite(options.BaseStats.MoveSpeed) || options.BaseStats.MoveSpeed <= 0)
        {
            failures.Add("Player:BaseStats:MoveSpeed must be finite and positive.");
        }

        if (!double.IsFinite(options.BaseStats.HeadshotDamageMultiplier)
            || options.BaseStats.HeadshotDamageMultiplier <= 0)
        {
            failures.Add("Player:BaseStats:HeadshotDamageMultiplier must be finite and positive.");
        }

        RequireRange(options.StatUpgrades.MaxLevel, 1, 1000, "Player:StatUpgrades:MaxLevel", failures);

        if (options.StatUpgrades.BaseCost <= 0)
        {
            failures.Add("Player:StatUpgrades:BaseCost must be greater than zero.");
        }

        RequireFiniteRange(
            options.StatUpgrades.CostIncreaseRate,
            0,
            10,
            "Player:StatUpgrades:CostIncreaseRate",
            failures);

        if (options.StatUpgrades.IncreasesByStat is null || options.StatUpgrades.IncreasesByStat.Count == 0)
        {
            failures.Add("Player:StatUpgrades:IncreasesByStat must contain at least one entry.");
        }
        else if (options.StatUpgrades.IncreasesByStat.Any(
                     x => string.IsNullOrWhiteSpace(x.Key) || !double.IsFinite(x.Value) || x.Value <= 0))
        {
            failures.Add("Player stat upgrade names must be non-empty and increases must be finite and positive.");
        }

        return BuildResult(failures);
    }
}

public sealed class PlayerDefaultDataOptionsValidator : IValidateOptions<PlayerDefaultDataOptions>
{
    public ValidateOptionsResult Validate(string? name, PlayerDefaultDataOptions options)
    {
        var failures = new List<string>();
        if (options.InitialGold < 0)
        {
            failures.Add("PlayerDefaultData:InitialGold must be non-negative.");
        }

        RequireRange(
            options.DefaultWeaponLevel,
            1,
            1000,
            "PlayerDefaultData:DefaultWeaponLevel",
            failures);

        if (options.WeaponStates is not null && options.WeaponStates.Keys.Any(string.IsNullOrWhiteSpace))
        {
            failures.Add("PlayerDefaultData:WeaponStates cannot contain an empty weapon name.");
        }

        if (options.StatUpgradeLevels is not null
            && options.StatUpgradeLevels.Any(x => string.IsNullOrWhiteSpace(x.Key) || x.Value < 1))
        {
            failures.Add("PlayerDefaultData:StatUpgradeLevels must use non-empty names and levels of at least one.");
        }

        return BuildResult(failures);
    }
}

internal static class OptionsValidation
{
    public static ValidateOptionsResult BuildResult(IReadOnlyCollection<string> failures)
    {
        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    public static void RequireText(string? value, string path, ICollection<string> failures)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            failures.Add($"{path} is required.");
        }
    }

    public static void RequireRange(int value, int minimum, int maximum, string path, ICollection<string> failures)
    {
        if (value < minimum || value > maximum)
        {
            failures.Add($"{path} must be between {minimum} and {maximum}.");
        }
    }

    public static void RequireFiniteRange(
        double value,
        double minimum,
        double maximum,
        string path,
        ICollection<string> failures)
    {
        if (!double.IsFinite(value) || value < minimum || value > maximum)
        {
            failures.Add($"{path} must be finite and between {minimum} and {maximum}.");
        }
    }
}
