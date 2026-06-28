using Corbel.Common.Errors;
using Corbel.Common.Exceptions;
using Corbel.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace Corbel.Common.Web;

/// <summary>
/// Single exception → ProblemDetails (RFC 9457) mapping point. Expected failures map to their status codes and
/// are logged as warnings; anything unexpected becomes a 500 (detail hidden outside Development) and is logged
/// as an error. <c>Title</c> is the stable status reason phrase; the machine-readable <c>errorCode</c> carries
/// the specifics, and a correlation <c>traceId</c> is added by the ProblemDetails customization.
/// </summary>
public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    IHostEnvironment environment,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        // A client that disconnected mid-request isn't a server fault — let the framework abort quietly.
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
            return false;

        var (status, errorCode) = Map(exception);

        if (status >= StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Unhandled exception");
        else
            logger.LogWarning("Handled {ErrorCode}: {Message}", errorCode, exception.Message);

        httpContext.Response.StatusCode = status;

        var problemDetails = new ProblemDetails
        {
            Status = status,
            Title = ReasonPhrases.GetReasonPhrase(status),
            Instance = httpContext.Request.Path,
            Detail = Detail(exception, status),
        };
        problemDetails.Extensions["errorCode"] = errorCode;

        if (exception is ValidationException validation)
            problemDetails.Extensions["errors"] = validation.Errors;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails,
            Exception = exception,
        });
    }

    private static (int Status, string ErrorCode) Map(Exception exception) => exception switch
    {
        AppException app => (app.StatusCode, app.ErrorCode),
        DomainException domain => (StatusCodes.Status422UnprocessableEntity, domain.ErrorCode),
        DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, ErrorCodes.Concurrency),
        _ => (StatusCodes.Status500InternalServerError, ErrorCodes.Unexpected),
    };

    // AppException/DomainException carry curated, client-safe messages, so those flow through verbatim. Everything
    // else is a framework/persistence exception whose Message can leak internals, so it gets a stable, safe string:
    // a generic line for 500s outside Development, and a curated hint for the concurrency 409 (whose raw EF text —
    // "expected to affect 1 row(s) but actually affected 0" — is meaningless and leaky to a client).
    private string Detail(Exception exception, int status) => exception switch
    {
        AppException or DomainException => exception.Message,
        DbUpdateConcurrencyException => "The record was modified by another request. Reload and try again.",
        _ when status >= StatusCodes.Status500InternalServerError && !environment.IsDevelopment()
            => "An unexpected error occurred.",
        _ => exception.Message,
    };
}
