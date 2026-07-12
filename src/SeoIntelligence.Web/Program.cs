using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SeoIntelligence.Application.Configuration;
using SeoIntelligence.Application.Diagnostics;
using SeoIntelligence.Web.Components;
using SeoIntelligence.Web.Configuration;
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
});
builder.Services
    .AddHealthChecks()
    .AddCheck(
        "self",
        () => HealthCheckResult.Healthy("Web host is running."),
        tags: ["live", "ready"]);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapHealthChecks("/healthz", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
});

app.MapHealthChecks("/readyz", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

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
