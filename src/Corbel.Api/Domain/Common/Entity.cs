using System.ComponentModel.DataAnnotations.Schema;
using Corbel.Domain.Events;

namespace Corbel.Domain.Common;

/// <summary>
/// Base class for entities: a Guid v7 key and a buffer of domain events raised by behavior methods. Events are
/// published after the command commits by <c>TransactionBehavior</c> (see <c>Note.Archive()</c>).
/// </summary>
public abstract class Entity
{
    private readonly List<IDomainEvent> _domainEvents = [];

    protected Entity(Guid id) => Id = id;

    /// <summary>Parameterless ctor required for EF Core materialization.</summary>
    protected Entity() { }

    public Guid Id { get; protected init; }

    [NotMapped]
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents;

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
