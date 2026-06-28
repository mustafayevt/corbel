using Corbel.Common.Abstractions;
using Corbel.Common.Pagination;
using Corbel.Common.Web;
using Corbel.Infrastructure.Persistence;
using Mediator;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Corbel.Features.Notes;

public sealed record ListNotesQuery(int Page, int PageSize, string? Search) : IRequest<PagedResult<NoteResponse>>;

public sealed class ListNotesHandler(AppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<ListNotesQuery, PagedResult<NoteResponse>>
{
    public async ValueTask<PagedResult<NoteResponse>> Handle(ListNotesQuery query, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireId();

        var notes = db.Notes.AsNoTracking().Where(n => n.OwnerId == userId);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // Escape LIKE metacharacters so a user typing "50%" or "a_b" gets a literal substring match rather than
            // wildcard behavior (backslash first, so we don't double-escape the ones we add). Already parameterized
            // by EF and scoped to OwnerId, so this is purely about correct search semantics, not injection.
            var term = query.Search.Trim()
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("%", "\\%", StringComparison.Ordinal)
                .Replace("_", "\\_", StringComparison.Ordinal);
            var pattern = $"%{term}%";
            // Match Title OR Content so the generically-named `search` param behaves the way a caller expects
            // (case-insensitive on Postgres via ILike).
            notes = notes.Where(n =>
                EF.Functions.ILike(n.Title, pattern, "\\") || EF.Functions.ILike(n.Content, pattern, "\\"));
        }

        // Count the filtered set before paging so TotalCount reflects what the caller searched for.
        var totalCount = await notes.CountAsync(cancellationToken);

        var items = await notes
            .OrderByDescending(n => n.CreatedAtUtc)
            .ThenByDescending(n => n.Id) // stable tiebreaker (Guid v7 is time-ordered) → deterministic paging
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(NoteResponse.Projection)
            .ToListAsync(cancellationToken);

        return new PagedResult<NoteResponse>(items, query.Page, query.PageSize, totalCount);
    }
}

public sealed class ListNotesEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("notes", Handle)
            .WithName("ListNotes")
            .WithTags("Notes")
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

    private static async Task<Ok<PagedResult<NoteResponse>>> Handle(
        [AsParameters] PageRequest page, string? search, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ListNotesQuery(page.NormalizedPage, page.NormalizedSize, search), cancellationToken);
        return TypedResults.Ok(result);
    }
}
