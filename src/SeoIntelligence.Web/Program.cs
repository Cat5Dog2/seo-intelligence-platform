using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SeoIntelligence.Application.Configuration;
using SeoIntelligence.Application.Diagnostics;
using SeoIntelligence.Infrastructure.Identity;
using SeoIntelligence.Web.Components;
using SeoIntelligence.Web.Configuration;
using SeoIntelligence.Web.Endpoints;
using SeoIntelligence.Web.Security;
using SeoIntelligence.Web.Services;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = ResolveContentRootPath()
});

RegisterSeoIntelligenceOptions(builder.Services, builder.Configuration);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.AddEventSourceLogger();
builder.Logging.Configure(options =>
{
    options.ActivityTrackingOptions =
        ActivityTrackingOptions.TraceId |
        ActivityTrackingOptions.SpanId |
        ActivityTrackingOptions.ParentId |
        ActivityTrackingOptions.Baggage;
});

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddSingleton(SeoIntelligenceDiagnostics.ActivitySource);
builder.Services.AddSingleton(SeoIntelligenceDiagnostics.Meter);
builder.Services.Configure<SeoIntelligenceApiOptions>(builder.Configuration.GetSection(SeoIntelligenceApiOptions.SectionName));
builder.Services.AddScoped<ProjectSelectionState>();
builder.Services.AddTrustedProxyForwardedHeaders(builder.Configuration, builder.Environment);
builder.Services.AddSeoIntelligenceWebAuthentication(builder.Configuration, builder.Environment);
var dataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"];
if (string.IsNullOrWhiteSpace(dataProtectionKeysPath))
{
    dataProtectionKeysPath = Path.Combine(builder.Environment.ContentRootPath, ".data", "data-protection-keys");
}
Directory.CreateDirectory(dataProtectionKeysPath);
builder.Services
    .AddDataProtection()
    .SetApplicationName("SeoIntelligence.Web")
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));
builder.Services.AddHttpClient<ISeoIntelligenceApiClient, SeoIntelligenceApiClient>((serviceProvider, client) =>
{
    var options = serviceProvider
        .GetRequiredService<Microsoft.Extensions.Options.IOptions<SeoIntelligenceApiOptions>>()
        .Value;
    var baseUrl = string.IsNullOrWhiteSpace(options.BaseUrl)
        ? "http://localhost:5251"
        : options.BaseUrl;

    client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
}).AddHttpMessageHandler<ServiceKeyHttpMessageHandler>();
builder.Services
    .AddHealthChecks()
    .AddCheck(
        "self",
        () => HealthCheckResult.Healthy("Web host is running."),
        tags: ["live", "ready"]);

var app = builder.Build();

// First: everything after this reads RemoteIpAddress and Scheme. HSTS and HTTPS redirection
// would otherwise see http behind a TLS-terminating proxy and redirect in a loop.
app.UseForwardedHeaders();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
// Status-code pages exist for page navigation. The account endpoints answer with API-style
// status codes (for example 400 when an antiforgery token is rejected), and re-executing
// /not-found would replace those with a page - and, because /not-found requires
// authentication, turn them into a sign-in redirect that hides the real status code.
app.UseWhen(
    context => !IsAccountEndpointPath(context.Request.Path),
    branch => branch.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true));
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseAntiforgery();

app.MapHealthChecks("/healthz", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
}).AllowAnonymous();

app.MapHealthChecks("/readyz", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
}).AllowAnonymous();

app.MapStaticAssets();
app.MapAccountEndpoints();
app.MapDownloadEndpoints();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

await SeedIdentityAsync(app);
await app.RunAsync();

static bool IsAccountEndpointPath(PathString path)
    => path.StartsWithSegments("/login")
        || path.StartsWithSegments("/logout")
        || path.StartsWithSegments("/account");

static async Task SeedIdentityAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var seeder = scope.ServiceProvider.GetRequiredService<IIdentityDataSeeder>();
    await seeder.SeedAsync();
}

static void RegisterSeoIntelligenceOptions(IServiceCollection services, IConfiguration configuration)
{
    services.AddOptions<DatabaseOptions>()
        .Configure(options => options.ConnectionString =
            configuration.GetConnectionString(DatabaseOptions.DefaultConnectionName));

    services.Configure<RedisOptions>(configuration.GetSection(RedisOptions.SectionName));
    services.Configure<HangfireOptions>(configuration.GetSection(HangfireOptions.SectionName));
    services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
    services.Configure<OpenTelemetryOptions>(configuration.GetSection(OpenTelemetryOptions.SectionName));
}

static string ResolveContentRootPath()
{
    var explicitContentRoot =
        Environment.GetEnvironmentVariable("ASPNETCORE_CONTENTROOT")
        ?? Environment.GetEnvironmentVariable("DOTNET_CONTENTROOT");
    if (!string.IsNullOrWhiteSpace(explicitContentRoot))
    {
        return explicitContentRoot;
    }

    var currentDirectory = Directory.GetCurrentDirectory();
    if (IsWebContentRoot(currentDirectory))
    {
        return currentDirectory;
    }

    var applicationBaseDirectory = AppContext.BaseDirectory;
    return IsWebContentRoot(applicationBaseDirectory)
        ? applicationBaseDirectory
        : currentDirectory;
}

static bool IsWebContentRoot(string path)
    => File.Exists(Path.Combine(path, "SeoIntelligence.Web.csproj"))
        || File.Exists(Path.Combine(path, "SeoIntelligence.Web.staticwebassets.runtime.json"))
        || Directory.Exists(Path.Combine(path, "Components"));
