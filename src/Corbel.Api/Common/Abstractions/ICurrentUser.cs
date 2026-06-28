using Corbel.Common.Exceptions;

namespace Corbel.Common.Abstractions;

/// <summary>Scoped accessor for the authenticated user's id, resolved from the HTTP context. Injected into handlers and the audit interceptor.</summary>
public interface ICurrentUser
{
    public Guid? Id { get; }
}

/// <summary>Helpers over <see cref="ICurrentUser"/>.</summary>
public static class CurrentUserExtensions
{
    /// <summary>The authenticated user's id, or a clean 401 if a handler is somehow reached without one.</summary>
    public static Guid RequireId(this ICurrentUser user)
        => user.Id ?? throw new NotAuthenticatedException();
}
