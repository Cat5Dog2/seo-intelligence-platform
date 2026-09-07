using Microsoft.AspNetCore.HttpOverrides;

// Microsoft.AspNetCore.HttpOverrides also defines an IPNetwork, deprecated in .NET 10 in favour of
// this one. Aliased rather than fully qualified at each use, so the wrong one cannot creep back in.
using IPNetwork = System.Net.IPNetwork;

namespace SeoIntelligence.Web.Configuration;

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

        if (trustedNetwork is not { } network)
        {
            // Nothing is configured outside Production, and the options are deliberately left
            // alone. Enabling the middleware with empty KnownProxies and KnownIPNetworks does not
            // mean "trust nobody": ASP.NET Core skips the known-address check when both lists are
            // empty, so every source is trusted. Verified by running the middleware, in
            // ForwardedHeadersBehaviourTests. The default ForwardedHeaders.None makes it a no-op.
            return services;
        }

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
            // where Caddy is, and leaving them would widen the range this exists to narrow. The
            // list is never left empty: see the early return above for why that is not "trust
            // nobody".
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();
            options.KnownIPNetworks.Add(network);
        });

        return services;
    }

    private static IPNetwork? ResolveTrustedNetwork(IConfiguration configuration, IHostEnvironment environment)
    {
        var configured = configuration[SubnetConfigurationKey];

        if (string.IsNullOrWhiteSpace(configured))
        {
            // Outside Production the application is reached directly and there is no proxy to
            // trust. The caller leaves the middleware unconfigured entirely rather than configuring
            // it with an empty list, which would trust everyone.
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
                + "10.89.0.0/28. Continuing without a trusted range would enable the middleware "
                + "with an empty list, which ASP.NET Core reads as 'check nothing' and accepts the "
                + "headers from every source.");
        }

        if (parsed.PrefixLength == 0)
        {
            throw new InvalidOperationException(
                $"{SubnetConfigurationKey} is '{configured}', which covers every address. That is "
                + "the same as trusting every caller, which is what naming a proxy network exists "
                + "to avoid.");
        }

        return parsed;
    }
}
