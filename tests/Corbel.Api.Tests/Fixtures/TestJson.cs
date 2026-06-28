using System.Net.Http.Json;
using System.Text.Json;

namespace Corbel.Api.Tests.Fixtures;

/// <summary>Shared JSON settings + small response helpers that mirror the API's Web (camelCase) defaults.</summary>
public static class TestJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static async Task<T> ReadJsonAsync<T>(this HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(Options))!;

    /// <summary>Reads the RFC 9457 <c>errorCode</c> extension from a ProblemDetails body (it serializes at the root).</summary>
    public static async Task<string?> ReadErrorCodeAsync(this HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.TryGetProperty("errorCode", out var code) ? code.GetString() : null;
    }

    /// <summary>Reads the whole ProblemDetails body as a JSON element (cloned so it outlives the parsed document).</summary>
    public static async Task<JsonElement> ReadProblemAsync(this HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }
}

/// <summary>The documented auth token envelope: <c>{ accessToken, expiresIn, refreshToken? }</c>.</summary>
public sealed record TokenResponse(string AccessToken, int ExpiresIn, string? RefreshToken);
