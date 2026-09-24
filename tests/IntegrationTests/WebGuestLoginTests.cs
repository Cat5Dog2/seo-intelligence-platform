using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
using SeoIntelligence.Application.Services;
using SeoIntelligence.Contracts.Api;

namespace IntegrationTests;

public sealed partial class WebGuestLoginTests
{
    [Theory]
    [Trait("Category", "UI")]
    [InlineData("/briefs", "生成ジョブ登録")]
    [InlineData("/competitors", "登録</button>")]
    [InlineData("/content-analysis", "分析を開始")]
    [InlineData("/clusters", "クラスターを生成")]
    [InlineData("/rank-monitoring", "順位チェック登録")]
    [InlineData("/rewrite", "カニバリ再計算")]
    [InlineData("/ai-assistant", "送信</button>")]
    public async Task AdministratorKeepsBusinessActionsAfterGuestUiRestrictions(string path, string action)
    {
        await using var factory = new WebAuthenticationFactory();
        var project = new ProjectDetails(Guid.NewGuid(), Guid.NewGuid(), "UI regression", "Japan", "Japanese",
            JsonSerializer.SerializeToElement(new { }), null, "active", DateTime.UtcNow, DateTime.UtcNow, null);
        factory.RecordedApiCalls.Responder = request => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = request.RequestUri!.AbsolutePath switch
            {
                "/api/projects" => JsonContent.Create(ApiResponseEnvelope<ProjectDetails[]>.Success("projects", [project])),
                var route when route.EndsWith("/rank-results", StringComparison.Ordinal)
                    => JsonContent.Create(ApiResponseEnvelope<RankResultList>.Success("ranks", new([], new(0, 0, 0, 0, 0, 0), 1, 100, 0, 0))),
                _ => JsonContent.Create(ApiResponseEnvelope<object[]>.Success("empty-list", []))
            }
        };
        using var client = factory.CreateAnonymousClient();
        var token = await ReadTokenAsync(client, "/login");
        using var signIn = await client.PostAsync("/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["email"] = WebAuthenticationFactory.AdminEmail,
            ["password"] = WebAuthenticationFactory.AdminPassword
        }));
        Assert.Equal(HttpStatusCode.Redirect, signIn.StatusCode);
        using var response = await client.GetAsync(path);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(action, html);
        Assert.DoesNotContain("サンプル閲覧専用", html);
        Assert.DoesNotContain("role=\"alert\"", html);
    }

    [Theory]
    [Trait("Category", "UI")]
    [InlineData("/briefs", "生成ジョブ登録")]
    [InlineData("/competitors", "競合抽出")]
    [InlineData("/influx", "分析ジョブ登録")]
    [InlineData("/content-analysis", "ブリーフ生成")]
    [InlineData("/clusters", "ブリーフ作成")]
    [InlineData("/rank-monitoring", "順位チェック登録")]
    [InlineData("/rewrite", "カニバリ再計算")]
    [InlineData("/ai-assistant", "送信</button>")]
    public async Task GuestReadOnlyPagesExplainCapabilitiesAndHideUnsupportedActions(string path, string action)
    {
        await using var factory = new WebAuthenticationFactory();
        using var client = factory.CreateAnonymousClient();
        using var signIn = await SignInAsync(client);
        using var response = await client.GetAsync(path);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("サンプル閲覧専用", html);
        Assert.DoesNotContain(action, html);
        Assert.DoesNotContain("APIクレジットを消費", html);
        Assert.DoesNotContain("Guest.Unsupported", html);
        if (path == "/briefs") Assert.Contains("第1版", html);
        Assert.Empty(factory.RecordedApiCalls.Requests);
    }

    [Fact]
    [Trait("Category", "UI")]
    public async Task AdministratorReportsKeepGenerationControlsAndLoadNotificationHistory()
    {
        await using var factory = new WebAuthenticationFactory();
        var project = new ProjectDetails(Guid.NewGuid(), Guid.NewGuid(), "Reports regression", "Japan", "Japanese",
            JsonSerializer.SerializeToElement(new { }), null, "active", DateTime.UtcNow, DateTime.UtcNow, null);
        factory.RecordedApiCalls.Responder = request => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = request.RequestUri!.AbsolutePath == "/api/projects"
                ? JsonContent.Create(ApiResponseEnvelope<ProjectDetails[]>.Success("projects", [project]))
                : JsonContent.Create(ApiResponseEnvelope<object[]>.Success("empty-list", []))
        };
        using var client = factory.CreateAnonymousClient();
        var token = await ReadTokenAsync(client, "/login");
        using var signIn = await client.PostAsync("/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["email"] = WebAuthenticationFactory.AdminEmail,
            ["password"] = WebAuthenticationFactory.AdminPassword
        }));
        Assert.Equal(HttpStatusCode.Redirect, signIn.StatusCode);

        using var response = await client.GetAsync("/reports");
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("data-testid=\"report-create-button\"", html);
        Assert.Contains("通知履歴更新", html);
        Assert.DoesNotContain("ゲストデモの利用範囲", html);
        Assert.DoesNotContain("role=\"alert\"", html);
        Assert.Contains("GET /api/admin/notification-deliveries", factory.RecordedApiCalls.Requests);
    }

    [Fact]
    [Trait("Category", "UI")]
    public async Task GuestReportsExplainTheLimitWithoutAnErrorOrRealApiCall()
    {
        await using var factory = new WebAuthenticationFactory();
        using var client = factory.CreateAnonymousClient();
        using var signIn = await SignInAsync(client);

        using var response = await client.GetAsync("/reports");
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("ゲストデモではレポート生成・共有・通知履歴は利用できません。", html);
        Assert.DoesNotContain("role=\"alert\"", html);
        Assert.DoesNotContain("data-testid=\"report-create-button\"", html);
        Assert.DoesNotContain("通知履歴更新", html);
        Assert.Empty(factory.RecordedApiCalls.Requests);
    }

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
