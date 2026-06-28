using System.Linq.Expressions;
using Corbel.Domain.Entities;

namespace Corbel.Features.Notes;

/// <summary>
/// The wire contract for a note. The entity is never serialized directly — slices project to this record (via
/// <see cref="Projection"/> in SQL, or <see cref="From"/> for an already-loaded entity), so the API surface stays
/// decoupled from the persistence model.
/// </summary>
public sealed record NoteResponse(Guid Id, string Title, string Content, bool IsArchived, DateTimeOffset CreatedAtUtc)
{
    /// <summary>EF projection for read queries — the projected shape lives here once and is translated to SQL.</summary>
    public static readonly Expression<Func<Note, NoteResponse>> Projection =
        note => new NoteResponse(note.Id, note.Title, note.Content, note.IsArchived, note.CreatedAtUtc);

    /// <summary>Maps an already-materialized <see cref="Note"/> (the expression above can't run on an in-memory object).</summary>
    public static NoteResponse From(Note note)
        => new(note.Id, note.Title, note.Content, note.IsArchived, note.CreatedAtUtc);
}
