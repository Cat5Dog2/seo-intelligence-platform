using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using SeoIntelligence.Application.Security;
using SeoIntelligence.Web.Services;

namespace SeoIntelligence.Web.Security;

public static class GuestAuthentication
{
    public const string ModeClaim = "seo:execution-mode";
    public const string MockMode = "Mock";

    public static bool IsGuest(ClaimsPrincipal? principal)
        => principal?.Identity?.IsAuthenticated == true
            && principal.IsInRole(ApplicationRoles.Guest)
            && principal.HasClaim(ModeClaim, MockMode);

    public static async Task ValidatePrincipalAsync(CookieValidatePrincipalContext context)
    {
        if (!IsGuest(context.Principal))
        {
            await SecurityStampValidator.ValidatePrincipalAsync(context);
            return;
        }

        // Guests have no Identity row or security stamp. Their server-side demo session is
        // the authority for expiry and logout, including old cookies and Blazor circuits.
        var sessions = context.HttpContext.RequestServices.GetRequiredService<GuestDemoSessionStore>();
        if (sessions.Find(context.Principal!) is null)
        {
            context.RejectPrincipal();
        }
    }
}
