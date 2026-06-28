namespace Corbel.Features.Auth;

/// <summary>The result of a successful sign-in or refresh: a short-lived access token, plus the refresh token in bearer mode only.</summary>
/// <param name="AccessToken">The JWT access token to send as <c>Authorization: Bearer &lt;token&gt;</c> on subsequent requests.</param>
/// <param name="ExpiresIn">Lifetime of the access token, in seconds from now.</param>
/// <param name="RefreshToken">The raw refresh token in bearer mode; <c>null</c> in cookie mode, where it is set as an httpOnly cookie instead.</param>
public sealed record TokenResponse(string AccessToken, int ExpiresIn, string? RefreshToken);

/// <summary>The authenticated user's public profile, shaped for the SPA.</summary>
/// <param name="Id">The user's unique identifier.</param>
/// <param name="Email">The user's email address (also the username).</param>
/// <param name="DisplayName">An optional display name; <c>null</c> if the user didn't set one.</param>
/// <param name="Roles">The roles granted to the user (e.g. <c>User</c>, <c>Admin</c>).</param>
public sealed record UserResponse(Guid Id, string Email, string? DisplayName, string[] Roles);
