using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using ApiTrustedProxy = SeoIntelligence.Api.Security.TrustedProxyExtensions;
using WebTrustedProxy = SeoIntelligence.Web.Configuration.TrustedProxyExtensions;

namespace IntegrationTests;

/// <summary>
/// The forwarded-headers trust boundary for both hosts.
/// </summary>
/// <remarks>
/// The previous configuration was <c>ASPNETCORE_FORWARDEDHEADERS_ENABLED=true</c>, which enables
/// the middleware by clearing the known-proxy and known-network lists - every source is trusted.
/// Only network isolation stopped a caller that reached the container from claiming any client
/// address, and the rate limiter partitions on that address.
/// </remarks>
public sealed class TrustedProxyForwardedHeadersTests
{
    private const string Subnet = "10.89.0.0/28";

    private static ForwardedHeadersOptions Resolve(
        Func<IServiceCollection, IConfiguration, IHostEnvironment, IServiceCollection> configure,
        string? subnet,
        string environmentName)
    {
        var settings = new Dictionary<string, string?>();
        if (subnet is not null)
        {
            settings[ApiTrustedProxy.SubnetConfigurationKey] = subnet;
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var environment = new StubHostEnvironment(environmentName);
        var services = new ServiceCollection();

        configure(services, configuration, environment);

        return services.BuildServiceProvider().GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;
    }

    public static TheoryData<string> Hosts => new() { "api", "web" };

    private static ForwardedHeadersOptions ResolveForHost(string host, string? subnet, string environmentName)
        => host == "api"
            ? Resolve((s, c, e) => ApiTrustedProxy.AddTrustedProxyForwardedHeaders(s, c, e), subnet, environmentName)
            : Resolve((s, c, e) => WebTrustedProxy.AddTrustedProxyForwardedHeaders(s, c, e), subnet, environmentName);

    [Theory]
    [MemberData(nameof(Hosts))]
    [Trait("Category", "Integration")]
    public void TrustsOnlyTheConfiguredNetwork(string host)
    {
        var options = ResolveForHost(host, Subnet, Environments.Production);

        var network = Assert.Single(options.KnownIPNetworks);
        Assert.True(network.Contains(IPAddress.Parse("10.89.0.5")), "an address inside the Caddy network is trusted");
        Assert.False(network.Contains(IPAddress.Parse("10.89.0.20")), "an address outside it is not");
        Assert.False(network.Contains(IPAddress.Parse("172.18.0.5")), "nor is the range Docker would have assigned");
    }

    [Theory]
    [MemberData(nameof(Hosts))]
    [Trait("Category", "Integration")]
    public void DoesNotTrustTheDefaultProxies(string host)
    {
        var options = ResolveForHost(host, Subnet, Environments.Production);

        // The defaults trust the loopback address, which is not where Caddy is. Leaving them would
        // widen the range this exists to narrow.
        Assert.Empty(options.KnownProxies);
    }

    [Theory]
    [MemberData(nameof(Hosts))]
    [Trait("Category", "Integration")]
    public void AcceptsOnlyTheHeadersCaddySets(string host)
    {
        var options = ResolveForHost(host, Subnet, Environments.Production);

        Assert.Equal(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto, options.ForwardedHeaders);

        // XForwardedHost is deliberately absent: the host decides the redirect URLs and the
        // allowed-forwarded-host check, and is worth more to an attacker than the address.
        Assert.False(options.ForwardedHeaders.HasFlag(ForwardedHeaders.XForwardedHost));
    }

    [Theory]
    [MemberData(nameof(Hosts))]
    [Trait("Category", "Integration")]
    public void WalksExactlyOneProxy(string host)
    {
        // One proxy stands in front of the container. A larger limit would let a caller prepend
        // entries and have the middleware walk past Caddy's own value.
        Assert.Equal(1, ResolveForHost(host, Subnet, Environments.Production).ForwardLimit);
    }

    [Theory]
    [MemberData(nameof(Hosts))]
    [Trait("Category", "Integration")]
    public void RefusesToStartInProductionWithoutASubnet(string host)
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => ResolveForHost(host, subnet: null, Environments.Production));

        Assert.Contains("not configured", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("api", "not-a-cidr")]
    [InlineData("api", "10.89.0.0")]
    [InlineData("web", "not-a-cidr")]
    [InlineData("web", "10.89.0.0")]
    [Trait("Category", "Integration")]
    public void RefusesToStartOnAnUnparseableSubnet(string host, string subnet)
    {
        // Silently ignoring it would leave the trusted list empty, which reads as "nothing is
        // trusted" and turns every client address into Caddy's.
        var error = Assert.Throws<InvalidOperationException>(
            () => ResolveForHost(host, subnet, Environments.Production));

        Assert.Contains("CIDR", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Hosts))]
    [Trait("Category", "Integration")]
    public void TrustsNothingOutsideProductionWhenUnset(string host)
    {
        // Reached directly in development, so there is no proxy to trust and no setting to miss.
        var options = ResolveForHost(host, subnet: null, Environments.Development);

        Assert.Empty(options.KnownIPNetworks);
        Assert.Empty(options.KnownProxies);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void BothHostsAgree()
    {
        // The two hosts carry their own copy of this configuration, because neither can reference
        // the other and the shared libraries do not depend on ASP.NET Core. Two copies of a
        // security policy drift; this is what notices.
        var api = ResolveForHost("api", Subnet, Environments.Production);
        var web = ResolveForHost("web", Subnet, Environments.Production);

        Assert.Equal(api.ForwardedHeaders, web.ForwardedHeaders);
        Assert.Equal(api.ForwardLimit, web.ForwardLimit);
        Assert.Equal(api.KnownProxies.Count, web.KnownProxies.Count);
        Assert.Equal(
            api.KnownIPNetworks.Select(n => n.ToString()),
            web.KnownIPNetworks.Select(n => n.ToString()));
        Assert.Equal(ApiTrustedProxy.SubnetConfigurationKey, WebTrustedProxy.SubnetConfigurationKey);
    }

    private sealed class StubHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
