using Corbel.Common;
using Corbel.Common.Abstractions;
using Corbel.Common.Messaging;
using Corbel.Common.Web;
using Corbel.Domain.Entities;
using Corbel.Infrastructure.Persistence;
using FluentValidation;
using Mediator;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Corbel.Features.Notes;

public sealed record CreateNoteCommand(string Title, string? Content) : IRequest<NoteResponse>, IWriteCommand;

public sealed class CreateNoteValidator : AbstractValidator<CreateNoteCommand>
{
    public CreateNoteValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(NoteConstraints.TitleMaxLength);
        RuleFor(x => x.Content).MaximumLength(NoteConstraints.ContentMaxLength);
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
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesValidationProblem();

    private static async Task<CreatedAtRoute<NoteResponse>> Handle(
        CreateNoteCommand command, ISender sender, CancellationToken cancellationToken)
    {
        var note = await sender.Send(command, cancellationToken);
        return TypedResults.CreatedAtRoute(note, "GetNote", new { id = note.Id });
    }
}
