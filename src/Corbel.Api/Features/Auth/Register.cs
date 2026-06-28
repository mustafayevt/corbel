using Corbel.Common;
using Corbel.Common.Messaging;
using Corbel.Common.Validation;
using Corbel.Common.Web;
using Corbel.Domain.Entities;
using FluentValidation;
using Mediator;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

namespace Corbel.Features.Auth;

/// <summary>A new-account registration request.</summary>
/// <param name="Email">The email address to register; also becomes the username.</param>
/// <param name="Password">The account password. Must meet the complexity policy (min length, upper, lower, digit).</param>
/// <param name="DisplayName">An optional display name.</param>
public sealed record RegisterCommand(string Email, string Password, string? DisplayName) : IRequest<MessageResponse>, IWriteCommand;

public sealed class RegisterValidator : AbstractValidator<RegisterCommand>
{
    public RegisterValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(AppUserConstraints.EmailMaxLength);
        RuleFor(x => x.Password).Password();
        RuleFor(x => x.DisplayName).MaximumLength(AppUserConstraints.DisplayNameMaxLength);
    }
}

public sealed class RegisterHandler(
    UserManager<AppUser> userManager,
    IPasswordHasher<AppUser> passwordHasher,
    ILogger<RegisterHandler> logger) : IRequestHandler<RegisterCommand, MessageResponse>
{
    // Shared so the taken-email and create paths return a byte-identical body (see the anti-enumeration note below).
    private static readonly MessageResponse Acknowledgement = new("Registration received. You can now sign in.");

    public async ValueTask<MessageResponse> Handle(RegisterCommand command, CancellationToken cancellationToken)
    {
        var emailAddress = command.Email.Trim();

        // Anti-enumeration: identical response whether or not the email is taken, and a dummy Argon2 hash on the
        // taken-email path so it pays the same dominant hashing cost as the create path. The residual delta — the
        // create path additionally runs INSERTs (CreateAsync + AddToRoleAsync) — is small next to the hash and to
        // network jitter, so this closes the meaningful timing oracle rather than every last nanosecond.
        if (await userManager.FindByEmailAsync(emailAddress) is not null)
        {
            _ = passwordHasher.HashPassword(new AppUser(), command.Password);
            return Acknowledgement;
        }

        var user = new AppUser
        {
            Id = Guid.CreateVersion7(),
            UserName = emailAddress,
            Email = emailAddress,
            EmailConfirmed = true, // lean core has no email-confirmation flow; accounts are usable immediately
            DisplayName = string.IsNullOrWhiteSpace(command.DisplayName) ? null : command.DisplayName.Trim(),
        };

        var created = await userManager.CreateAsync(user, command.Password);
        if (!created.Succeeded)
        {
            // A duplicate slipping past the check above (race) still must not leak — fall back to the ack.
            if (created.Errors.Any(e => e.Code is "DuplicateUserName" or "DuplicateEmail"))
                return Acknowledgement;

            throw created.ToValidationException();
        }

        // Atomic with the user insert (TransactionBehavior): a failed role grant rolls the whole command back
        // instead of leaving a role-less account. A throw here is a server misconfig (e.g. roles unseeded) → 500.
        var roleAssigned = await userManager.AddToRoleAsync(user, AppRoles.User);
        if (!roleAssigned.Succeeded)
            throw new InvalidOperationException(
                $"Failed to assign the default role: {string.Join("; ", roleAssigned.Errors.Select(e => e.Description))}");

        logger.LogInformation("Registered new user {UserId}", user.Id);
        return Acknowledgement;
    }
}

public sealed class RegisterEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("auth/register", Handle)
            .WithName("Register")
            .WithTags("Auth")
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .WithSummary("Register a new account.")
            .WithDescription(
                "Creates a user with the default role. To prevent account enumeration, the response is an identical acknowledgement whether or not the email was already registered — this endpoint never returns 409.\n\n"
                + "**Errors:** 400 `common.validation`, 429 `common.rate_limited`.")
            .ProducesValidationProblem();

    private static async Task<Ok<MessageResponse>> Handle(
        RegisterCommand command, ISender sender, CancellationToken cancellationToken)
        => TypedResults.Ok(await sender.Send(command, cancellationToken));
}
