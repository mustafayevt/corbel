using Corbel.Common;
using FluentValidation;

namespace Corbel.Common.Validation;

/// <summary>
/// The single password-complexity gate, shared by every slice that accepts a new password (register, change
/// password). FluentValidation runs in the pipeline <b>before</b> the handler, so a password that fails these
/// rules is rejected uniformly with a 400 validation problem — the same response whether or not the target
/// account exists. Identity itself is configured length-only (see <c>AuthServiceCollectionExtensions</c>), so a
/// password these rules accept can never be rejected later by <c>UserManager.CreateAsync</c>/<c>ChangePasswordAsync</c>;
/// that's what keeps registration from leaking account existence via a divergent validation outcome.
/// </summary>
public static class PasswordRules
{
    /// <summary>Length + character-class policy for a user-supplied password.</summary>
    public static IRuleBuilderOptions<T, string> Password<T>(this IRuleBuilder<T, string> rule)
        => rule
            .NotEmpty()
            .MinimumLength(PasswordPolicy.MinLength)
            .MaximumLength(PasswordPolicy.MaxLength)
            .Must(static value => value.Any(char.IsDigit))
                .WithMessage("Password must contain at least one digit.")
            .Must(static value => value.Any(char.IsLower))
                .WithMessage("Password must contain at least one lowercase letter.")
            .Must(static value => value.Any(char.IsUpper))
                .WithMessage("Password must contain at least one uppercase letter.");
}
