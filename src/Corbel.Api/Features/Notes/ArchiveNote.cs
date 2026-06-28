using Corbel.Common.Abstractions;
using Corbel.Common.Messaging;
using Corbel.Common.Web;
using Corbel.Infrastructure.Persistence;
using Mediator;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Corbel.Features.Notes;

public sealed record ArchiveNoteCommand(Guid Id) : IRequest<NoteResponse>, IWriteCommand;

public sealed class ArchiveNoteHandler(AppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<ArchiveNoteCommand, NoteResponse>
{
    public async ValueTask<NoteResponse> Handle(ArchiveNoteCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireId();

        var note = await db.Notes.FirstOrDefaultAsync(
                       n => n.Id == command.Id && n.OwnerId == userId, cancellationToken)
                   ?? throw new NoteNotFoundException();

        // Enforces the invariant in the domain: archiving an already-archived note throws → 422 via the global handler.
        note.Archive();
        await db.SaveChangesAsync(cancellationToken);

        return NoteResponse.From(note);
    }
}

public sealed class ArchiveNoteEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("notes/{id:guid}/archive", Handle)
            .WithName("ArchiveNote")
            .WithTags("Notes")
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict) // xmin optimistic-concurrency conflict
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

    private static async Task<Ok<NoteResponse>> Handle(Guid id, ISender sender, CancellationToken cancellationToken)
        => TypedResults.Ok(await sender.Send(new ArchiveNoteCommand(id), cancellationToken));
}
