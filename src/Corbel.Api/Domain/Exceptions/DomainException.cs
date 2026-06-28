namespace Corbel.Domain.Exceptions;

/// <summary>
/// Thrown by domain entities when a business invariant is violated. Pure domain — no HTTP knowledge — but
/// each leaf carries a machine-readable <see cref="ErrorCode"/> (from the shared ErrorCodes registry) so the
/// global handler can surface a specific code. The handler maps every <see cref="DomainException"/> to 422.
/// </summary>
public abstract class DomainException(string errorCode, string message) : Exception(message)
{
    /// <summary>The stable error code for this invariant; supplied by the leaf type.</summary>
    public string ErrorCode { get; } = errorCode;
}
