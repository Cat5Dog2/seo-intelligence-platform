using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using ApiTrustedProxy = SeoIntelligence.Api.Security.TrustedProxyExtensions;

namespace IntegrationTests;

/// <summary>
/// What the forwarded-headers middleware actually does with a request, rather than what the
/// options object contains.
/// </summary>
/// <remarks>
/// Asserting on <see cref="ForwardedHeadersOptions"/> alone cannot distinguish "trusts nothing"
/// from "trusts everything": in ASP.NET Core, empty <c>KnownProxies</c> and <c>KnownIPNetworks</c>
/// lists disable the known-address check rather than failing it. These run the middleware.
/// </remarks>
public sealed class ForwardedHeadersBehaviourTests
{
    private const string Subnet = "10.89.0.0/28";

    private static async Task<(string? RemoteIp, string Scheme)> SendAsync(
        string? subnet,
        string environmentName,
        string remoteAddress,
        string? forwardedFor,
        string? forwardedProto = null)
    {
        var settings = new Dictionary<string, string?>();
        if (subnet is not null)
        {
            settings[ApiTrustedProxy.SubnetConfigurationKey] = subnet;
        }

        using var host = await new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.UseEnvironment(environmentName);
                webHost.ConfigureAppConfiguration((_, builder) => builder.AddInMemoryCollection(settings));
                webHost.ConfigureServices((context, services) =>
                    ApiTrustedProxy.AddTrustedProxyForwardedHeaders(
                        services, context.Configuration, context.HostingEnvironment));
                webHost.Configure(app =>
                {
                    app.UseForwardedHeaders();
                    app.Run(async httpContext =>
                    {
                        httpContext.Response.Headers["X-Observed-Remote-Ip"] =
                            httpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
                        httpContext.Response.Headers["X-Observed-Scheme"] = httpContext.Request.Scheme;
                        await httpContext.Response.WriteAsync("ok");
                    });
                });
            })
            .StartAsync();

        var client = host.GetTestServer();
        client.BaseAddress = new Uri("http://localhost/");

        var response = await client.SendAsync(httpContext =>
        {
            httpContext.Connection.RemoteIpAddress = IPAddress.Parse(remoteAddress);
            httpContext.Request.Scheme = "http";
            if (forwardedFor is not null)
            {
                httpContext.Request.Headers["X-Forwarded-For"] = forwardedFor;
            }

            if (forwardedProto is not null)
            {
                httpContext.Request.Headers["X-Forwarded-Proto"] = forwardedProto;
            }
        });

        return (response.Response.Headers["X-Observed-Remote-Ip"].ToString(),
                response.Response.Headers["X-Observed-Scheme"].ToString());
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AppliesForwardedForFromInsideTheTrustedNetwork()
    {
        var (remoteIp, _) = await SendAsync(Subnet, Environments.Production, "10.89.0.2", "203.0.113.7");

        Assert.Equal("203.0.113.7", remoteIp);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task IgnoresForwardedForFromOutsideTheTrustedNetwork()
    {
        // The neighbouring /28 is the other application's Caddy network: close enough to be a
        // realistic mistake, and it must not be trusted.
        var (remoteIp, _) = await SendAsync(Subnet, Environments.Production, "10.89.0.20", "203.0.113.7");

        Assert.Equal("10.89.0.20", remoteIp);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AppliesForwardedProtoFromInsideTheTrustedNetwork()
    {
        // Behind a TLS-terminating proxy the request arrives as http. Without this, HTTPS
        // redirection sees http and redirects forever.
        var (_, scheme) = await SendAsync(Subnet, Environments.Production, "10.89.0.2", "203.0.113.7", "https");

        Assert.Equal("https", scheme);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task IgnoresForwardedProtoFromOutsideTheTrustedNetwork()
    {
        var (_, scheme) = await SendAsync(Subnet, Environments.Production, "10.89.0.20", "203.0.113.7", "https");

        Assert.Equal("http", scheme);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task WalksOnlyOneProxyDeep()
    {
        // Caddy appends the address it saw. An attacker who prepends entries must not have them
        // walked past: with ForwardLimit 1 the result is the last entry, which is Caddy's own view.
        var (remoteIp, _) = await SendAsync(
            Subnet, Environments.Production, "10.89.0.2", "198.51.100.9, 203.0.113.7");

        Assert.Equal("203.0.113.7", remoteIp);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task IgnoresForwardedHeadersEntirelyWhenNoProxyIsConfigured()
    {
        // Outside Production there is no proxy in front, so nothing may be trusted. Configuring the
        // middleware with empty known lists does NOT mean that: ASP.NET Core skips the
        // known-address check when both lists are empty, which accepts the header from every
        // source. This asserts the request is untouched.
        var (remoteIp, scheme) = await SendAsync(
            subnet: null, Environments.Development, "198.51.100.9", "203.0.113.7", "https");

        Assert.Equal("198.51.100.9", remoteIp);
        Assert.Equal("http", scheme);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task TreatsAnIpv4MappedProxyAddressAsInsideTheNetwork()
    {
        // Kestrel can present the peer as ::ffff:10.89.0.2 depending on the socket. The trusted
        // range is IPv4, so a mapped address must still match or Caddy stops being trusted.
        var (remoteIp, _) = await SendAsync(Subnet, Environments.Production, "::ffff:10.89.0.2", "203.0.113.7");

        Assert.Equal("203.0.113.7", remoteIp);
    }
}
