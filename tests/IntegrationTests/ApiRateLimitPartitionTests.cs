using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using SeoIntelligence.Api.Security;

namespace IntegrationTests;

/// <summary>
/// The two decisions the share rate limiter is built on: which requests it applies to, and how one
/// caller is told apart from another. Both are pure functions, so they are pinned here rather than
/// only through the end-to-end limit, which cannot distinguish "limited correctly" from "limited
/// everyone together".
/// </summary>
public sealed class ApiRateLimitPartitionTests
{
    [Fact]
    [Trait("Category", "Security")]
    public void DifferentClientAddressesAreCountedSeparately()
    {
        var first = ApiRateLimitingExtensions.ResolveClientPartitionKey(
            CreateContext(remoteIp: IPAddress.Parse("203.0.113.10")));
        var second = ApiRateLimitingExtensions.ResolveClientPartitionKey(
            CreateContext(remoteIp: IPAddress.Parse("203.0.113.11")));

        // If these collapsed into one partition, one caller exhausting the window would lock out
        // every share recipient. Behind Caddy the forwarded-headers middleware is what makes this
        // the caller's address rather than the proxy's.
        Assert.NotEqual(first, second);
    }

    [Fact]
    [Trait("Category", "Security")]
    public void TheSameClientAddressSharesOnePartition()
    {
        var address = IPAddress.Parse("203.0.113.10");

        Assert.Equal(
            ApiRateLimitingExtensions.ResolveClientPartitionKey(CreateContext(remoteIp: address)),
            ApiRateLimitingExtensions.ResolveClientPartitionKey(CreateContext(remoteIp: address)));
    }

    [Fact]
    [Trait("Category", "Security")]
    public void RequestsWithNoAddressShareOnePartitionRatherThanEscapingTheLimit()
        => Assert.Equal(
            ApiRateLimitingExtensions.ResolveClientPartitionKey(CreateContext(remoteIp: null)),
            ApiRateLimitingExtensions.ResolveClientPartitionKey(CreateContext(remoteIp: null)));

    [Theory]
    [Trait("Category", "Security")]
    [InlineData("/api/report-shares/{token}", true)]
    [InlineData("/api/report-shares/{token}/content", true)]
    [InlineData("/api/projects/{projectId}/reports/{reportId}/content", false)]
    [InlineData("/healthz", false)]
    [InlineData("/api/projects", false)]
    public void OnlyTheAnonymousShareRoutesAreLimited(string routePattern, bool expected)
        => Assert.Equal(
            expected,
            ApiRateLimitingExtensions.IsRateLimited(CreateContext(routePattern: routePattern)));

    [Fact]
    [Trait("Category", "Security")]
    public void ARequestThatMatchedNoEndpointIsNotLimited()
        => Assert.False(ApiRateLimitingExtensions.IsRateLimited(new DefaultHttpContext()));

    private static HttpContext CreateContext(IPAddress? remoteIp = null, string? routePattern = null)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = remoteIp;

        if (routePattern is not null)
        {
            context.SetEndpoint(new RouteEndpoint(
                _ => Task.CompletedTask,
                RoutePatternFactory.Parse(routePattern),
                order: 0,
                new EndpointMetadataCollection(),
                displayName: routePattern));
        }

        return context;
    }
}
