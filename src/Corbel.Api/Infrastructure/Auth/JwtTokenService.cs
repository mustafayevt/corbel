using System.Security.Claims;
using System.Text;
using Corbel.Common.Options;
using Corbel.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Corbel.Infrastructure.Auth;

/// <summary>
/// Issues short-lived HS256 access tokens from <see cref="JwtOptions"/>. Emits compact claim names
/// (<c>sub</c>, <c>email</c>, <c>name</c>, <see cref="RoleClaimType"/>) — the bearer handler is configured
/// with <c>MapInboundClaims = false</c> and a matching <c>RoleClaimType</c>, so the names round-trip unchanged.
/// Registered as a singleton: the signing key is fixed for the app's lifetime, so the credentials and the
/// thread-safe handler are built once.
/// </summary>
public sealed class JwtTokenService(IOptions<JwtOptions> options, TimeProvider timeProvider)
{
    /// <summary>The claim type roles are written to and validated against (kept short to keep tokens compact).</summary>
    public const string RoleClaimType = "role";

    private static readonly JsonWebTokenHandler Handler = new();

    private readonly JwtOptions _options = options.Value;
    // Reads the constructor parameter (not the _options field): a field initializer cannot reference another
    // instance field. The signing key is fixed for the app's lifetime, so the credentials are built once here.
    private readonly SigningCredentials _signingCredentials = new(
        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Value.SigningKey)), SecurityAlgorithms.HmacSha256);

    public (string Token, int ExpiresInSeconds) CreateAccessToken(AppUser user, IEnumerable<string> roles)
    {
        var now = timeProvider.GetUtcNow();
        var expires = now.AddMinutes(_options.AccessTokenMinutes);

        List<Claim> claims =
        [
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),
        ];

        if (!string.IsNullOrEmpty(user.Email))
            claims.Add(new Claim(JwtRegisteredClaimNames.Email, user.Email));

        if (!string.IsNullOrEmpty(user.DisplayName))
            claims.Add(new Claim(JwtRegisteredClaimNames.Name, user.DisplayName));

        foreach (var role in roles)
            claims.Add(new Claim(RoleClaimType, role));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            Subject = new ClaimsIdentity(claims),
            SigningCredentials = _signingCredentials,
        };

        return (Handler.CreateToken(descriptor), (int)(expires - now).TotalSeconds);
    }
}
