using System.ComponentModel.DataAnnotations;

namespace Corbel.Common.Options;

/// <summary>
/// JWT settings, bound from configuration and validated on startup (ValidateOnStart). No usable default
/// ships — the app refuses to boot without a real signing key of at least 32 bytes (256-bit for HS256).
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; set; } = default!;

    [Required]
    public string Audience { get; set; } = default!;

    [Required, MinLength(32, ErrorMessage = "Jwt:SigningKey must be at least 32 characters (256-bit).")]
    public string SigningKey { get; set; } = default!;

    [Range(1, 1440)]
    public int AccessTokenMinutes { get; set; } = 15;

    [Range(1, 365)]
    public int RefreshTokenDays { get; set; } = 7;

    [Range(1, 3650)]
    public int RefreshAbsoluteDays { get; set; } = 30;
}
