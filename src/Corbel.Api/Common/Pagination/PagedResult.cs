using Microsoft.AspNetCore.Mvc;

namespace Corbel.Common.Pagination;

/// <summary>Consistent list envelope returned by every paginated endpoint → generates a clean PagedResult&lt;T&gt; in the TS client.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasNext => Page < TotalPages;
    public bool HasPrevious => Page > 1;
}

/// <summary>Bound from the query string via [AsParameters]; the Normalized* members own both defaulting and clamping.</summary>
public sealed record PageRequest
{
    /// <summary>Upper bound on the page number — stops a pathological page value from overflowing the SQL OFFSET and caps pointless deep paging.</summary>
    public const int MaxPage = 100_000;

    [FromQuery(Name = "page")] public int? Page { get; init; }
    [FromQuery(Name = "pageSize")] public int? PageSize { get; init; }

    // Clamp both ends: a missing/≤0 page defaults to 1; the upper bound keeps (page - 1) * pageSize from
    // overflowing Int32 (which would surface as a 500 from a negative OFFSET) on a crafted page value.
    public int NormalizedPage => Math.Clamp(Page ?? 1, 1, MaxPage);
    public int NormalizedSize => Math.Clamp(PageSize ?? 20, 1, 100);
}
