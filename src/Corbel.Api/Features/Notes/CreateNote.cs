using Corbel.Common.Abstractions;
using Corbel.Common.Messaging;
using Corbel.Common.Validation;
using Corbel.Common.Web;
using Corbel.Domain.Entities;
using Corbel.Infrastructure.Persistence;
using FluentValidation;
using Mediator;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Corbel.Features.Notes;

/// <summary>A note to create.</summary>
/// <param name="Title">The note's title (required).</param>
/// <param name="Content">The note's body (optional).</param>
public sealed record CreateNoteCommand(string Title, string? Content) : IRequest<NoteResponse>, IWriteCommand;

public sealed class CreateNoteValidator : AbstractValidator<CreateNoteCommand>
{
    public CreateNoteValidator()
    {
        RuleFor(x => x.Title).NoteTitle();
        RuleFor(x => x.Content).NoteContent();
    }
}

public sealed class CreateNoteHandler(AppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<CreateNoteCommand, NoteResponse>
{
    public async ValueTask<NoteResponse> Handle(CreateNoteCommand command, CancellationToken cancellationToken)
    {
        // Ownership is stamped here, never accepted from the client.
        var note = Note.Create(command.Title, command.Content, currentUser.RequireId());

        db.Notes.Add(note);
        await db.SaveChangesAsync(cancellationToken);

        return NoteResponse.From(note);
    }
}

public sealed class CreateNoteEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("notes", Handle)
            .WithName("CreateNote")
            .WithTags("Notes")
            .RequireAuthorization()
            .WithSummary("Create a note.")
            .WithDescription(
                "Creates a note owned by the authenticated caller and returns it; the new note's URL is in the `Location` header.\n\n"
                + "**Errors:** 400 `common.validation`, 401 `common.unauthorized`, 429 `common.rate_limited`.")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesValidationProblem();

    private static async Task<CreatedAtRoute<NoteResponse>> Handle(
        CreateNoteCommand command, ISender sender, CancellationToken cancellationToken)
    {
        var note = await sender.Send(command, cancellationToken);
        return TypedResults.CreatedAtRoute(note, "GetNote", new { id = note.Id });
    }
}
