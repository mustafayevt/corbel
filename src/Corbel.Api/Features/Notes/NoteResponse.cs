using System.Linq.Expressions;
using Corbel.Domain.Entities;

namespace Corbel.Features.Notes;

/// <summary>
/// The wire contract for a note. The entity is never serialized directly — slices project to this record so the
/// API surface stays decoupled from the persistence model.
/// </summary>
/// <param name="Id">The note's unique identifier.</param>
/// <param name="Title">The note's title.</param>
/// <param name="Content">The note's body (empty string when it has no content).</param>
/// <param name="IsArchived">Whether the note has been archived.</param>
/// <param name="CreatedAtUtc">When the note was created (UTC).</param>
public sealed record NoteResponse(Guid Id, string Title, string Content, bool IsArchived, DateTimeOffset CreatedAtUtc)
{
    /// <summary>EF projection for read queries — the projected shape lives here once and is translated to SQL.</summary>
    public static readonly Expression<Func<Note, NoteResponse>> Projection =
        note => new NoteResponse(note.Id, note.Title, note.Content, note.IsArchived, note.CreatedAtUtc);

    /// <summary>Maps an already-materialized <see cref="Note"/> (the expression above can't run on an in-memory object).</summary>
    public static NoteResponse From(Note note)
        => new(note.Id, note.Title, note.Content, note.IsArchived, note.CreatedAtUtc);
}
