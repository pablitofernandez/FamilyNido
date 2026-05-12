using FluentValidation;

namespace FamilyNido.Api.Features.Auth;

/// <summary>
/// Centralized password complexity requirements. Plain enough to remember,
/// strict enough to keep the obvious "1234" / "password" attacks out.
/// Reused by both <see cref="SetLocalPassword"/> and the invitation
/// "accept-local" path.
/// </summary>
internal static class PasswordPolicy
{
    /// <summary>Minimum number of characters.</summary>
    public const int MinLength = 8;

    /// <summary>Maximum length we ever accept (defends the hash function from absurd inputs).</summary>
    public const int MaxLength = 256;

    /// <summary>
    /// Rule helper used by FluentValidation builders.
    /// Requires length, at least one letter, and at least one digit.
    /// </summary>
    public static IRuleBuilderOptions<T, string> Password<T>(this IRuleBuilder<T, string> rule)
    {
        return rule
            .NotEmpty()
            .MinimumLength(MinLength).WithMessage($"Password must be at least {MinLength} characters.")
            .MaximumLength(MaxLength)
            .Must(s => s.Any(char.IsLetter)).WithMessage("Password must contain at least one letter.")
            .Must(s => s.Any(char.IsDigit)).WithMessage("Password must contain at least one digit.");
    }
}
