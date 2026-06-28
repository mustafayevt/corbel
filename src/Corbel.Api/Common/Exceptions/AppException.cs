namespace Corbel.Common.Exceptions;

/// <summary>
/// Base for expected application failures that carry an HTTP meaning. Each concrete exception owns its message
/// and its machine-readable error code; the HTTP status comes from the abstract NotFound/Forbidden/Unauthorized
/// bases below. (Named AppException, not ApplicationException — the BCL type of that name should not be derived
/// from.)
/// </summary>
public abstract class AppException : Exception
{
    protected AppException(string errorCode, string message, Exception? innerException = null)
        : base(message, innerException) => ErrorCode = errorCode;

    public abstract int StatusCode { get; }

    public string ErrorCode { get; }
}

/// <summary>404 base — a concrete subtype names the specific resource (e.g. <c>NoteNotFoundException</c>).</summary>
public abstract class NotFoundException(string errorCode, string message) : AppException(errorCode, message)
{
    public override int StatusCode => StatusCodes.Status404NotFound;
}

/// <summary>403 base — a concrete subtype names the specific denial (e.g. <c>CsrfValidationException</c>).</summary>
public abstract class ForbiddenException(string errorCode, string message) : AppException(errorCode, message)
{
    public override int StatusCode => StatusCodes.Status403Forbidden;
}

/// <summary>401 base — a concrete subtype names the specific failure (e.g. <c>InvalidCredentialsException</c>).</summary>
public abstract class UnauthorizedException(string errorCode, string message) : AppException(errorCode, message)
{
    public override int StatusCode => StatusCodes.Status401Unauthorized;
}
