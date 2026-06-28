using System.Text;
using Corbel.Common;
using Corbel.Common.Abstractions;
using Corbel.Common.Options;
using Corbel.Common.Web;
using Corbel.Domain.Entities;
using Corbel.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Corbel.Infrastructure.Auth;

/// <summary>
/// Registers the whole authentication/authorization stack: validated options, Identity Core with an Argon2id
/// hasher, JWT bearer (configured from the validated <see cref="JwtOptions"/>), a deny-by-default fallback
/// policy plus an "Admin" policy, and the token/CSRF services. Self-contained — call <c>services.AddAuth()</c>;
/// the host still owns UseAuthentication/UseAuthorization and the "auth" rate-limit policy.
/// </summary>
public static class AuthServiceCollectionExtensions
{
    public static IServiceCollection AddAuth(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);

        services.AddOptions<JwtOptions>()
            .BindConfiguration(JwtOptions.SectionName)
            .ValidateDataAnnotations()
            .Validate(o => o.RefreshAbsoluteDays >= o.RefreshTokenDays,
                "Jwt:RefreshAbsoluteDays must be greater than or equal to Jwt:RefreshTokenDays.")
            .ValidateOnStart();

        services.AddOptions<CookieAuthOptions>()
            .BindConfiguration(CookieAuthOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddIdentityCore<AppUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedEmail = false;
                // Identity enforces length only; character-class complexity is owned by PasswordRules (the
                // FluentValidation gate that runs before any handler). Keeping Identity from independently
                // rejecting a password the validator accepted is what stops Register from leaking account
                // existence: a complexity failure is a uniform 400 at validation, never a taken=200/free=400 split.
                options.Password.RequiredLength = PasswordPolicy.MinLength;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                // Fixed lockout throttles online password guessing / credential stuffing. Tradeoff: anyone who
                // knows a victim's email can deliberately lock the account by submitting bad passwords (a targeted
                // DoS). That is an accepted cost for this default; production behind a real proxy should pair it
                // with edge IP/CAPTCHA throttling. Set the window explicitly rather than leaning on the framework
                // default so the policy is visible at a glance.
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        // Argon2id in place of Identity's default PBKDF2 hasher.
        services.Replace(ServiceDescriptor.Singleton<IPasswordHasher<AppUser>, Argon2PasswordHasher>());

        // Configure JWT bearer from the validated options (no eager re-bind, no placeholder key, HS256 pinned).
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;
                bearer.MapInboundClaims = false; // keep the compact claim names (sub/email/name/role)
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = JwtRegisteredClaimNames.Name,
                    RoleClaimType = JwtTokenService.RoleClaimType,
                };
            });
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(AuthorizationPolicies.Admin, policy => policy.RequireRole(AppRoles.Admin));

        services.AddHttpContextAccessor();
        services.TryAddScoped<ICurrentUser, CurrentUser>();
        services.AddSingleton<JwtTokenService>();
        services.AddScoped<RefreshTokenService>();
        services.AddSingleton<CsrfProtection>();
        services.AddSingleton<AuthCookies>();

        // Periodic purge of absolutely-expired refresh tokens so the table doesn't grow unbounded.
        services.AddHostedService<RefreshTokenCleanupService>();

        return services;
    }
}
