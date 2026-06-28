using Corbel.Common.Exceptions;
using FluentValidation;
using FluentValidation.Results;
using Mediator;
using ValidationException = Corbel.Common.Exceptions.ValidationException;

namespace Corbel.Common.Behaviors;

/// <summary>
/// Mediator pipeline behavior that runs all FluentValidation validators for a message before the handler,
/// throwing <see cref="ValidationException"/> (→ 400) on failure. Registered in ServiceRegistration as the
/// outer behavior so invalid input is rejected before a transaction is opened.
/// </summary>
public sealed class ValidationBehavior<TMessage, TResponse>(IEnumerable<IValidator<TMessage>> validators)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : notnull, IMessage
{
    public async ValueTask<TResponse> Handle(
        TMessage message, MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken cancellationToken)
    {
        if (validators.Any())
        {
            var context = new ValidationContext<TMessage>(message);

            // Sequential: a shared ValidationContext isn't safe to run concurrently, and there is at most one
            // validator per message — fanning out with Task.WhenAll would only allocate for nothing.
            var failures = new List<ValidationFailure>();
            foreach (var validator in validators)
            {
                var result = await validator.ValidateAsync(context, cancellationToken);
                if (!result.IsValid)
                    failures.AddRange(result.Errors);
            }

            if (failures.Count != 0)
            {
                var errors = failures
                    .GroupBy(f => f.PropertyName, StringComparer.Ordinal)
                    .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).ToArray(), StringComparer.Ordinal);

                throw new ValidationException(errors);
            }
        }

        return await next(message, cancellationToken);
    }
}
