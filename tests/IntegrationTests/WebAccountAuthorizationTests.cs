using System.Net;
using System.Text.RegularExpressions;
using IntegrationTests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SeoIntelligence.Application.Security;
using SeoIntelligence.Infrastructure.Identity;

namespace IntegrationTests;

public sealed partial class WebAccountAuthorizationTests
{
    [Fact]
    [Trait("Category", "Security")]
    public void EveryBusinessPageRequiresTheAdminPolicy()
    {
        // Self-service and error pages must stay reachable by any signed-in account; everything
        // else renders business data through the shared layout and needs the Admin policy.
        var selfServicePages = new[] { "Account", "Forbidden", "Error" };

        var pagesMissingTheAdminPolicy = typeof(SeoIntelligence.Web.Components.App).Assembly
            .GetTypes()
            .Where(type => type.GetCustomAttributes(typeof(RouteAttribute), inherit: true).Length > 0)
            .Where(type => type.Name != "Login" && !selfServicePages.Contains(type.Name))
            .Where(type => GetAuthorizePolicy(type) != ApplicationPolicies.RequireAdmin)
            .Select(type => type.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(pagesMissingTheAdminPolicy);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task NonAdminUsersAreSentToForbiddenRatherThanBackToSignIn()
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
            Assert.Equal("/", GetRedirectTarget(signIn));
        }

        using var dashboard = await client.GetAsync("/dashboard");

        // On a full page load the endpoint authorization middleware answers first and uses the
        // cookie handler's AccessDeniedPath, which appends the original path as ReturnUrl.
        // In-circuit navigation is handled by RedirectToSignInOrForbidden instead.
        Assert.Equal(HttpStatusCode.Redirect, dashboard.StatusCode);
        Assert.StartsWith("/forbidden", GetRedirectTarget(dashboard), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task NonAdminUsersCanStillReachTheirOwnAccountAndSignOut()
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

        using (var account = await client.GetAsync("/account"))
        {
            Assert.Equal(HttpStatusCode.OK, account.StatusCode);
        }

        var token = await ReadAntiforgeryTokenAsync(client, "/account");
        using var signOut = await PostFormAsync(client, "/logout", new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token
        });

        Assert.Equal(HttpStatusCode.Redirect, signOut.StatusCode);
        Assert.Equal("/login", GetRedirectTarget(signOut));
    }

    [Theory]
    [InlineData("/account")]
    [InlineData("/forbidden")]
    [InlineData("/Error")]
    [Trait("Category", "Security")]
    public async Task SelfServicePagesRenderNoBusinessComponentsAndCallNoBusinessApi(string path)
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

        using var response = await client.GetAsync(path);
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // The shared layout's business components read and write project data through the service
        // key, which the API cannot attribute to a role. None of them may render here.
        Assert.DoesNotContain("project-switcher", content, StringComparison.Ordinal);
        Assert.DoesNotContain("credit-badge", content, StringComparison.Ordinal);
        Assert.DoesNotContain("location-language-selector", content, StringComparison.Ordinal);
        Assert.DoesNotContain("side-nav", content, StringComparison.Ordinal);

        Assert.Empty(factory.RecordedApiCalls.Requests);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task ForbiddenOffersNonAdminUsersOnlyLinksTheyCanActuallyOpen()
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

        using var forbidden = await client.GetAsync("/forbidden");
        var content = await forbidden.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, forbidden.StatusCode);

        // The project list requires the Admin policy, so either of these pointing at "/" would
        // send the visitor straight back to this page. Each link is checked by its own test id:
        // asserting over all anchors would let one correct link mask a wrong one.
        Assert.Equal("/account", GetAnchorHrefByTestId(content, "self-service-brand-link"));
        Assert.Equal("/account", GetAnchorHrefByTestId(content, "forbidden-back-link"));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ForbiddenLinksAdminsBackToTheProjectList()
    {
        await using var factory = new WebAuthenticationFactory();
        using var client = factory.CreateAnonymousClient();

        await SignInAndDisposeAsync(client, WebAuthenticationFactory.AdminPassword);

        using var forbidden = await client.GetAsync("/forbidden");
        var content = await forbidden.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, forbidden.StatusCode);
        Assert.Equal("/", GetAnchorHrefByTestId(content, "self-service-brand-link"));
        Assert.Equal("/", GetAnchorHrefByTestId(content, "forbidden-back-link"));
    }

    private static string GetAnchorHrefByTestId(string html, string testId)
    {
        var tag = Regex.Match(
            html,
            $"<a\\b[^>]*data-testid=\"{Regex.Escape(testId)}\"[^>]*>",
            RegexOptions.IgnoreCase);
        Assert.True(tag.Success, $"No anchor with data-testid=\"{testId}\" was rendered.");

        var href = Regex.Match(tag.Value, "\\bhref=\"([^\"]*)\"", RegexOptions.IgnoreCase);
        Assert.True(href.Success, $"The anchor with data-testid=\"{testId}\" has no href.");

        return href.Groups[1].Value;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ChangingThePasswordSucceedsAndTheNewPasswordWorks()
    {
        const string newPassword = "RotatedWebTests!1";

        await using var factory = new WebAuthenticationFactory();
        using var client = factory.CreateAnonymousClient();

        await SignInAndDisposeAsync(client, WebAuthenticationFactory.AdminPassword);

        using var changed = await ChangePasswordAsync(
            client,
            WebAuthenticationFactory.AdminPassword,
            newPassword,
            newPassword);

        Assert.Equal(HttpStatusCode.Redirect, changed.StatusCode);
        Assert.Equal("/account?passwordChanged=true", GetRedirectTarget(changed));

        using var freshClient = factory.CreateAnonymousClient();
        using var signInWithNewPassword = await SignInAsync(
            freshClient,
            WebAuthenticationFactory.AdminEmail,
            newPassword);

        Assert.Equal("/", GetRedirectTarget(signInWithNewPassword));

        using var signInWithOldPassword = await SignInAsync(
            factory.CreateAnonymousClient(),
            WebAuthenticationFactory.AdminEmail,
            WebAuthenticationFactory.AdminPassword);

        Assert.Equal("/login?loginError=invalid", GetRedirectTarget(signInWithOldPassword));
    }

    [Theory]
    [InlineData("WrongCurrent!Pw1", "NewValidPassword!1", "NewValidPassword!1", "current")]
    [InlineData(WebAuthenticationFactory.AdminPassword, "NewValidPassword!1", "Mismatched!Pw1", "confirm")]
    [InlineData(WebAuthenticationFactory.AdminPassword, "short1!A", "short1!A", "new")]
    [InlineData(WebAuthenticationFactory.AdminPassword, "nouppercase!123", "nouppercase!123", "new")]
    [InlineData(
        WebAuthenticationFactory.AdminPassword,
        WebAuthenticationFactory.AdminPassword,
        WebAuthenticationFactory.AdminPassword,
        "same")]
    [Trait("Category", "Security")]
    public async Task ChangingThePasswordReportsTheSpecificValidationFailure(
        string currentPassword,
        string newPassword,
        string confirmPassword,
        string expectedError)
    {
        await using var factory = new WebAuthenticationFactory();
        using var client = factory.CreateAnonymousClient();

        await SignInAndDisposeAsync(client, WebAuthenticationFactory.AdminPassword);

        using var response = await ChangePasswordAsync(client, currentPassword, newPassword, confirmPassword);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal($"/account?passwordError={expectedError}", GetRedirectTarget(response));

        // The stored password must be unchanged after every rejected attempt.
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var admin = await userManager.FindByEmailAsync(WebAuthenticationFactory.AdminEmail);
        Assert.NotNull(admin);
        Assert.True(await userManager.CheckPasswordAsync(admin!, WebAuthenticationFactory.AdminPassword));
    }

    [Theory]
    [InlineData("/logout")]
    [InlineData("/account/password")]
    [Trait("Category", "Security")]
    public async Task AccountEndpointsRejectPostsWithoutAnAntiforgeryToken(string path)
    {
        await using var factory = new WebAuthenticationFactory();
        using var client = factory.CreateAnonymousClient();

        await SignInAndDisposeAsync(client, WebAuthenticationFactory.AdminPassword);

        // The page load leaves the antiforgery cookie in place, mirroring a cross-site post from a
        // browser that cannot read the token out of the page.
        await ReadAntiforgeryTokenAsync(client, "/account");

        using var response = await PostFormAsync(client, path, BuildFormWithoutToken(path));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("/logout")]
    [InlineData("/account/password")]
    [Trait("Category", "Security")]
    public async Task AccountEndpointsRejectPostsWithATamperedAntiforgeryToken(string path)
    {
        await using var factory = new WebAuthenticationFactory();
        using var client = factory.CreateAnonymousClient();

        await SignInAndDisposeAsync(client, WebAuthenticationFactory.AdminPassword);

        var token = await ReadAntiforgeryTokenAsync(client, "/account");
        var form = BuildFormWithoutToken(path);
        form["__RequestVerificationToken"] = token[..^4] + "AAAA";

        using var response = await PostFormAsync(client, path, form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static Dictionary<string, string> BuildFormWithoutToken(string path)
        => path == "/account/password"
            ? new Dictionary<string, string>
            {
                ["currentPassword"] = WebAuthenticationFactory.AdminPassword,
                ["newPassword"] = "AttackerChosen!Pw1",
                ["confirmNewPassword"] = "AttackerChosen!Pw1"
            }
            : [];

    private static async Task SignInAndDisposeAsync(HttpClient client, string password)
    {
        using var signIn = await SignInAsync(client, WebAuthenticationFactory.AdminEmail, password);
        Assert.Equal(HttpStatusCode.Redirect, signIn.StatusCode);
    }

    private static async Task<HttpResponseMessage> ChangePasswordAsync(
        HttpClient client,
        string currentPassword,
        string newPassword,
        string confirmPassword)
    {
        var token = await ReadAntiforgeryTokenAsync(client, "/account");

        return await PostFormAsync(client, "/account/password", new Dictionary<string, string>
        {
            ["currentPassword"] = currentPassword,
            ["newPassword"] = newPassword,
            ["confirmNewPassword"] = confirmPassword,
            ["__RequestVerificationToken"] = token
        });
    }

    private static async Task<HttpResponseMessage> SignInAsync(HttpClient client, string email, string password)
    {
        var token = await ReadAntiforgeryTokenAsync(client, "/login");

        return await PostFormAsync(client, "/login", new Dictionary<string, string>
        {
            ["email"] = email,
            ["password"] = password,
            ["__RequestVerificationToken"] = token
        });
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

    private static Task<HttpResponseMessage> PostFormAsync(
        HttpClient client,
        string path,
        Dictionary<string, string> form)
        => client.PostAsync(path, new FormUrlEncodedContent(form));

    private static string GetRedirectTarget(HttpResponseMessage response)
    {
        var location = response.Headers.Location;
        Assert.NotNull(location);

        return location!.IsAbsoluteUri ? location.PathAndQuery : location.OriginalString;
    }

    private static string? GetAuthorizePolicy(Type componentType)
        => componentType
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), inherit: true)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()
            .SingleOrDefault()
            ?.Policy;

    [GeneratedRegex("""name="__RequestVerificationToken"[^>]*value="([^"]+)""")]
    private static partial Regex AntiforgeryTokenRegex();

}
