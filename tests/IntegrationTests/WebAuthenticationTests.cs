using System.Net;
using System.Text.RegularExpressions;
using IntegrationTests.Support;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SeoIntelligence.Application.Security;
using SeoIntelligence.Infrastructure.Identity;

namespace IntegrationTests;

public sealed partial class WebAuthenticationTests
{
    [Theory]
    [InlineData("/")]
    [InlineData("/dashboard")]
    [InlineData("/keywords")]
    [InlineData("/admin")]
    [InlineData("/account")]
    [InlineData("/not-found")]
    [Trait("Category", "Security")]
    public async Task ProtectedPagesRedirectAnonymousVisitorsToSignIn(string path)
    {
        await using var factory = new WebAuthenticationFactory();
        using var client = factory.CreateAnonymousClient();

        using var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/login", GetRedirectTarget(response), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task SignInPageIsReachableAnonymously()
    {
        await using var factory = new WebAuthenticationFactory();
        using var client = factory.CreateAnonymousClient();

        using var response = await client.GetAsync("/login");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("name=\"password\"", content, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Security")]
    public void SignInIsTheOnlyRoutableComponentReachableWithoutAuthentication()
    {
        var routableComponents = typeof(SeoIntelligence.Web.Components.App).Assembly
            .GetTypes()
            .Where(type => type
                .GetCustomAttributes(typeof(Microsoft.AspNetCore.Components.RouteAttribute), inherit: true)
                .Length > 0)
            .ToArray();

        Assert.NotEmpty(routableComponents);

        var anonymousComponents = routableComponents
            .Where(type => !RequiresAuthentication(type))
            .Select(type => type.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["Login"], anonymousComponents);
    }

    [Fact]
    [Trait("Category", "Security")]
    public void BusinessPagesRequireTheAdminPolicyWhileSelfServicePagesDoNot()
    {
        var assembly = typeof(SeoIntelligence.Web.Components.App).Assembly;

        Assert.Equal(
            ApplicationPolicies.RequireAdmin,
            GetAuthorizePolicy(assembly.GetType("SeoIntelligence.Web.Components.Pages.Admin")!));
        Assert.Equal(
            ApplicationPolicies.RequireAdmin,
            GetAuthorizePolicy(assembly.GetType("SeoIntelligence.Web.Components.Pages.Dashboard")!));

        // Changing your own password and seeing the access-denied page must not need the Admin
        // policy, otherwise a non-Admin account could never recover or sign itself out.
        Assert.Null(GetAuthorizePolicy(assembly.GetType("SeoIntelligence.Web.Components.Pages.Account")!));
        Assert.Null(GetAuthorizePolicy(assembly.GetType("SeoIntelligence.Web.Components.Pages.Forbidden")!));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AdministratorCanSignInAndReachProtectedPages()
    {
        await using var factory = new WebAuthenticationFactory();
        using var client = factory.CreateAnonymousClient();

        var signIn = await SignInAsync(
            client,
            WebAuthenticationFactory.AdminEmail,
            WebAuthenticationFactory.AdminPassword);

        Assert.Equal(HttpStatusCode.Redirect, signIn.StatusCode);
        Assert.Equal("/", GetRedirectTarget(signIn));

        using var dashboard = await client.GetAsync("/dashboard");
        Assert.Equal(HttpStatusCode.OK, dashboard.StatusCode);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SignOutClearsTheSession()
    {
        await using var factory = new WebAuthenticationFactory();
        using var client = factory.CreateAnonymousClient();

        using (var signIn = await SignInAsync(
            client,
            WebAuthenticationFactory.AdminEmail,
            WebAuthenticationFactory.AdminPassword))
        {
            Assert.Equal(HttpStatusCode.Redirect, signIn.StatusCode);
        }

        var token = await ReadAntiforgeryTokenAsync(client, "/account");
        using var signOut = await PostFormAsync(client, "/logout", new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token
        });

        Assert.Equal(HttpStatusCode.Redirect, signOut.StatusCode);
        Assert.Equal("/login", GetRedirectTarget(signOut));

        using var afterSignOut = await client.GetAsync("/dashboard");
        Assert.Equal(HttpStatusCode.Redirect, afterSignOut.StatusCode);
        Assert.StartsWith("/login", GetRedirectTarget(afterSignOut), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("wrong-address@localhost", WebAuthenticationFactory.AdminPassword)]
    [InlineData(WebAuthenticationFactory.AdminEmail, "TotallyWrong!Passw0rd")]
    [Trait("Category", "Security")]
    public async Task UnknownAccountsAndWrongPasswordsShareTheSameFailureMessage(string email, string password)
    {
        await using var factory = new WebAuthenticationFactory();
        using var client = factory.CreateAnonymousClient();

        using var response = await SignInAsync(client, email, password);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/login?loginError=invalid", GetRedirectTarget(response));
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task SignInWithoutAnAntiforgeryTokenIsRejected()
    {
        await using var factory = new WebAuthenticationFactory();
        using var client = factory.CreateAnonymousClient();

        // Loading the page first mirrors a cross-site post from a browser that already holds the
        // antiforgery cookie but cannot read the token out of the page.
        await ReadAntiforgeryTokenAsync(client, "/login");

        using var response = await PostFormAsync(client, "/login", new Dictionary<string, string>
        {
            ["email"] = WebAuthenticationFactory.AdminEmail,
            ["password"] = WebAuthenticationFactory.AdminPassword
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task SignInWithATamperedAntiforgeryTokenIsRejected()
    {
        await using var factory = new WebAuthenticationFactory();
        using var client = factory.CreateAnonymousClient();

        var token = await ReadAntiforgeryTokenAsync(client, "/login");

        using var response = await PostFormAsync(client, "/login", new Dictionary<string, string>
        {
            ["email"] = WebAuthenticationFactory.AdminEmail,
            ["password"] = WebAuthenticationFactory.AdminPassword,
            ["__RequestVerificationToken"] = token[..^4] + "AAAA"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task RepeatedFailuresLockTheAccountEvenForTheCorrectPassword()
    {
        await using var factory = new WebAuthenticationFactory();
        using var client = factory.CreateAnonymousClient();

        for (var attempt = 0; attempt < SeoIntelligenceIdentityOptions.MaximumFailedAccessAttempts; attempt++)
        {
            using var failure = await SignInAsync(
                client,
                WebAuthenticationFactory.AdminEmail,
                "TotallyWrong!Passw0rd");
            Assert.Equal(HttpStatusCode.Redirect, failure.StatusCode);
        }

        using var response = await SignInAsync(
            client,
            WebAuthenticationFactory.AdminEmail,
            WebAuthenticationFactory.AdminPassword);

        Assert.Equal("/login?loginError=lockout", GetRedirectTarget(response));
    }

    [Theory]
    [InlineData("https://evil.example.com/dashboard")]
    [InlineData("//evil.example.com")]
    [Trait("Category", "Security")]
    public async Task SignInIgnoresReturnUrlsThatPointOffSite(string returnUrl)
    {
        await using var factory = new WebAuthenticationFactory();
        using var client = factory.CreateAnonymousClient();

        using var response = await SignInAsync(
            client,
            WebAuthenticationFactory.AdminEmail,
            WebAuthenticationFactory.AdminPassword,
            returnUrl);

        Assert.Equal("/", GetRedirectTarget(response));
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task StartupFailsWhenNoAdminExistsAndNoSeedCredentialsAreConfigured()
    {
        await using var factory = WebAuthenticationFactory.WithoutAdminSeed();

        // The failure surfaces when the host is first built, which the factory defers until a
        // client is created. The host wraps startup failures, so the whole chain is searched.
        var exception = Record.Exception(() => factory.CreateAnonymousClient());

        Assert.NotNull(exception);
        Assert.Contains("AdminSeed", DescribeExceptionChain(exception!), StringComparison.Ordinal);
    }

    private static string DescribeExceptionChain(Exception exception)
    {
        var messages = new List<string>();
        for (var current = exception; current is not null; current = current.InnerException)
        {
            messages.Add(current.Message);
        }

        return string.Join(" | ", messages);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task RestartingWithClearedSeedCredentialsSucceedsOnceAnAdminExists()
    {
        await using var firstStart = new WebAuthenticationFactory();
        using (var client = firstStart.CreateAnonymousClient())
        {
            using var signIn = await SignInAsync(
                client,
                WebAuthenticationFactory.AdminEmail,
                WebAuthenticationFactory.AdminPassword);
            Assert.Equal(HttpStatusCode.Redirect, signIn.StatusCode);
        }

        // The operator clears AdminSeed after the first sign-in; the same database must still boot.
        await using var restart = WebAuthenticationFactory.RestartWithoutAdminSeed(firstStart);
        using var restartedClient = restart.CreateAnonymousClient();

        using var loginPage = await restartedClient.GetAsync("/login");
        Assert.Equal(HttpStatusCode.OK, loginPage.StatusCode);

        using var signInAfterRestart = await SignInAsync(
            restartedClient,
            WebAuthenticationFactory.AdminEmail,
            WebAuthenticationFactory.AdminPassword);
        Assert.Equal("/", GetRedirectTarget(signInAfterRestart));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SeedingRecoversWhenTheAccountExistsWithoutTheAdminRole()
    {
        await using var factory = new WebAuthenticationFactory();
        using var client = factory.CreateAnonymousClient();

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var seeder = scope.ServiceProvider.GetRequiredService<IIdentityDataSeeder>();

        // Reproduces a previous startup that created the account and then failed before the role
        // was assigned.
        var admin = await userManager.FindByEmailAsync(WebAuthenticationFactory.AdminEmail);
        Assert.NotNull(admin);
        await userManager.RemoveFromRoleAsync(admin!, ApplicationRoles.Admin);
        Assert.Empty(await userManager.GetUsersInRoleAsync(ApplicationRoles.Admin));

        await seeder.SeedAsync();

        var admins = await userManager.GetUsersInRoleAsync(ApplicationRoles.Admin);
        Assert.Single(admins);
        Assert.Equal(WebAuthenticationFactory.AdminEmail, admins[0].Email);
    }

    /// <summary>
    /// The handler resolves Location against the base address, so tests compare the site-relative
    /// part rather than the absolute URI.
    /// </summary>
    private static string GetRedirectTarget(HttpResponseMessage response)
    {
        var location = response.Headers.Location;
        Assert.NotNull(location);

        if (!location!.IsAbsoluteUri)
        {
            return location.OriginalString;
        }

        return location.PathAndQuery;
    }

    private static bool RequiresAuthentication(Type componentType)
    {
        var hasAuthorize = componentType
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), inherit: true)
            .Length > 0;
        var hasAllowAnonymous = componentType
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute), inherit: true)
            .Length > 0;

        return hasAuthorize && !hasAllowAnonymous;
    }

    private static string? GetAuthorizePolicy(Type componentType)
        => componentType
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), inherit: true)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()
            .Single()
            .Policy;

    private static async Task<HttpResponseMessage> SignInAsync(
        HttpClient client,
        string email,
        string password,
        string? returnUrl = null)
    {
        var token = await ReadAntiforgeryTokenAsync(client, "/login");
        var form = new Dictionary<string, string>
        {
            ["email"] = email,
            ["password"] = password,
            ["__RequestVerificationToken"] = token
        };

        if (returnUrl is not null)
        {
            form["returnUrl"] = returnUrl;
        }

        return await PostFormAsync(client, "/login", form);
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

    [GeneratedRegex("""name="__RequestVerificationToken"[^>]*value="([^"]+)""")]
    private static partial Regex AntiforgeryTokenRegex();
}
