using Microsoft.AspNetCore.Identity;

namespace SeoIntelligence.Infrastructure.Identity;

public static class SeoIntelligenceIdentityOptions
{
    public const int MinimumPasswordLength = 12;

    public const int MaximumFailedAccessAttempts = 5;

    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    /// <summary>
    /// The password and lockout policy for the single administrator account. Kept here rather than
    /// inline in the Web host so the values are asserted by tests and stay aligned with
    /// docs/basic_design.md.
    /// </summary>
    public static void Configure(IdentityOptions options)
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.User.RequireUniqueEmail = true;

        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = MaximumFailedAccessAttempts;
        options.Lockout.DefaultLockoutTimeSpan = LockoutDuration;

        options.Password.RequiredLength = MinimumPasswordLength;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
    }
}
