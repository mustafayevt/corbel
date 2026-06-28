using Corbel.Common.Errors;
using Corbel.Common.Web;
using Microsoft.AspNetCore.Http.Features;

namespace Corbel.Setup;

/// <summary>
/// The error contract: every failure becomes an RFC 9457 ProblemDetails carrying a correlation <c>traceId</c> and a
/// machine-readable <c>errorCode</c>. <see cref="GlobalExceptionHandler"/> maps thrown exceptions; this also fills
/// in a code for responses the framework middleware produces (401/403/404/429) so the client contract is uniform.
/// </summary>
internal static class ErrorHandlingSetup
{
    public static IServiceCollection AddApiProblemDetails(this IServiceCollection services)
    {
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            context.ProblemDetails.Extensions.TryAdd(
                "traceId",
                context.HttpContext.Features.Get<IHttpActivityFeature>()?.Activity?.Id
                    ?? context.HttpContext.TraceIdentifier);

            // Give framework-produced responses (auth challenge 401, authz 403, routing 404, rate limiter 429) the
            // same errorCode a handler-thrown AppException carries. TryAdd never overwrites the specific code
            // GlobalExceptionHandler already set, so this only fills the gaps.
            if (DefaultErrorCodeFor(context.ProblemDetails.Status) is { } code)
                context.ProblemDetails.Extensions.TryAdd("errorCode", code);
        });

        services.AddExceptionHandler<GlobalExceptionHandler>();

        return services;
    }

    // Stable error code for a status produced by the framework middleware (not GlobalExceptionHandler), so every
    // error body the client sees — handler-thrown or middleware-generated — carries a machine-readable code.
    private static string? DefaultErrorCodeFor(int? status) => status switch
    {
        StatusCodes.Status401Unauthorized => ErrorCodes.Unauthorized,
        StatusCodes.Status403Forbidden => ErrorCodes.Forbidden,
        StatusCodes.Status404NotFound => ErrorCodes.NotFound,
        StatusCodes.Status429TooManyRequests => ErrorCodes.RateLimited,
        _ => null,
    };
}
