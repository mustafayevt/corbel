namespace Corbel.Common.Errors;

/// <summary>
/// Single source of truth for machine-readable error codes returned in ProblemDetails (<c>errorCode</c>).
/// Stable strings the frontend switches on — decoupled from HTTP status and exception class names. Add a
/// constant here when you add a failure the client must distinguish, and mirror it in the TS client.
/// </summary>
public static class ErrorCodes
{
    // Generic
    public const string Validation = "common.validation";
    public const string Forbidden = "common.forbidden";
    public const string Unauthorized = "common.unauthorized";
    public const string NotFound = "common.not_found";
    public const string RateLimited = "common.rate_limited";
    public const string Concurrency = "common.concurrency_conflict";
    public const string Unexpected = "common.unexpected";

    // Auth
    public const string InvalidCredentials = "auth.invalid_credentials";
    public const string AccountLocked = "auth.account_locked";
    public const string InvalidToken = "auth.invalid_token";
    public const string TokenReuseDetected = "auth.token_reuse_detected";

    // Notes
    public const string NoteNotFound = "note.not_found";
    public const string NoteAlreadyArchived = "note.already_archived";
}
