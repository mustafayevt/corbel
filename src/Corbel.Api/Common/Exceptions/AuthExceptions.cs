using Corbel.Common.Errors;

namespace Corbel.Common.Exceptions;

// Self-describing auth/identity failures: each owns its error code + a curated, client-safe message, so a throw
// site is just `throw new XxxException()` with no loose code/message strings to keep in sync. Thrown across the
// auth handlers (Features/Auth) and the token service (Infrastructure/Auth), so they live in shared Common.

/// <summary>Reached a handler without an authenticated user (deny-by-default should normally prevent this).</summary>
public sealed class NotAuthenticatedException()
    : UnauthorizedException(ErrorCodes.Unauthorized, "Not authenticated.");

/// <summary>Wrong email/password — deliberately generic so it can't confirm whether the account exists.</summary>
public sealed class InvalidCredentialsException()
    : UnauthorizedException(ErrorCodes.InvalidCredentials, "Invalid email or password.");

/// <summary>The account is within the Identity lockout window.</summary>
public sealed class AccountLockedException()
    : UnauthorizedException(ErrorCodes.AccountLocked, "The account is temporarily locked. Try again later.");

/// <summary>The presented refresh token is missing, unknown, revoked, consumed, or expired (one uniform reply).</summary>
public sealed class InvalidRefreshTokenException()
    : UnauthorizedException(ErrorCodes.InvalidToken, "The refresh token is invalid or has expired.");

/// <summary>A consumed refresh token was replayed — the whole token family has been revoked (reuse detection).</summary>
public sealed class TokenReuseException()
    : UnauthorizedException(ErrorCodes.TokenReuseDetected, "Refresh token reuse detected; the session has been revoked.");

/// <summary>The signed double-submit CSRF check failed on a cookie-mode request.</summary>
public sealed class CsrfValidationException()
    : ForbiddenException(ErrorCodes.Forbidden, "CSRF validation failed.");
