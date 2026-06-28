using Corbel.Common.Abstractions;
using Corbel.Common.Web;
using Corbel.Infrastructure.Persistence;
using Mediator;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Corbel.Features.Notes;

public sealed record GetNoteQuery(Guid Id) : IRequest<NoteResponse>;

public sealed class GetNoteHandler(AppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetNoteQuery, NoteResponse>
{
    public async ValueTask<NoteResponse> Handle(GetNoteQuery query, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireId();

        // Project in SQL (no tracking) and scope to the owner in the same predicate.
        var note = await db.Notes
            .AsNoTracking()
            .Where(n => n.Id == query.Id && n.OwnerId == userId)
            .Select(NoteResponse.Projection)
            .FirstOrDefaultAsync(cancellationToken);

        return note ?? throw new NoteNotFoundException();
    }
}

public sealed class GetNoteEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("notes/{id:guid}", Handle)
            .WithName("GetNote")
            .WithTags("Notes")
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static async Task<Ok<NoteResponse>> Handle(Guid id, ISender sender, CancellationToken cancellationToken)
        => TypedResults.Ok(await sender.Send(new GetNoteQuery(id), cancellationToken));
}
