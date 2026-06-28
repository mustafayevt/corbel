using Corbel.Domain.Common;
using Corbel.Domain.Events;
using Corbel.Domain.Exceptions;

namespace Corbel.Domain.Entities;

/// <summary>
/// A rich-domain aggregate: state changes only through behavior methods that enforce invariants, never public
/// setters. Auditable + soft-deletable, and owned by a user (per-object authorization). Shape rules (e.g. a
/// non-blank title) are validated as 400 by FluentValidation before a handler runs; the domain keeps only a
/// defensive guard. State-dependent invariants (archiving twice) surface as 422 via the global handler.
/// </summary>
public sealed class Note : Entity, IAuditable, ISoftDelete
{
    private Note() { } // EF

    private Note(Guid id, string title, string content, Guid ownerId) : base(id)
    {
        Title = title;
        Content = content;
        OwnerId = ownerId;
    }

    public string Title { get; private set; } = default!;
    public string Content { get; private set; } = default!;
    public Guid OwnerId { get; private set; }
    public bool IsArchived { get; private set; }

    // IAuditable — populated by the SaveChanges interceptor.
    public DateTimeOffset CreatedAtUtc { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? ModifiedAtUtc { get; set; }
    public Guid? ModifiedBy { get; set; }

    // ISoftDelete — the interceptor flips Delete → update.
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAtUtc { get; set; }

    public static Note Create(string title, string? content, Guid ownerId)
        => new(Guid.CreateVersion7(), Normalize(title), content ?? string.Empty, ownerId);

    public void Edit(string title, string? content)
    {
        Title = Normalize(title);
        Content = content ?? string.Empty;
    }

    public void Archive()
    {
        if (IsArchived)
            throw new NoteAlreadyArchivedException();

        IsArchived = true;
        Raise(new NoteArchived(Id, OwnerId));
    }

    // Last-line defensive guard only — a blank title is rejected as 400 by the validator first, so this is an
    // ArgumentException (programmer/contract error), not a domain (422) invariant.
    private static string Normalize(string title)
        => string.IsNullOrWhiteSpace(title)
            ? throw new ArgumentException("A note title is required.", nameof(title))
            : title.Trim();
}
