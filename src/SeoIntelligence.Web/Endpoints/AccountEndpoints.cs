using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SeoIntelligence.Application.Accounts;
using SeoIntelligence.Application.Security;
using SeoIntelligence.Infrastructure.Identity;
using SeoIntelligence.Web.Security;
using SeoIntelligence.Web.Services;

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

        endpoints.MapPost("/login/guest", GuestLoginAsync)
            .AllowAnonymous()
            .RequireRateLimiting(SecurityRateLimitPolicyNames.Login)
            .RequireCsrfToken();

        endpoints.MapPost("/account/password", ChangePasswordFromFormAsync)
            .RequireAuthorization(ApplicationPolicies.RequireRegisteredAccount)
            .RequireRateLimiting(SecurityRateLimitPolicyNames.PasswordChange)
            .RequireCsrfToken();

        return endpoints;
    }

    private static async Task<IResult> LoginFromFormAsync(
        [FromForm] LoginForm form,
        HttpContext context,
        GuestDemoSessionStore sessions,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("SeoIntelligence.Web.Login");
        var previousPrincipal = context.User;

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
        if (GuestAuthentication.IsGuest(previousPrincipal))
        {
            sessions.Remove(previousPrincipal);
        }
        return Results.Redirect(GetSafeReturnUrl(form.ReturnUrl));
    }

    private static async Task<IResult> GuestLoginAsync(
        [FromForm] GuestLoginForm form,
        HttpContext context,
        GuestDemoSessionStore sessions,
        SignInManager<ApplicationUser> signInManager,
        TimeProvider timeProvider)
    {
        if (GuestAuthentication.IsGuest(context.User))
        {
            sessions.Remove(context.User);
        }
        await signInManager.SignOutAsync();
        var id = sessions.Create();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, id),
            new Claim(ClaimTypes.Name, "ゲスト"),
            new Claim(ClaimTypes.Role, ApplicationRoles.Guest),
            new Claim(GuestAuthentication.ModeClaim, GuestAuthentication.MockMode)
        ], IdentityConstants.ApplicationScheme));
        await context.SignInAsync(IdentityConstants.ApplicationScheme, principal, new AuthenticationProperties
        {
            IsPersistent = false,
            AllowRefresh = false,
            ExpiresUtc = timeProvider.GetUtcNow().Add(GuestDemoSessionStore.Lifetime)
        });
        return Results.Redirect(GetSafeReturnUrl(form.ReturnUrl));
    }

    private static async Task<IResult> LogoutAsync(
        HttpContext context,
        GuestDemoSessionStore sessions,
        SignInManager<ApplicationUser> signInManager)
    {
        if (GuestAuthentication.IsGuest(context.User))
        {
            sessions.Remove(context.User);
        }
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

    private sealed class GuestLoginForm
    {
        public string? ReturnUrl { get; set; }
    }

    private sealed class ChangePasswordForm
    {
        public string? CurrentPassword { get; set; }

        public string? NewPassword { get; set; }

        public string? ConfirmNewPassword { get; set; }
    }
}
