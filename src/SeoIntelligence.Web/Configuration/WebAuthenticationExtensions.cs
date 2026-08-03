using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using SeoIntelligence.Application.Configuration;
using SeoIntelligence.Application.Security;
using SeoIntelligence.Infrastructure.Identity;
using SeoIntelligence.Infrastructure.Persistence;
using SeoIntelligence.Infrastructure.Secrets;
using SeoIntelligence.Web.Security;

namespace SeoIntelligence.Web.Configuration;

public static class WebAuthenticationExtensions
{
    public static IServiceCollection AddSeoIntelligenceWebAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        services.AddSeoIntelligenceSecretStore(configuration);
        services.AddSeoIntelligenceIdentityStores(configuration);

        services
            .AddIdentity<ApplicationUser, IdentityRole>(SeoIntelligenceIdentityOptions.Configure)
            .AddEntityFrameworkStores<SeoIntelligenceDbContext>()
            .AddDefaultTokenProviders();

        services.ConfigureApplicationCookie(options =>
        {
            // The __Host- prefix requires Secure and Path=/, which browsers will not accept over
            // plain HTTP outside localhost, so development uses the unprefixed name.
            options.Cookie.Name = environment.IsDevelopment()
                ? "SeoIntelligence.Auth"
                : "__Host-SeoIntelligence.Auth";
            options.Cookie.HttpOnly = true;
            options.Cookie.Path = "/";
            options.Cookie.SecurePolicy = environment.IsDevelopment()
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = true;
            options.LoginPath = "/login";
            options.LogoutPath = "/logout";
            options.AccessDeniedPath = "/forbidden";
        });

        services.AddCascadingAuthenticationState();
        services.AddAuthorization(options =>
        {
            options.AddPolicy(
                ApplicationPolicies.RequireAdmin,
                policy => policy.RequireRole(ApplicationRoles.Admin));
        });

        services.AddAntiforgery(options => options.HeaderName = CsrfEndpointFilter.HeaderName);
        services.AddSecurityRateLimiting();

        services.AddOptions<ServiceAuthenticationOptions>()
            .Bind(configuration.GetSection(ServiceAuthenticationOptions.SectionName));
        services.AddTransient<ServiceKeyHttpMessageHandler>();

        return services;
    }
}
