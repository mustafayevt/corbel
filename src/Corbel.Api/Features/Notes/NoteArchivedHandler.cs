using Corbel.Domain.Events;
using Mediator;

namespace Corbel.Features.Notes;

/// <summary>
/// Demonstrates the domain-events pattern: logs when a note is archived. Add real post-commit side-effects
/// (send an email, update a projection, enqueue a job) here. Auto-discovered by the mediator source generator.
/// </summary>
public sealed class NoteArchivedHandler(ILogger<NoteArchivedHandler> logger) : INotificationHandler<NoteArchived>
{
    public ValueTask Handle(NoteArchived notification, CancellationToken cancellationToken)
    {
        logger.LogInformation("Note {NoteId} archived by user {OwnerId}", notification.NoteId, notification.OwnerId);
        return ValueTask.CompletedTask;
    }
}
