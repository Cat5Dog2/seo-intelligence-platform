using Microsoft.Playwright;

namespace E2ETests;

public sealed class BrowserSmokeTests
{
    [EnvironmentEnabledFact("E2E_BROWSER_ENABLED")]
    [Trait("Category", "BrowserE2E")]
    public async Task BrowserSmokeCompletesRepresentativePhaseUserFlows()
    {
        var webUrl = RequiredEnvironment("E2E_WEB_URL").TrimEnd('/');
        var apiUrl = RequiredEnvironment("E2E_API_URL").TrimEnd('/');
        var api = new BrowserSmokeApi(apiUrl);
        var project = await api.CreateSmokeProjectAsync();

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = IsHeadless()
        });
        await using var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize
            {
                Width = 1366,
                Height = 900
            }
        });
        var page = await context.NewPageAsync();
        page.SetDefaultTimeout(15_000);

        var flow = new BrowserSmokeFlow(page, webUrl, api, project);
        await flow.CompleteAsync();
    }

    private static bool IsHeadless()
    {
        DotEnvEnvironment.LoadRepositoryDotEnv();
        return !string.Equals(Environment.GetEnvironmentVariable("E2E_HEADLESS"), "false", StringComparison.OrdinalIgnoreCase);
    }

    private static string RequiredEnvironment(string name)
    {
        DotEnvEnvironment.LoadRepositoryDotEnv();
        return Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"{name} is required when E2E_BROWSER_ENABLED=true.");
    }
}

public sealed class EnvironmentEnabledFactAttribute : FactAttribute
{
    public EnvironmentEnabledFactAttribute(string environmentVariable, string requiredValue = "true")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(environmentVariable);
        DotEnvEnvironment.LoadRepositoryDotEnv();

        if (!string.Equals(
            Environment.GetEnvironmentVariable(environmentVariable),
            requiredValue,
            StringComparison.OrdinalIgnoreCase))
        {
            Skip = $"{environmentVariable}={requiredValue} is required to run this test.";
        }
    }
}
