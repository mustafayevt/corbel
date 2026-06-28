using System.Security.Claims;
using Corbel.Common.Abstractions;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Corbel.Common.Web;

/// <summary>
/// Resolves the authenticated user's id from the current HTTP context's <c>sub</c> claim (emitted by the JWT
/// token service, which pins <c>MapInboundClaims = false</c> so the claim name round-trips unchanged).
/// </summary>
public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid? Id =>
        Guid.TryParse(accessor.HttpContext?.User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id)
            ? id
            : null;
}
