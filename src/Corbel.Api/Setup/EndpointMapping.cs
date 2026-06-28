using Corbel.Common.Web;

namespace Corbel.Setup;

/// <summary>
/// Discovers every <see cref="IEndpoint"/> in the assembly and maps it under the <c>/api</c> route group — so
/// adding a slice is "drop one file", with no central registration to edit and a single place to add shared
/// group metadata or a future version segment.
/// </summary>
public static class EndpointMapping
{
    public static void MapEndpoints(this WebApplication app)
    {
        // Every slice lives under /api and is covered by the global rate limiter, so document 429 once here
        // rather than on each endpoint.
        var api = app.MapGroup("/api")
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        var endpoints = typeof(Program).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && type.IsAssignableTo(typeof(IEndpoint)))
            .Select(type => (IEndpoint)Activator.CreateInstance(type)!);

        foreach (var endpoint in endpoints)
            endpoint.MapEndpoint(api);
    }
}
