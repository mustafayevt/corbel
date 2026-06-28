using Corbel.Common.Abstractions;
using Corbel.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Corbel.Infrastructure.Persistence.Interceptors;

/// <summary>
/// SaveChanges interceptor that, before saving, turns hard deletes of <see cref="ISoftDelete"/> entities into
/// soft-delete updates and stamps <see cref="IAuditable"/> fields (soft-delete first, so the flipped entries
/// are also audited as modified). Registered scoped (needs the scoped <see cref="ICurrentUser"/>). Domain-event
/// dispatch is a separate concern, handled post-commit by <c>TransactionBehavior</c>.
/// </summary>
public sealed class AuditingInterceptor(ICurrentUser currentUser, TimeProvider timeProvider) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Apply(DbContext? context)
    {
        if (context is null)
            return;

        var now = timeProvider.GetUtcNow();
        var userId = currentUser.Id;

        // 1) Soft-delete first: convert Deleted → Modified so the row is updated, not removed. The flip also
        //    makes these entries visible to the audit pass below, which stamps them as modified.
        foreach (var entry in context.ChangeTracker.Entries<ISoftDelete>())
        {
            if (entry.State is not EntityState.Deleted)
                continue;

            entry.State = EntityState.Modified;
            entry.Entity.IsDeleted = true;
            entry.Entity.DeletedAtUtc = now;
        }

        // 2) Audit: stamp created on insert, modified on update (including the soft-deletes flipped above).
        foreach (var entry in context.ChangeTracker.Entries<IAuditable>())
        {
            if (entry.State is EntityState.Added)
            {
                entry.Entity.CreatedAtUtc = now;
                entry.Entity.CreatedBy = userId;
            }

            // Modified* only on a real update, so the nullable fields keep their "never modified since creation"
            // meaning on a freshly inserted row. (Soft-delete flips Deleted → Modified above, so deletes still audit.)
            if (entry.State is EntityState.Modified)
            {
                entry.Entity.ModifiedAtUtc = now;
                entry.Entity.ModifiedBy = userId;
            }
        }
    }
}
