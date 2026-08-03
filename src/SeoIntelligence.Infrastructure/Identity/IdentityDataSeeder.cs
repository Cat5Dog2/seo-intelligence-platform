using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SeoIntelligence.Application.Security;

namespace SeoIntelligence.Infrastructure.Identity;

public sealed class IdentityDataSeeder(
    RoleManager<IdentityRole> roleManager,
    UserManager<ApplicationUser> userManager,
    IOptions<AdminSeedOptions> options,
    TimeProvider timeProvider,
    ILogger<IdentityDataSeeder> logger)
    : IIdentityDataSeeder
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await SeedRolesAsync(cancellationToken);

        var admins = await userManager.GetUsersInRoleAsync(ApplicationRoles.Admin);
        if (admins.Count > 0)
        {
            logger.LogInformation("Admin seed skipped because at least one Admin user already exists.");
            return;
        }

        var seed = options.Value;
        if (string.IsNullOrWhiteSpace(seed.Email) || string.IsNullOrWhiteSpace(seed.Password))
        {
            // Failing closed: starting without an Admin and without seed credentials would leave a
            // healthy application that nobody can sign in to.
            throw new InvalidOperationException(
                "No Admin user exists and AdminSeed credentials are not configured. "
                + $"Set {AdminSeedOptions.SectionName}:{nameof(AdminSeedOptions.Email)} and "
                + $"{AdminSeedOptions.SectionName}:{nameof(AdminSeedOptions.Password)} "
                + "(AdminSeed__Email / AdminSeed__Password) so an administrator can be created.");
        }

        var admin = await FindOrCreateSeedUserAsync(seed);

        var roleAssignResult = await userManager.AddToRoleAsync(admin, ApplicationRoles.Admin);
        EnsureSucceeded(roleAssignResult, "Failed to assign the Admin role to the initial Admin user.");

        logger.LogInformation("Initial Admin user was seeded.");
    }

    /// <summary>
    /// Reuses an existing account with the seed address instead of creating a second one. A previous
    /// startup can have created the user and then failed before the role was assigned; without this,
    /// every later startup would fail on a duplicate address and the application could never recover.
    /// </summary>
    private async Task<ApplicationUser> FindOrCreateSeedUserAsync(AdminSeedOptions seed)
    {
        var existing = await userManager.FindByEmailAsync(seed.Email!);
        if (existing is not null)
        {
            logger.LogWarning(
                "A user with the configured seed address already exists without the Admin role. "
                + "Assigning the Admin role to user {user_id} instead of creating another account.",
                existing.Id);
            return existing;
        }

        var now = timeProvider.GetUtcNow();
        var admin = new ApplicationUser
        {
            UserName = seed.Email,
            Email = seed.Email,
            EmailConfirmed = true,
            DisplayName = string.IsNullOrWhiteSpace(seed.DisplayName) ? "Admin" : seed.DisplayName,
            IsEnabled = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        var createResult = await userManager.CreateAsync(admin, seed.Password!);
        EnsureSucceeded(createResult, "Failed to seed the initial Admin user.");

        return admin;
    }

    private async Task SeedRolesAsync(CancellationToken cancellationToken)
    {
        foreach (var role in ApplicationRoles.All)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!await roleManager.RoleExistsAsync(role))
            {
                var roleResult = await roleManager.CreateAsync(new IdentityRole(role));
                EnsureSucceeded(roleResult, $"Failed to seed the role '{role}'.");
            }
        }
    }

    // Identity error codes are safe to log; Identity never places the submitted password in them.
    private static void EnsureSucceeded(IdentityResult result, string message)
    {
        if (result.Succeeded)
        {
            return;
        }

        var errors = string.Join(", ", result.Errors.Select(error => error.Code));
        throw new InvalidOperationException($"{message} Identity errors: {errors}");
    }
}
