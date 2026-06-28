using Mediator;

namespace Corbel.Common.Messaging;

/// <summary>
/// Marker for write messages. <c>TransactionBehavior</c> wraps every <see cref="IWriteCommand"/> in a database
/// transaction (so a multi-write handler commits all-or-nothing); plain <c>IRequest</c> queries pass through
/// untouched. It extends <see cref="IMessage"/> so it satisfies the pipeline-behavior generic constraint. (We
/// deliberately don't reuse martinothamar's built-in ICommand/IQuery — those are a separate handler hierarchy.)
/// </summary>
public interface IWriteCommand : IMessage;
