using Corbel.Common.Pagination;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Corbel.Setup;

/// <summary>
/// Shapes individual schemas so the generated TS client gets a precise contract: collapses .NET 10's lenient
/// numeric <c>["integer"/"number","string"]</c> unions to a plain number, marks <see cref="PagedResult{T}"/>'s
/// computed properties required, and documents the runtime-only errorCode/traceId extensions on ProblemDetails.
/// </summary>
internal sealed class ClientContractSchemaTransformer : IOpenApiSchemaTransformer
{
    // PagedResult's computed get-only properties — always present on a response, so marked required in the schema.
    private static readonly string[] PagedResultComputedProperties = ["totalPages", "hasNext", "hasPrevious"];

    public Task TransformAsync(
        OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        // .NET 10 emits numeric types as a lenient ["integer"/"number","string"] union (+ a pattern) so a value
        // may arrive as a string. Collapse it to the plain numeric type so the client gets `number`, not
        // `number | string`, for every int/decimal on the wire.
        if (schema.Type is { } type && type.HasFlag(JsonSchemaType.String)
            && (type.HasFlag(JsonSchemaType.Integer) || type.HasFlag(JsonSchemaType.Number)))
        {
            schema.Type = type & ~JsonSchemaType.String;
            schema.Pattern = null;
        }

        // PagedResult's TotalPages/HasNext/HasPrevious are computed get-only properties, so .NET omits them from
        // `required`; they are always present on a response, so mark them required for a precise client type.
        if (context.JsonTypeInfo?.Type is { IsGenericType: true } clr
            && clr.GetGenericTypeDefinition() == typeof(PagedResult<>) && schema.Properties is { } props)
        {
            foreach (var name in PagedResultComputedProperties)
                if (props.ContainsKey(name))
                    (schema.Required ??= new HashSet<string>(StringComparer.Ordinal)).Add(name);
        }

        // errorCode/traceId ride as runtime Extensions the CLR type doesn't declare, so .NET omits them from the
        // schema. Document them so the generated TS client gets the contract it actually receives.
        if (context.JsonTypeInfo?.Type is { } clrType
            && (clrType == typeof(ProblemDetails) || clrType == typeof(HttpValidationProblemDetails))
            && schema.Properties is { } problemProps)
        {
            problemProps.TryAdd("errorCode", new OpenApiSchema { Type = JsonSchemaType.String });
            problemProps.TryAdd("traceId", new OpenApiSchema { Type = JsonSchemaType.String });
        }

        return Task.CompletedTask;
    }
}
