using Mediator;

namespace Corbel.Domain.Events;

/// <summary>
/// A domain event raised by an aggregate. Implements the mediator's <see cref="INotification"/> so it can be
/// published after the command commits by <c>TransactionBehavior</c>; handlers live in the application layer.
/// </summary>
public interface IDomainEvent : INotification;
