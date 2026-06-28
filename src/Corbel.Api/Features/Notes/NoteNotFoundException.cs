using Corbel.Common.Errors;
using Corbel.Common.Exceptions;

namespace Corbel.Features.Notes;

/// <summary>A note that doesn't exist OR isn't owned by the caller — the same 404 either way (anti-BOLA).</summary>
public sealed class NoteNotFoundException() : NotFoundException(ErrorCodes.NoteNotFound, "Note not found");
