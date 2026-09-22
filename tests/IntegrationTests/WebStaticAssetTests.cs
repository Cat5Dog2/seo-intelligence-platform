using System.Net;
using System.Text.RegularExpressions;
using IntegrationTests.Support;
using Microsoft.AspNetCore.Hosting;

namespace IntegrationTests;

public sealed partial class WebStaticAssetTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task SignInPageServesItsBlazorBootScriptAsJavaScript()
    {
        await using var factory = new WebAuthenticationFactory();
        await using var staticAssetFactory = factory.WithWebHostBuilder(builder => builder.UseStaticWebAssets());
        using var client = staticAssetFactory.CreateClient(new()
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

        using var page = await client.GetAsync("/login");
        page.EnsureSuccessStatusCode();
        var html = await page.Content.ReadAsStringAsync();
        var script = Assert.Single(
            ScriptSourceRegex().Matches(html)
                .Select(match => WebUtility.HtmlDecode(match.Groups[1].Value)),
            source => source.Contains("/blazor.web", StringComparison.Ordinal));

        using var response = await client.GetAsync(script);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(response.Content.Headers.ContentType?.MediaType,
            new[] { "text/javascript", "application/javascript" });
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Blazor", body, StringComparison.Ordinal);
        Assert.DoesNotContain("<!DOCTYPE html>", body, StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex("<script[^>]+src=\"([^\"]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex ScriptSourceRegex();
}
