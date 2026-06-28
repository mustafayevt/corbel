using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Corbel.Setup;

/// <summary>
/// Adds the operation-level documentation that the fluent <c>.WithSummary()</c>/<c>.WithDescription()</c> at each
/// endpoint can't express: per-parameter descriptions (route/query params) and representative request/response
/// examples, for a curated set of operations matched by <c>operationId</c> (the <c>.WithName(...)</c> route name).
/// Examples live only in the OpenAPI document — <c>openapi-typescript</c> doesn't emit them into the typed
/// client — so they enrich the docs without changing the generated <c>schema.d.ts</c> types.
/// </summary>
internal sealed class OperationDocsTransformer : IOpenApiOperationTransformer
{
    private const string NoteJson =
        """{ "id": "0194e6a0-1c2d-7b3e-9f10-2a3b4c5d6e7f", "title": "Shopping list", "content": "Milk, eggs, bread", "isArchived": false, "createdAtUtc": "2026-01-15T09:30:00+00:00" }""";

    private static readonly Dictionary<string, string> NoteIdParam = new(StringComparer.Ordinal)
    {
        ["id"] = "The note's unique identifier (GUID).",
    };

    private static readonly Dictionary<string, Dictionary<string, string>> ParameterDescriptions = new(StringComparer.Ordinal)
    {
        ["ListNotes"] = new(StringComparer.Ordinal)
        {
            ["page"] = "1-based page number. Defaults to 1; clamped to the range 1–100000.",
            ["pageSize"] = "Items per page. Defaults to 20; clamped to the range 1–100.",
            ["search"] = "Optional case-insensitive substring matched against each note's title and content. LIKE wildcards (% and _) are treated literally.",
        },
        ["GetNote"] = NoteIdParam,
        ["UpdateNote"] = NoteIdParam,
        ["ArchiveNote"] = NoteIdParam,
        ["DeleteNote"] = NoteIdParam,
    };

    public Task TransformAsync(
        OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        if (operation.OperationId is not { } operationId)
            return Task.CompletedTask;

        ApplyParameterDescriptions(operation, operationId);
        ApplyExamples(operation, operationId);
        return Task.CompletedTask;
    }

    private static void ApplyParameterDescriptions(OpenApiOperation operation, string operationId)
    {
        if (!ParameterDescriptions.TryGetValue(operationId, out var descriptions) || operation.Parameters is not { } parameters)
            return;

        foreach (var parameter in parameters)
            if (parameter is OpenApiParameter { Name: { } name } concrete && descriptions.TryGetValue(name, out var description))
                concrete.Description = description;
    }

    private static void ApplyExamples(OpenApiOperation operation, string operationId)
    {
        switch (operationId)
        {
            case "Login":
                SetRequestExample(operation, """{ "email": "ada@example.com", "password": "Pa55word!", "useCookies": true }""");
                SetResponseExample(operation, "200", """{ "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...", "expiresIn": 900, "refreshToken": null }""");
                SetResponseExample(operation, "401", """{ "title": "Unauthorized", "status": 401, "detail": "Invalid email or password.", "instance": "/api/auth/login", "errorCode": "auth.invalid_credentials", "traceId": "00-3a1f...-01" }""");
                break;

            case "Register":
                SetRequestExample(operation, """{ "email": "ada@example.com", "password": "Pa55word!", "displayName": "Ada Lovelace" }""");
                SetResponseExample(operation, "200", """{ "message": "Registration received. You can now sign in." }""");
                break;

            case "CreateNote":
                SetRequestExample(operation, """{ "title": "Shopping list", "content": "Milk, eggs, bread" }""");
                SetResponseExample(operation, "201", NoteJson);
                break;

            case "ListNotes":
                SetResponseExample(operation, "200",
                    $$"""{ "items": [ {{NoteJson}} ], "page": 1, "pageSize": 20, "totalCount": 1, "totalPages": 1, "hasNext": false, "hasPrevious": false }""");
                break;
        }
    }

    private static void SetRequestExample(OpenApiOperation operation, string json)
    {
        if (operation.RequestBody?.Content is { } content)
            ApplyExample(content, json);
    }

    private static void SetResponseExample(OpenApiOperation operation, string statusCode, string json)
    {
        if (operation.Responses is { } responses
            && responses.TryGetValue(statusCode, out var response)
            && response.Content is { } content)
            ApplyExample(content, json);
    }

    // Set the example on every media type of the body (typically just application/json or application/problem+json).
    // Re-parse per media type: a JsonNode can have only one parent, so the value can't be shared.
    private static void ApplyExample(IDictionary<string, OpenApiMediaType> content, string json)
    {
        foreach (var media in content.Values)
            media.Example = JsonNode.Parse(json);
    }
}
