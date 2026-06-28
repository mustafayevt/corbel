using System.Reflection;
using System.Text.Json.Nodes;
using Corbel.Common.Errors;
using Corbel.Common.Pagination;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Corbel.Setup;

/// <summary>
/// Shapes individual schemas so the generated TS client gets a precise contract: collapses .NET 10's lenient
/// numeric <c>["integer"/"number","string"]</c> unions to a plain number, marks <see cref="PagedResult{T}"/>'s
/// computed properties required, and documents the runtime-only errorCode/traceId extensions on ProblemDetails —
/// publishing the closed set of <see cref="ErrorCodes"/> as the <c>errorCode</c> enum so the generated client
/// (and any SDK) gets a discoverable, exhaustive union instead of an opaque string.
/// </summary>
internal sealed class ClientContractSchemaTransformer : IOpenApiSchemaTransformer
{
    // PagedResult's computed get-only properties — always present on a response, so marked required in the schema.
    private static readonly string[] PagedResultComputedProperties = ["totalPages", "hasNext", "hasPrevious"];

    // The closed vocabulary of machine-readable error codes, reflected from the single source of truth and sorted
    // so the generated document is byte-stable across regenerations (the CI contract-drift gate diffs it).
    private static readonly List<JsonNode> ErrorCodeEnum = typeof(ErrorCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field is { IsLiteral: true, IsInitOnly: false } && field.FieldType == typeof(string))
        .Select(field => (string)field.GetRawConstantValue()!)
        .OrderBy(code => code, StringComparer.Ordinal)
        .Select(code => (JsonNode)JsonValue.Create(code)!)
        .ToList();

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
            schema.Description ??= clrType == typeof(HttpValidationProblemDetails)
                ? "RFC 9457 problem document for a validation failure (400): an `errors` map of field name → messages, alongside the machine-readable `errorCode` and a correlation `traceId`."
                : "RFC 9457 problem document returned for every error response: carries a machine-readable `errorCode` and a correlation `traceId` (matching the server logs/traces).";

            problemProps["errorCode"] = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Description = "Stable, machine-readable code identifying the specific failure, decoupled from the HTTP status. One of the values in this enum.",
                Enum = ErrorCodeEnum,
            };
            problemProps.TryAdd("traceId", new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Description = "Correlation id for this request; matches the entry in the server logs/traces.",
            });
        }

        return Task.CompletedTask;
    }
}
