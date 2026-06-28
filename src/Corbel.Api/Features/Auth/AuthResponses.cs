namespace Corbel.Features.Auth;

/// <summary>Access token plus, in bearer mode, the raw refresh token. In cookie mode <see cref="RefreshToken"/> is null (it rides an httpOnly cookie).</summary>
public sealed record TokenResponse(string AccessToken, int ExpiresIn, string? RefreshToken);

/// <summary>The authenticated user's public profile, shaped for the SPA.</summary>
public sealed record UserResponse(Guid Id, string Email, string? DisplayName, string[] Roles);

/// <summary>A generic, non-revealing acknowledgement returned by flows that must not leak account existence.</summary>
public sealed record MessageResponse(string Message);
