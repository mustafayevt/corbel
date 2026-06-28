using Corbel.Common;
using Corbel.Common.Abstractions;
using Corbel.Common.Exceptions;
using Corbel.Common.Messaging;
using Corbel.Common.Validation;
using Corbel.Common.Web;
using Corbel.Domain.Entities;
using Corbel.Infrastructure.Auth;
using FluentValidation;
using Mediator;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

namespace Corbel.Features.Auth;

public sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword) : IRequest<MessageResponse>, IWriteCommand;

public sealed class ChangePasswordValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().MaximumLength(PasswordPolicy.MaxLength);
        RuleFor(x => x.NewPassword)
            .Password()
            .NotEqual(x => x.CurrentPassword).WithMessage("The new password must differ from the current password.");
    }
}

public sealed class ChangePasswordHandler(
    UserManager<AppUser> userManager,
    ICurrentUser currentUser,
    RefreshTokenService refreshTokens) : IRequestHandler<ChangePasswordCommand, MessageResponse>
{
    public async ValueTask<MessageResponse> Handle(ChangePasswordCommand command, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(currentUser.RequireId().ToString())
                   ?? throw new NotAuthenticatedException();

        var result = await userManager.ChangePasswordAsync(user, command.CurrentPassword, command.NewPassword);
        if (!result.Succeeded)
            throw result.ToValidationException();

        // Changing the password kills every other session (revoked atomically with the change via the
        // transaction behavior); the caller is signed out by the endpoint clearing the cookies.
        await refreshTokens.RevokeAllForUserAsync(user.Id, cancellationToken);

        return new MessageResponse("Your password has been changed.");
    }
}

public sealed class ChangePasswordEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("auth/change-password", Handle)
            .WithName("ChangePassword")
            .WithTags("Auth")
            .RequireAuthorization()
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesValidationProblem();

    private static async Task<Ok<MessageResponse>> Handle(
        ChangePasswordCommand command, ISender sender, HttpContext httpContext, AuthCookies cookies, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        cookies.Clear(httpContext); // refresh tokens were revoked; drop the now-useless cookies too
        return TypedResults.Ok(result);
    }
}
