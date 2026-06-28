namespace Corbel.Domain.Events;

/// <summary>Raised when a note is archived. Handled in the application layer as the canonical domain-event example.</summary>
public sealed record NoteArchived(Guid NoteId, Guid OwnerId) : IDomainEvent;
