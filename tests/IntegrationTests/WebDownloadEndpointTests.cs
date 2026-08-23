using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using IntegrationTests.Support;
using SeoIntelligence.Application.Configuration;

namespace IntegrationTests;

/// <summary>
/// The Web host's download routes. They exist because the API requires a service key on every
/// business endpoint, which a browser cannot present: these routes authenticate the operator by
/// cookie and fetch the file with the key the Web host holds.
/// </summary>
public sealed partial class WebDownloadEndpointTests
{
    private static readonly Guid ProjectId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ExportId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ReportId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Theory]
    [Trait("Category", "Security")]
    [InlineData("exports")]
    [InlineData("reports")]
    public async Task AnonymousVisitorsAreSentToSignInRatherThanGivenTheFile(string resource)
    {
        await using var factory = new WebAuthenticationFactory();
        using var client = factory.CreateAnonymousClient();

        using var response = await client.GetAsync(BuildPath(resource));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/login", GetRedirectTarget(response), StringComparison.Ordinal);

        // The API must never have been called on behalf of someone who is not signed in.
        Assert.DoesNotContain(factory.RecordedApiCalls.Requests, request => request.Contains("/content", StringComparison.Ordinal));
    }

    [Theory]
    [Trait("Category", "Security")]
    [InlineData("exports")]
    [InlineData("reports")]
    public async Task NonAdminUsersAreRefusedTheFile(string resource)
    {
        await using var factory = new WebAuthenticationFactory();
        using var client = factory.CreateAnonymousClient();
        await factory.EnsureNonAdminUserExistsAsync();

        using (var signIn = await SignInAsync(
            client,
            WebAuthenticationFactory.UserEmail,
            WebAuthenticationFactory.UserPassword))
        {
            Assert.Equal(HttpStatusCode.Redirect, signIn.StatusCode);
        }

        factory.RecordedApiCalls.Clear();
        using var response = await client.GetAsync(BuildPath(resource));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/forbidden", GetRedirectTarget(response), StringComparison.Ordinal);
        Assert.DoesNotContain(factory.RecordedApiCalls.Requests, request => request.Contains("/content", StringComparison.Ordinal));
    }

    [Theory]
    [Trait("Category", "Integration")]
    [InlineData("exports", "keyword_metrics-export.csv", "text/csv")]
    [InlineData("reports", "monthly-2026-08-report.pdf", "application/pdf")]
    public async Task SignedInAdminsGetTheFileStreamedThroughWithItsNameAndType(
        string resource,
        string fileName,
        string contentType)
    {
        var payload = Encoding.UTF8.GetBytes("keyword,searchVolume\ncontent marketing,1200\n");

        await using var factory = new WebAuthenticationFactory();
        factory.RecordedApiCalls.Responder = request =>
        {
            if (request.RequestUri?.AbsolutePath.EndsWith("/content", StringComparison.Ordinal) != true)
            {
                return null;
            }

            // The Web host must present the service key on the API call; without it the real API
            // answers 401 and the download silently becomes an error page.
            Assert.True(request.Headers.Contains(ServiceAuthenticationOptions.HeaderName));
            Assert.Equal(
                WebAuthenticationFactory.ServiceKey,
                request.Headers.GetValues(ServiceAuthenticationOptions.HeaderName).Single());

            var content = new ByteArrayContent(payload);
            content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
            {
                FileNameStar = fileName
            };
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        };

        using var client = factory.CreateAnonymousClient();
        using (var signIn = await SignInAsync(
            client,
            WebAuthenticationFactory.AdminEmail,
            WebAuthenticationFactory.AdminPassword))
        {
            Assert.Equal(HttpStatusCode.Redirect, signIn.StatusCode);
        }

        using var response = await client.GetAsync(BuildPath(resource));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(contentType, response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal(
            fileName,
            response.Content.Headers.ContentDisposition?.FileNameStar
                ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        Assert.Equal(payload, await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task TheApiStatusCodeSurvivesSoAMissingExportStaysAMissingExport()
    {
        await using var factory = new WebAuthenticationFactory();
        factory.RecordedApiCalls.Responder = request =>
            request.RequestUri?.AbsolutePath.EndsWith("/content", StringComparison.Ordinal) == true
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent(
                        """
                        {"result":false,"data":null,"errors":[{"code":"Resource.NotFound","message":"Data export was not found."}],"meta":{}}
                        """,
                        Encoding.UTF8,
                        "application/json")
                }
                : null;

        using var client = factory.CreateAnonymousClient();
        using (var signIn = await SignInAsync(
            client,
            WebAuthenticationFactory.AdminEmail,
            WebAuthenticationFactory.AdminPassword))
        {
            Assert.Equal(HttpStatusCode.Redirect, signIn.StatusCode);
        }

        using var response = await client.GetAsync(BuildPath("exports"));

        // A 502 here would tell the operator the API is broken when the export simply is not there.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains(
            "Data export was not found.",
            await response.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);
    }

    private static string BuildPath(string resource)
        => resource switch
        {
            "exports" => $"/downloads/projects/{ProjectId:D}/exports/{ExportId:D}",
            "reports" => $"/downloads/projects/{ProjectId:D}/reports/{ReportId:D}",
            _ => throw new ArgumentOutOfRangeException(nameof(resource), resource, "Unknown download resource.")
        };

    private static async Task<HttpResponseMessage> SignInAsync(HttpClient client, string email, string password)
    {
        var token = await ReadAntiforgeryTokenAsync(client, "/login");

        return await client.PostAsync("/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["email"] = email,
            ["password"] = password,
            ["__RequestVerificationToken"] = token
        }));
    }

    private static async Task<string> ReadAntiforgeryTokenAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();

        var match = AntiforgeryTokenRegex().Match(content);
        Assert.True(match.Success, $"No antiforgery token was rendered on {path}.");
        return match.Groups[1].Value;
    }

    private static string GetRedirectTarget(HttpResponseMessage response)
    {
        var location = response.Headers.Location;
        Assert.NotNull(location);

        return location!.IsAbsoluteUri ? location.PathAndQuery : location.OriginalString;
    }

    [GeneratedRegex("""name="__RequestVerificationToken"[^>]*value="([^"]+)""")]
    private static partial Regex AntiforgeryTokenRegex();
}
