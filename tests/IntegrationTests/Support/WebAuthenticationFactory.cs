using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SeoIntelligence.Application.Configuration;
using SeoIntelligence.Application.Security;
using SeoIntelligence.Infrastructure.Identity;
using SeoIntelligence.Infrastructure.Persistence;
using SeoIntelligence.Web.Components;
using SeoIntelligence.Web.Services;

namespace IntegrationTests.Support;

/// <summary>
/// Hosts the Web application for authentication tests. <see cref="App"/> is only used to locate the
/// Web assembly. Identity needs a real store, so the PostgreSQL context is swapped for a
/// per-instance in-memory one; everything else (Identity options, cookie settings, endpoints, the
/// seeder) runs unchanged.
/// </summary>
public sealed class WebAuthenticationFactory : WebApplicationFactory<App>
{
    public const string ServiceKey = "web-integration-test-service-key";
    public const string AdminEmail = "web-tests-admin@localhost";
    public const string AdminPassword = "WebTests!Passw0rd";
    public const string UserEmail = "web-tests-user@localhost";
    public const string UserPassword = "WebTests!UserPw1";

    private readonly string _databaseName;
    private readonly bool _configureAdminSeed;

    public WebAuthenticationFactory()
        : this($"web-auth-tests-{Guid.NewGuid():N}", configureAdminSeed: true)
    {
    }

    private WebAuthenticationFactory(string databaseName, bool configureAdminSeed)
    {
        _databaseName = databaseName;
        _configureAdminSeed = configureAdminSeed;
    }

    /// <summary>The in-memory database this host uses, so a second host can reuse the same data.</summary>
    public string DatabaseName => _databaseName;

    /// <summary>
    /// Builds a factory with no <c>AdminSeed</c> values, so a caller can assert that starting
    /// without any Admin account fails instead of coming up unusable.
    /// </summary>
    public static WebAuthenticationFactory WithoutAdminSeed()
        => new($"web-auth-tests-{Guid.NewGuid():N}", configureAdminSeed: false);

    /// <summary>
    /// Builds a factory with no <c>AdminSeed</c> values that reuses an existing database. This is
    /// the production restart case: once an Admin exists the operator clears the seed credentials,
    /// and every later start must still succeed.
    /// </summary>
    public static WebAuthenticationFactory RestartWithoutAdminSeed(WebAuthenticationFactory original)
        => new(original.DatabaseName, configureAdminSeed: false);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // UseSetting writes host configuration, which minimal hosting exposes on
        // builder.Configuration before Program.cs registers services. ConfigureAppConfiguration
        // would land too late: the Identity registration reads configuration as it runs.
        // A connection string must be present for that registration to succeed; the provider is
        // replaced below so nothing ever reaches PostgreSQL.
        builder.UseSetting("ConnectionStrings:Default", "Host=unused;Database=unused;Username=unused;Password=unused");
        builder.UseSetting("Database:Host", string.Empty);
        builder.UseSetting("SecretStore:Provider", "Configuration");
        builder.UseSetting("SecretStore:ConfigurationPrefix", "Secrets");
        builder.UseSetting("ServiceAuthentication:ServiceKeyRef", ServiceAuthenticationOptions.DefaultServiceKeyRef);
        builder.UseSetting($"Secrets:{ServiceAuthenticationOptions.DefaultServiceKeyRef}", ServiceKey);
        builder.UseSetting("Api:BaseUrl", "http://localhost:5251");

        if (_configureAdminSeed)
        {
            builder.UseSetting("AdminSeed:Email", AdminEmail);
            builder.UseSetting("AdminSeed:Password", AdminPassword);
            builder.UseSetting("AdminSeed:DisplayName", "Web Tests Admin");
        }
        else
        {
            builder.UseSetting("AdminSeed:Email", string.Empty);
            builder.UseSetting("AdminSeed:Password", string.Empty);
        }

        builder.ConfigureServices(services =>
        {
            ReplaceWithInMemoryDatabase(services);

            // Records every outbound API call so tests can prove that a page rendered nothing
            // which reaches business data. Without this the calls would still be attempted and
            // simply fail, which no assertion on the markup would catch.
            services
                .AddHttpClient<ISeoIntelligenceApiClient, SeoIntelligenceApiClient>()
                .ConfigurePrimaryHttpMessageHandler(() => RecordedApiCalls);
        });
    }

    /// <summary>Captures the API requests the Web host makes while rendering.</summary>
    public RecordingHttpMessageHandler RecordedApiCalls { get; } = new();

    public sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        private readonly List<string> _requests = [];
        private readonly Lock _gate = new();

        public IReadOnlyList<string> Requests
        {
            get
            {
                lock (_gate)
                {
                    return [.. _requests];
                }
            }
        }

        public void Clear()
        {
            lock (_gate)
            {
                _requests.Clear();
            }
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                _requests.Add($"{request.Method} {request.RequestUri?.AbsolutePath}");
            }

            // The API is not part of these tests; an empty failure keeps components on their
            // "could not load" path instead of throwing.
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent(string.Empty),
                RequestMessage = request
            });
        }
    }

    private void ReplaceWithInMemoryDatabase(IServiceCollection services)
    {
        // Every registration keyed on the context has to go, not just DbContextOptions<T>:
        // AddDbContext also registers IDbContextOptionsConfiguration<T>, and leaving any of them
        // behind makes EF report two providers for one service provider.
        var contextType = typeof(SeoIntelligenceDbContext);
        var descriptorsToRemove = services
            .Where(descriptor =>
                descriptor.ServiceType == contextType
                || descriptor.ServiceType == typeof(DbContextOptions)
                || (descriptor.ServiceType.IsGenericType
                    && descriptor.ServiceType.GetGenericArguments().Contains(contextType)))
            .ToArray();

        foreach (var descriptor in descriptorsToRemove)
        {
            services.Remove(descriptor);
        }

        services.AddDbContext<SeoIntelligenceDbContext>(options =>
            options.UseInMemoryDatabase(_databaseName));
    }

    /// <summary>
    /// Outside Development the authentication cookie is marked Secure, so the client has to speak
    /// https or <see cref="System.Net.CookieContainer"/> silently drops the session. Redirects stay
    /// unfollowed so tests can assert on the redirect target itself.
    /// </summary>
    public HttpClient CreateAnonymousClient()
        => CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

    /// <summary>
    /// Creates a signed-in-capable account in the <see cref="ApplicationRoles.User"/> role, so
    /// tests can prove that a non-Admin account is kept out of the business screens while still
    /// being able to manage its own sign-in.
    /// </summary>
    public async Task EnsureNonAdminUserExistsAsync()
    {
        using var scope = Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        if (await userManager.FindByEmailAsync(UserEmail) is not null)
        {
            return;
        }

        var now = TimeProvider.System.GetUtcNow();
        var user = new ApplicationUser
        {
            UserName = UserEmail,
            Email = UserEmail,
            EmailConfirmed = true,
            DisplayName = "Web Tests User",
            IsEnabled = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        var created = await userManager.CreateAsync(user, UserPassword);
        Assert.True(created.Succeeded, string.Join(", ", created.Errors.Select(error => error.Code)));

        var roleAssigned = await userManager.AddToRoleAsync(user, ApplicationRoles.User);
        Assert.True(roleAssigned.Succeeded, string.Join(", ", roleAssigned.Errors.Select(error => error.Code)));
    }
}
