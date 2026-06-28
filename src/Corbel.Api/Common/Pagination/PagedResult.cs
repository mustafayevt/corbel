using Microsoft.AspNetCore.Mvc;

namespace Corbel.Common.Pagination;

/// <summary>The standard envelope for a paginated list response: the page of items plus the paging metadata a client needs to render controls.</summary>
/// <param name="Items">The items on the current page.</param>
/// <param name="Page">The 1-based page number this result represents.</param>
/// <param name="PageSize">The maximum number of items per page.</param>
/// <param name="TotalCount">The total number of items across all pages (after any filter).</param>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    /// <summary>The total number of pages across the result set.</summary>
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    /// <summary><c>true</c> when a page after this one exists.</summary>
    public bool HasNext => Page < TotalPages;

    /// <summary><c>true</c> when a page before this one exists.</summary>
    public bool HasPrevious => Page > 1;
}

/// <summary>Bound from the query string via [AsParameters]; the Normalized* members own both defaulting and clamping.</summary>
public sealed record PageRequest
{
    /// <summary>Upper bound on the page number — stops a pathological page value from overflowing the SQL OFFSET and caps pointless deep paging.</summary>
    public const int MaxPage = 100_000;

    // The OpenAPI descriptions for these query params (defaults + clamping) live in OperationDocsTransformer so
    // there's a single source of truth; here the Normalized* members below own the actual defaulting/clamping.
    [FromQuery(Name = "page")] public int? Page { get; init; }
    [FromQuery(Name = "pageSize")] public int? PageSize { get; init; }

    // Clamp both ends: a missing/≤0 page defaults to 1; the upper bound keeps (page - 1) * pageSize from
    // overflowing Int32 (which would surface as a 500 from a negative OFFSET) on a crafted page value.
    public int NormalizedPage => Math.Clamp(Page ?? 1, 1, MaxPage);
    public int NormalizedSize => Math.Clamp(PageSize ?? 20, 1, 100);
}
