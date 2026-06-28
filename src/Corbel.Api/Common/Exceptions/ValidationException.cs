using Corbel.Common.Errors;

namespace Corbel.Common.Exceptions;

/// <summary>Thrown by the validation pipeline behavior; the global handler renders it as a 400 with field errors.</summary>
public sealed class ValidationException(IReadOnlyDictionary<string, string[]> errors)
    : AppException(ErrorCodes.Validation, "One or more validation errors occurred.")
{
    public override int StatusCode => StatusCodes.Status400BadRequest;

    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}
