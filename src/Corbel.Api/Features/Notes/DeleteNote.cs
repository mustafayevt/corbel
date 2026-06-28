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
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict); // xmin optimistic-concurrency conflict

    private static async Task<NoContent> Handle(Guid id, ISender sender, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteNoteCommand(id), cancellationToken);
        return TypedResults.NoContent();
    }
}
