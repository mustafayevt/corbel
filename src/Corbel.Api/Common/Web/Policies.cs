namespace Corbel.Common.Web;

/// <summary>Named rate-limit policies — referenced by AddRateLimiter and the endpoints that opt in.</summary>
public static class RateLimitPolicies
{
    public const string Auth = "auth";
}

/// <summary>Named CORS policies (only relevant for genuinely split-origin / mobile clients).</summary>
public static class CorsPolicies
{
    public const string Spa = "spa";
}

/// <summary>Named authorization policies — referenced by AddAuthorizationBuilder and the endpoints that opt in.</summary>
public static class AuthorizationPolicies
{
    public const string Admin = "Admin";
}
