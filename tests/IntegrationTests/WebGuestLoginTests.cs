using System.Net;
using System.Text.RegularExpressions;
using IntegrationTests.Support;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SeoIntelligence.Web.Services;
using SeoIntelligence.Web.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using SeoIntelligence.Infrastructure.Identity;

namespace IntegrationTests;

public sealed partial class WebGuestLoginTests
{
    [Fact]
    [Trait("Category", "Security")]
    public async Task AdministratorSignInRevokesThePreviousGuestSession()
    {
        await using var factory = new WebAuthenticationFactory();
        using var client = factory.CreateAnonymousClient();
        using var guest = await SignInAsync(client);
        var options = factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(IdentityConstants.ApplicationScheme);
        var header = guest.Headers.GetValues("Set-Cookie").Last(value => value.StartsWith(options.Cookie.Name + "=", StringComparison.Ordinal));
        var cookie = header.Split(';')[0];
        var ticket = options.TicketDataFormat.Unprotect(Uri.UnescapeDataString(cookie[(cookie.IndexOf('=') + 1)..]))!;
        var token = await ReadTokenAsync(client, "/login");
        using var admin = await client.PostAsync("/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["email"] = WebAuthenticationFactory.AdminEmail,
            ["password"] = WebAuthenticationFactory.AdminPassword
        }));
        Assert.Equal("/", admin.Headers.Location?.OriginalString);
        Assert.Null(factory.Services.GetRequiredService<GuestDemoSessionStore>().Find(ticket.Principal));
        using var account = await client.GetAsync("/account");
        Assert.Contains("action=\"/account/password\"", await account.Content.ReadAsStringAsync());
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task AllGuestPagesRenderWithoutCallingTheApiInRealMode()
    {
        await using var factory = new WebAuthenticationFactory().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("RakkoKeyword:Mode", "Real");
            builder.ConfigureServices(services => services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.Zero));
        });
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false
        });
        using var signIn = await SignInAsync(client, "/dashboard");
        Assert.Equal("/dashboard", signIn.Headers.Location?.OriginalString);
        foreach (var path in new[] { "/", "/dashboard", "/keywords", "/search-volume", "/competitors", "/influx", "/content-analysis", "/clusters", "/briefs", "/rewrite", "/rank-monitoring", "/reports", "/ai-assistant" })
        {
            using var page = await client.GetAsync(path);
            var html = WebUtility.HtmlDecode(await page.Content.ReadAsStringAsync());
            Assert.Equal(HttpStatusCode.OK, page.StatusCode);
            Assert.Contains("ゲスト・Mock", html);
            Assert.DoesNotContain("Guest.NotFound", html);
            Assert.DoesNotContain("Guest.Unsupported", html);
            Assert.DoesNotContain("href=\"/admin\"", html);
        }
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task GuestCookieIsNonPersistentMockOnlyAndCannotBeReusedAfterLogout()
    {
        await using var factory = new WebAuthenticationFactory();
        using var client = factory.CreateAnonymousClient();
        using var signIn = await SignInAsync(client);
        var cookies = factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(IdentityConstants.ApplicationScheme);
        var header = signIn.Headers.GetValues("Set-Cookie").Last(value => value.StartsWith(cookies.Cookie.Name + "=", StringComparison.Ordinal));
        Assert.Contains("secure", header, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", header, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("expires=", header, StringComparison.OrdinalIgnoreCase);
        var cookie = header.Split(';')[0];
        var ticket = cookies.TicketDataFormat.Unprotect(Uri.UnescapeDataString(cookie[(cookie.IndexOf('=') + 1)..]));
        Assert.NotNull(ticket);
        Assert.True(GuestAuthentication.IsGuest(ticket.Principal));
        Assert.False(ticket.Principal.IsInRole("Admin"));
        Assert.False(ticket.Properties.AllowRefresh);
        Assert.InRange(ticket.Properties.ExpiresUtc!.Value - ticket.Properties.IssuedUtc!.Value, TimeSpan.FromMinutes(59), TimeSpan.FromMinutes(61));
        var token = await ReadTokenAsync(client, "/account");
        using var signOut = await client.PostAsync("/logout", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));
        using var oldCookieClient = factory.CreateAnonymousClient();
        oldCookieClient.DefaultRequestHeaders.Add("Cookie", cookie);
        using var replay = await oldCookieClient.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, replay.StatusCode);
        Assert.Contains("/login", replay.Headers.Location?.ToString());
        Assert.Null(factory.Services.GetRequiredService<GuestDemoSessionStore>().Find(ticket.Principal));
    }
    [Fact]
    [Trait("Category", "Security")]
    public async Task GuestSignsInWithoutAnAccountAndUsesOnlyDemoData()
    {
        await using var factory = new WebAuthenticationFactory();
        using var client = factory.CreateAnonymousClient();
        var token = await ReadTokenAsync(client, "/login");

        using var signIn = await client.PostAsync("/login/guest", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["returnUrl"] = "/"
        }));

        Assert.Equal(HttpStatusCode.Redirect, signIn.StatusCode);
        Assert.Equal("/", signIn.Headers.Location?.OriginalString);
        using var home = await client.GetAsync("/");
        var html = WebUtility.HtmlDecode(await home.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, home.StatusCode);
        Assert.Contains("ゲスト", html);
        Assert.Contains("Mock", html);
        Assert.Contains("デモプロジェクト", html);
        Assert.Empty(factory.RecordedApiCalls.Requests);

        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.Single(users.Users);
    }

    [Theory]
    [InlineData("")]
    [InlineData("tampered-token")]
    [Trait("Category", "Security")]
    public async Task GuestSignInRejectsMissingOrInvalidCsrfToken(string token)
    {
        await using var factory = new WebAuthenticationFactory();
        using var client = factory.CreateAnonymousClient();
        await ReadTokenAsync(client, "/login");

        using var response = await client.PostAsync("/login/guest", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token
        }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var home = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, home.StatusCode);
        Assert.Contains("/login", home.Headers.Location?.ToString());
    }

    [Theory]
    [InlineData("https://example.org")]
    [InlineData("//example.org")]
    [InlineData("/\\example.org")]
    [Trait("Category", "Security")]
    public async Task GuestSignInRejectsExternalReturnUrls(string returnUrl)
    {
        await using var factory = new WebAuthenticationFactory();
        using var client = factory.CreateAnonymousClient();
        using var response = await SignInAsync(client, returnUrl);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.OriginalString);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task GuestCannotManageAccountsOrAdministrationAndCanSignOut()
    {
        await using var factory = new WebAuthenticationFactory();
        using var client = factory.CreateAnonymousClient();
        using var signIn = await SignInAsync(client);
        Assert.Equal(HttpStatusCode.Redirect, signIn.StatusCode);

        using var account = await client.GetAsync("/account");
        Assert.Equal(HttpStatusCode.OK, account.StatusCode);
        Assert.DoesNotContain("action=\"/account/password\"", await account.Content.ReadAsStringAsync());
        using var admin = await client.GetAsync("/admin");
        Assert.Equal(HttpStatusCode.Redirect, admin.StatusCode);
        Assert.Contains("/forbidden", admin.Headers.Location?.ToString());

        var token = await ReadTokenAsync(client, "/account");
        using var password = await client.PostAsync("/account/password", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["currentPassword"] = "unused",
            ["newPassword"] = "unused",
            ["confirmNewPassword"] = "unused"
        }));
        Assert.Contains(password.StatusCode, new[] { HttpStatusCode.Forbidden, HttpStatusCode.Redirect });
        if (password.StatusCode == HttpStatusCode.Redirect)
        {
            Assert.Contains("/forbidden", password.Headers.Location?.ToString());
        }

        using var signOut = await client.PostAsync("/logout", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token
        }));
        Assert.Equal(HttpStatusCode.Redirect, signOut.StatusCode);
        using var home = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, home.StatusCode);
        Assert.Contains("/login", home.Headers.Location?.ToString());
        Assert.Empty(factory.RecordedApiCalls.Requests);
    }

    internal static async Task<HttpResponseMessage> SignInAsync(HttpClient client, string returnUrl = "/")
    {
        var token = await ReadTokenAsync(client, "/login");
        return await client.PostAsync("/login/guest", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["returnUrl"] = returnUrl
        }));
    }

    internal static async Task<string> ReadTokenAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        response.EnsureSuccessStatusCode();
        var match = TokenRegex().Match(await response.Content.ReadAsStringAsync());
        Assert.True(match.Success, $"Missing CSRF token on {path}.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    [GeneratedRegex("""name="__RequestVerificationToken"[^>]*value="([^"]+)""" )]
    private static partial Regex TokenRegex();
}
