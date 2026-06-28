using Corbel.Common;
using FluentValidation;

namespace Corbel.Common.Validation;

/// <summary>
/// The shared shape rules for a note's title and content, so the create and update slices validate identically
/// and can't drift. Mirrors <see cref="PasswordRules"/> — one rule definition reused by every slice that accepts
/// note input. The bounds come from <see cref="NoteConstraints"/>, the same constants the EF configuration uses.
/// </summary>
public static class NoteValidationRules
{
    /// <summary>A note title is required and bounded to <see cref="NoteConstraints.TitleMaxLength"/>.</summary>
    public static IRuleBuilderOptions<T, string> NoteTitle<T>(this IRuleBuilder<T, string> rule)
        => rule.NotEmpty().MaximumLength(NoteConstraints.TitleMaxLength);

    /// <summary>Note content is optional and bounded to <see cref="NoteConstraints.ContentMaxLength"/>.</summary>
    public static IRuleBuilderOptions<T, string?> NoteContent<T>(this IRuleBuilder<T, string?> rule)
        => rule.MaximumLength(NoteConstraints.ContentMaxLength);
}
