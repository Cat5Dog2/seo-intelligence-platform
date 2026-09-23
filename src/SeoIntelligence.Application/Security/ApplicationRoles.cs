namespace SeoIntelligence.Application.Security;

public static class ApplicationRoles
{
    public const string Admin = "Admin";
    public const string User = "User";
    // Ephemeral Web demo sessions only; never seeded into the Identity database.
    public const string Guest = "Guest";

    public static readonly string[] All = [Admin, User];
}
