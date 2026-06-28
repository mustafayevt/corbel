using Corbel.Common.Abstractions;
using Corbel.Common.Messaging;
using Corbel.Common.Web;
using Corbel.Infrastructure.Persistence;
using Mediator;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Corbel.Features.Notes;

public sealed record DeleteNoteCommand(Guid Id) : IRequest, IWriteCommand;

public sealed class DeleteNoteHandler(AppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<DeleteNoteCommand>
{
    public async ValueTask<Unit> Handle(DeleteNoteCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireId();

        var note = await db.Notes.FirstOrDefaultAsync(
                       n => n.Id == command.Id && n.OwnerId == userId, cancellationToken)
                   ?? throw new NoteNotFoundException();

        // Remove is translated to a soft-delete (IsDeleted flag) by the SaveChanges interceptor.
        db.Notes.Remove(note);
        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

public sealed class DeleteNoteEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapDelete("notes/{id:guid}", Handle)
            .WithName("DeleteNote")
            .WithTags("Notes")
            .RequireAuthorization()
            .WithSummary("Delete a note.")
            .WithDescription(
                "Soft-deletes the caller's note (flagged deleted, not physically removed). Returns 204 on success; a missing or not-owned note returns 404.\n\n"
                + "**Errors:** 401 `common.unauthorized`, 404 `note.not_found`, 409 `common.concurrency_conflict`, 429 `common.rate_limited`.")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict); // xmin optimistic-concurrency conflict

    private static async Task<NoContent> Handle(Guid id, ISender sender, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteNoteCommand(id), cancellationToken);
        return TypedResults.NoContent();
    }
}
