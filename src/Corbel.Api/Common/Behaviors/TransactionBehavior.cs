using Corbel.Common.Messaging;
using Corbel.Domain.Common;
using Corbel.Infrastructure.Persistence;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Corbel.Common.Behaviors;

/// <summary>
/// Wraps every command (<see cref="IWriteCommand"/>) in a single database transaction so a multi-write handler
/// commits all-or-nothing — e.g. Register (create user + assign role). Uses the EF execution strategy because
/// <c>EnableRetryOnFailure</c> is on (a hand-rolled <c>BeginTransaction</c> would otherwise throw), and reuses an
/// ambient transaction if one command dispatches another. Queries are plain <c>IRequest</c> and pass through.
/// </summary>
/// <remarks>
/// Domain events raised during the command are published AFTER the transaction commits and OUTSIDE the
/// execution-strategy scope: each attempt starts by clearing the ChangeTracker, so a transient retry rebuilds
/// its state — and re-raises its events — from scratch, and only the committed attempt's events remain on the
/// tracked entities to dispatch. That gives post-commit, at-most-once delivery, and means a fault in an event
/// handler surfaces to the caller instead of re-running the already-committed write. Handlers must therefore be
/// idempotent (a retry re-runs the whole delegate); strict once-only writes need an idempotency key or upsert.
/// Events are harvested off the entities still tracked after the commit, so an event-raising entity must survive
/// its SaveChanges to dispatch — the template only soft-deletes (a deleted row stays tracked as Modified); a hard
/// delete of an entity that raised an event in the same command would drop it.
/// </remarks>
public sealed class TransactionBehavior<TMessage, TResponse>(AppDbContext db, IPublisher publisher)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : notnull, IWriteCommand
{
    public async ValueTask<TResponse> Handle(
        TMessage message, MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken cancellationToken)
    {
        // Nested command: reuse the open transaction and let the outermost behavior drain the events post-commit.
        if (db.Database.CurrentTransaction is not null)
            return await next(message, cancellationToken);

        var strategy = db.Database.CreateExecutionStrategy();
        var response = await strategy.ExecuteAsync(async () =>
        {
            // A transient fault re-runs this delegate against the same DbContext, so drop everything the failed
            // attempt left tracked (and the events it raised) before re-running the handler.
            db.ChangeTracker.Clear();

            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var result = await next(message, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        });

        await DispatchDomainEventsAsync(cancellationToken);
        return response;
    }

    // Harvest the committed attempt's domain events off the tracked entities and publish them once. Runs after
    // the execution strategy returns, so a retried attempt's events (cleared above) can never be re-dispatched.
    private async Task DispatchDomainEventsAsync(CancellationToken cancellationToken)
    {
        var entities = db.ChangeTracker.Entries<Entity>()
            .Where(entry => entry.Entity.DomainEvents.Count > 0)
            .Select(entry => entry.Entity)
            .ToList();

        var events = entities.SelectMany(entity => entity.DomainEvents).ToList();
        foreach (var entity in entities)
            entity.ClearDomainEvents();

        foreach (var domainEvent in events)
            await publisher.Publish(domainEvent, cancellationToken);
    }
}
