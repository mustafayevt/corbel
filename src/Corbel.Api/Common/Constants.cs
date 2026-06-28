namespace Corbel.Common;

/// <summary>The application's role names — seeded by DbSeeder, assigned on registration, used by authz policies.</summary>
public static class AppRoles
{
    public const string Admin = "Admin";
    public const string User = "User";

    public static readonly string[] All = [Admin, User];
}

/// <summary>Password length policy — the single source for the Identity option and the FluentValidation rules.</summary>
public static class PasswordPolicy
{
    public const int MinLength = 8;
    public const int MaxLength = 128;
}

/// <summary>Note field length limits — shared by the EF mapping and the create/update validators.</summary>
public static class NoteConstraints
{
    public const int TitleMaxLength = 200;
    public const int ContentMaxLength = 10_000;
}

/// <summary>AppUser field length limits — the single source for the EF mapping and the registration validator.</summary>
public static class AppUserConstraints
{
    public const int DisplayNameMaxLength = 100;
    public const int EmailMaxLength = 256;
}
