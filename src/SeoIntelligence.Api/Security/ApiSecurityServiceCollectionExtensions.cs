using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using SeoIntelligence.Application.Configuration;

namespace SeoIntelligence.Api.Security;

public static class ApiSecurityServiceCollectionExtensions
{
    /// <summary>
    /// Requires a valid service key on every endpoint by default. Endpoints that must stay
    /// reachable without one (health probes and the public report share link) opt out with
    /// <c>AllowAnonymous</c>.
    /// </summary>
    public static IServiceCollection AddApiServiceKeyAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = new ServiceAuthenticationOptions();
        configuration.GetSection(ServiceAuthenticationOptions.SectionName).Bind(options);

        var errors = options.Validate();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }

        services.AddOptions<ServiceAuthenticationOptions>()
            .Configure(configured => configured.ServiceKeyRef = options.ServiceKeyRef);

        services
            .AddAuthentication(ServiceKeyAuthenticationDefaults.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, ServiceKeyAuthenticationHandler>(
                ServiceKeyAuthenticationDefaults.SchemeName,
                configureOptions: null);

        services.AddAuthorization(authorization =>
        {
            authorization.FallbackPolicy = new AuthorizationPolicyBuilder(
                    ServiceKeyAuthenticationDefaults.SchemeName)
                .RequireAuthenticatedUser()
                .Build();
        });

        return services;
    }
}
