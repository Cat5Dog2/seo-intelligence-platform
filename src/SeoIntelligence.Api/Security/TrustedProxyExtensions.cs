using Microsoft.AspNetCore.HttpOverrides;

// Microsoft.AspNetCore.HttpOverrides also defines an IPNetwork, deprecated in .NET 10 in favour of
// this one. Aliased rather than fully qualified at each use, so the wrong one cannot creep back in.
using IPNetwork = System.Net.IPNetwork;

namespace SeoIntelligence.Api.Security;

/// <summary>
/// Trusts <c>X-Forwarded-For</c> and <c>X-Forwarded-Proto</c> from the shared Caddy network only.
/// </summary>
/// <remarks>
/// <para>
/// This replaces <c>ASPNETCORE_FORWARDEDHEADERS_ENABLED=true</c>, which enables the middleware by
/// clearing the known-proxy and known-network lists - every source is trusted. Nothing but network
/// isolation then stops a caller that reaches the container from claiming any client address, and
/// the rate limiter partitions on that address: one caller could exhaust the window for everyone,
/// or evade their own limit by varying the header.
/// </para>
/// <para>
/// The range comes from wwt-seo-infra, which owns the Caddy network and verifies the value against
/// the real Docker IPAM configuration before deploying. It is not duplicated here.
/// </para>
/// </remarks>
public static class TrustedProxyExtensions
{
    public const string SubnetConfigurationKey = "TrustedProxy:Subnet";

    public static IServiceCollection AddTrustedProxyForwardedHeaders(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var trustedNetwork = ResolveTrustedNetwork(configuration, environment);

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            // Only what Caddy sets. XForwardedHost is not accepted: the host is what
            // Security__AllowedForwardedHosts and the redirect URLs are built from, and a forwarded
            // host is worth more to an attacker than a forwarded address.
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

            // Exactly one proxy stands in front of this container. Leaving the default would let a
            // caller prepend entries and have the middleware walk past Caddy's own value.
            options.ForwardLimit = 1;

            // Cleared rather than added to. The defaults trust the loopback address, which is not
            // where Caddy is, and leaving them would widen the range this exists to narrow.
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();

            if (trustedNetwork is { } network)
            {
                options.KnownIPNetworks.Add(network);
            }
        });

        return services;
    }

    private static IPNetwork? ResolveTrustedNetwork(IConfiguration configuration, IHostEnvironment environment)
    {
        var configured = configuration[SubnetConfigurationKey];

        if (string.IsNullOrWhiteSpace(configured))
        {
            // Outside Production the application is reached directly and there is no proxy to
            // trust, so an empty list is the correct answer rather than a missing setting.
            if (!environment.IsProduction())
            {
                return null;
            }

            throw new InvalidOperationException(
                $"{SubnetConfigurationKey} is not configured. In Production the forwarded headers "
                + "decide the client address, and trusting them from an unknown range is the same as "
                + "trusting every caller. wwt-seo-infra passes the Caddy network's subnet as "
                + "CADDY_NETWORK_SUBNET; deploy through its scripts/seo wrapper rather than calling "
                + "scripts/deploy-production.sh directly.");
        }

        if (!IPNetwork.TryParse(configured, out var parsed))
        {
            throw new InvalidOperationException(
                $"{SubnetConfigurationKey} is '{configured}', which is not a CIDR range such as "
                + "10.89.0.0/28. A value that cannot be parsed would otherwise leave the trusted "
                + "list empty, which reads as 'nothing is trusted' and silently turns every client "
                + "address into Caddy's.");
        }

        return parsed;
    }
}
