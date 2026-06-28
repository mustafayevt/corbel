using Microsoft.AspNetCore.Identity;

namespace Corbel.Domain.Entities;

/// <summary>
/// Application user. The one pragmatic "framework" entity: it extends ASP.NET Core Identity rather than
/// the domain <see cref="Common.Entity"/> base. Ids are Guid v7, assigned at registration time.
/// </summary>
public sealed class AppUser : IdentityUser<Guid>
{
    public string? DisplayName { get; set; }
}
