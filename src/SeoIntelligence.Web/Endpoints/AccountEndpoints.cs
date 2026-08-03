using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SeoIntelligence.Application.Accounts;
using SeoIntelligence.Application.Security;
using SeoIntelligence.Infrastructure.Identity;
using SeoIntelligence.Web.Security;

namespace SeoIntelligence.Web.Endpoints;

public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/login", LoginFromFormAsync)
            .AllowAnonymous()
            .RequireRateLimiting(SecurityRateLimitPolicyNames.Login)
            .RequireCsrfToken();

        endpoints.MapPost("/logout", LogoutAsync)
            .RequireAuthorization()
            .RequireCsrfToken();

        endpoints.MapPost("/account/password", ChangePasswordFromFormAsync)
            .RequireAuthorization()
            .RequireRateLimiting(SecurityRateLimitPolicyNames.PasswordChange)
            .RequireCsrfToken();

        return endpoints;
    }

    private static async Task<IResult> LoginFromFormAsync(
        [FromForm] LoginForm form,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("SeoIntelligence.Web.Login");

        if (string.IsNullOrWhiteSpace(form.Email) || string.IsNullOrWhiteSpace(form.Password))
        {
            return Results.Redirect(BuildLoginErrorUrl(form.ReturnUrl, "invalid"));
        }

        var user = await userManager.FindByEmailAsync(form.Email);
        if (user is null)
        {
            // Reported as a generic failure so the response does not reveal which accounts exist.
            logger.LogInformation("Sign-in was rejected because the account does not exist.");
            return Results.Redirect(BuildLoginErrorUrl(form.ReturnUrl, "invalid"));
        }

        if (!user.IsEnabled)
        {
            logger.LogWarning("Sign-in was rejected for user {user_id} because the account is disabled.", user.Id);
            return Results.Redirect(BuildLoginErrorUrl(form.ReturnUrl, "disabled"));
        }

        var result = await signInManager.PasswordSignInAsync(
            user,
            form.Password,
            form.RememberMe,
            lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            logger.LogWarning("Sign-in was rejected for user {user_id} because the account is locked out.", user.Id);
            return Results.Redirect(BuildLoginErrorUrl(form.ReturnUrl, "lockout"));
        }

        if (!result.Succeeded)
        {
            logger.LogInformation("Sign-in failed for user {user_id}.", user.Id);
            return Results.Redirect(BuildLoginErrorUrl(form.ReturnUrl, "invalid"));
        }

        user.LastLoginAt = timeProvider.GetUtcNow();
        user.UpdatedAt = user.LastLoginAt.Value;
        await userManager.UpdateAsync(user);

        logger.LogInformation("Sign-in succeeded for user {user_id}.", user.Id);
        return Results.Redirect(GetSafeReturnUrl(form.ReturnUrl));
    }

    private static async Task<IResult> LogoutAsync(SignInManager<ApplicationUser> signInManager)
    {
        await signInManager.SignOutAsync();
        return Results.Redirect("/login");
    }

    private static async Task<IResult> ChangePasswordFromFormAsync(
        [FromForm] ChangePasswordForm form,
        ClaimsPrincipal principal,
        IAccountPasswordService passwordService,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        CancellationToken cancellationToken)
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Results.Redirect("/login");
        }

        var result = await passwordService.ChangePasswordAsync(
            new ChangePasswordCommand(
                userId,
                form.CurrentPassword ?? string.Empty,
                form.NewPassword ?? string.Empty,
                form.ConfirmNewPassword ?? string.Empty),
            cancellationToken);

        if (result.Succeeded)
        {
            // Refreshing the sign-in reissues the cookie against the rotated security stamp.
            var user = await userManager.FindByIdAsync(userId);
            if (user is not null)
            {
                await signInManager.RefreshSignInAsync(user);
            }

            return Results.Redirect("/account?passwordChanged=true");
        }

        var error = result.Error switch
        {
            ChangePasswordError.UserNotFound => "user",
            ChangePasswordError.InvalidCurrentPassword => "current",
            ChangePasswordError.NewPasswordMismatch => "confirm",
            ChangePasswordError.NewPasswordSameAsCurrent => "same",
            ChangePasswordError.InvalidNewPassword => "new",
            _ => "failed"
        };

        return Results.Redirect($"/account?passwordError={error}");
    }

    private static string BuildLoginErrorUrl(string? returnUrl, string error)
    {
        var url = $"/login?loginError={Uri.EscapeDataString(error)}";
        var safeReturnUrl = GetSafeReturnUrl(returnUrl);

        if (safeReturnUrl != "/")
        {
            url += $"&returnUrl={Uri.EscapeDataString(safeReturnUrl)}";
        }

        return url;
    }

    private static string GetSafeReturnUrl(string? returnUrl)
        => SafeReturnUrl.Resolve(returnUrl);

    private sealed class LoginForm
    {
        public string? Email { get; set; }

        public string? Password { get; set; }

        public bool RememberMe { get; set; }

        public string? ReturnUrl { get; set; }
    }

    private sealed class ChangePasswordForm
    {
        public string? CurrentPassword { get; set; }

        public string? NewPassword { get; set; }

        public string? ConfirmNewPassword { get; set; }
    }
}
