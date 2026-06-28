using Corbel.Common;
using Corbel.Common.Abstractions;
using Corbel.Common.Messaging;
using Corbel.Common.Web;
using Corbel.Infrastructure.Persistence;
using FluentValidation;
using Mediator;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Corbel.Features.Notes;

/// <summary>The mutable body — the id is taken from the route, so it never appears in the request schema.</summary>
public sealed record UpdateNoteRequest(string Title, string? Content);

public sealed record UpdateNoteCommand(Guid Id, string Title, string? Content) : IRequest<NoteResponse>, IWriteCommand;

public sealed class UpdateNoteValidator : AbstractValidator<UpdateNoteCommand>
{
    public UpdateNoteValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(NoteConstraints.TitleMaxLength);
        RuleFor(x => x.Content).MaximumLength(NoteConstraints.ContentMaxLength);
    }
}

public sealed class UpdateNoteHandler(AppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<UpdateNoteCommand, NoteResponse>
{
    public async ValueTask<NoteResponse> Handle(UpdateNoteCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireId();

        // Owner-scoped load: a note belonging to another user is indistinguishable from one that doesn't exist (anti-BOLA).
        var note = await db.Notes.FirstOrDefaultAsync(
                       n => n.Id == command.Id && n.OwnerId == userId, cancellationToken)
                   ?? throw new NoteNotFoundException();

        note.Edit(command.Title, command.Content);
        await db.SaveChangesAsync(cancellationToken);

        return NoteResponse.From(note);
    }
}

public sealed class UpdateNoteEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPut("notes/{id:guid}", Handle)
            .WithName("UpdateNote")
            .WithTags("Notes")
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict) // xmin optimistic-concurrency conflict
            .ProducesValidationProblem();

    private static async Task<Ok<NoteResponse>> Handle(
        Guid id, UpdateNoteRequest request, ISender sender, CancellationToken cancellationToken)
    {
        var note = await sender.Send(new UpdateNoteCommand(id, request.Title, request.Content), cancellationToken);
        return TypedResults.Ok(note);
    }
}
