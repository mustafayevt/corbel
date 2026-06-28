using Corbel.Common.Errors;

namespace Corbel.Domain.Exceptions;

/// <summary>
/// A state-dependent domain invariant: archiving a note twice surfaces as 422. (Shape rules like "title is
/// required" are caught earlier by FluentValidation as 400 — see <see cref="Corbel.Domain.Entities.Note"/>.)
/// </summary>
public sealed class NoteAlreadyArchivedException()
    : DomainException(ErrorCodes.NoteAlreadyArchived, "The note is already archived.");
