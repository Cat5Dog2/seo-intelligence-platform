using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace E2ETests;

public sealed class BrowserSmokeTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ITestOutputHelper output;

    public BrowserSmokeTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    [Trait("Category", "BrowserE2E")]
    public async Task MvpBrowserSmokeCompletesPrimaryUserFlows()
    {
        if (!IsBrowserE2EEnabled())
        {
            output.WriteLine("BrowserE2E is disabled. Set E2E_BROWSER_ENABLED=true to run Playwright browser smoke tests.");
            return;
        }

        var webUrl = RequiredEnvironment("E2E_WEB_URL").TrimEnd('/');
        var apiUrl = RequiredEnvironment("E2E_API_URL").TrimEnd('/');
        var project = await CreateSmokeProjectAsync(apiUrl);

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

        await SelectProjectAsync(page, webUrl, project.ProjectId, project.Name);
        await CompleteKeywordDiscoveryFlowAsync(page, webUrl);
        await CompleteSearchVolumeFlowAsync(page, webUrl);
    }

    private static async Task SelectProjectAsync(
        IPage page,
        string webUrl,
        string projectId,
        string projectName)
    {
        await NavigateAsync(page, webUrl);
        var switcher = page.GetByTestId("project-switcher");
        await switcher.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await page.WaitForFunctionAsync(
            """
            ([selector, expectedValue]) => {
                const select = document.querySelector(selector);
                return !!select && Array.from(select.options).some(option => option.value === expectedValue);
            }
            """,
            new[] { "[data-testid='project-switcher']", projectId });
        await switcher.SelectOptionAsync(projectId);
        await page.WaitForFunctionAsync(
            """
            ([selector, expectedValue, expectedText]) => {
                const select = document.querySelector(selector);
                const option = select?.selectedOptions?.[0];
                return select?.value === expectedValue && option?.textContent?.trim() === expectedText;
            }
            """,
            new[] { "[data-testid='project-switcher']", projectId, projectName });
    }

    private static async Task CompleteKeywordDiscoveryFlowAsync(IPage page, string webUrl)
    {
        var seed = $"browser smoke {DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
        await NavigateAsync(page, $"{webUrl}/keywords");
        await page.GetByTestId("keyword-seed-input").FillAsync(seed);
        await page.GetByTestId("keyword-limit-input").FillAsync("10");
        await WaitForEnabledAsync(page, "keyword-discovery-run-button");
        await page.GetByTestId("keyword-discovery-run-button").ClickAsync();
        await WaitForAnyTextAsync(page, "探索ジョブを登録しました", "件の候補語を保存しました");

        await WaitForEnabledAsync(page, "keyword-candidates-export-button");
        await page.GetByTestId("keyword-candidates-export-button").ClickAsync();
        await WaitForTextVisibleAsync(page, "CSV出力ジョブを登録しました");
    }

    private static async Task CompleteSearchVolumeFlowAsync(IPage page, string webUrl)
    {
        await NavigateAsync(page, $"{webUrl}/search-volume");
        await page.GetByTestId("search-volume-keywords-input").FillAsync(
            """
            browser smoke keyword
            browser smoke keyword 2
            """);
        await page.GetByTestId("search-volume-location-input").FillAsync("JP");
        await page.GetByTestId("search-volume-language-input").FillAsync("ja");
        await WaitForEnabledAsync(page, "search-volume-register-button");
        await page.GetByTestId("search-volume-register-button").ClickAsync();
        await WaitForTextVisibleAsync(page, "検索ボリュームジョブを登録しました");
        await page.WaitForFunctionAsync(
            """
            selector => {
                const input = document.querySelector(selector);
                return !!input && input.value.length > 0;
            }
            """,
            "[data-testid='search-volume-job-id-input']");

        await WaitForEnabledAsync(page, "search-volume-export-button");
        await page.GetByTestId("search-volume-export-button").ClickAsync();
        await WaitForTextVisibleAsync(page, "CSV出力ジョブを登録しました");
    }

    private static async Task NavigateAsync(IPage page, string url)
    {
        await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForFunctionAsync("() => typeof window !== 'undefined' && !!window.Blazor");
        await page.WaitForTimeoutAsync(500);
    }

    private static Task WaitForEnabledAsync(IPage page, string testId)
        => page.WaitForFunctionAsync(
            """
            selector => {
                const element = document.querySelector(selector);
                return !!element && !element.disabled;
            }
            """,
            $"[data-testid='{testId}']");

    private static async Task WaitForAnyTextAsync(IPage page, params string[] texts)
    {
        try
        {
            await page.WaitForFunctionAsync(
                """
                expectedTexts => expectedTexts.some(text => document.body?.innerText.includes(text))
                """,
                texts);
        }
        catch (TimeoutException exception)
        {
            var bodyText = await ReadBodyTextAsync(page);
            throw new TimeoutException($"Timed out waiting for one of [{string.Join(", ", texts)}]. Current page text: {bodyText}", exception);
        }
    }

    private static async Task WaitForTextVisibleAsync(IPage page, string text)
    {
        try
        {
            await page.GetByText(text).WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        }
        catch (TimeoutException exception)
        {
            var bodyText = await ReadBodyTextAsync(page);
            throw new TimeoutException($"Timed out waiting for text '{text}'. Current page text: {bodyText}", exception);
        }
    }

    private static async Task<string> ReadBodyTextAsync(IPage page)
    {
        var bodyText = await page.Locator("body").InnerTextAsync();
        return bodyText.Length <= 2_000 ? bodyText : bodyText[..2_000];
    }

    private static async Task<SmokeProject> CreateSmokeProjectAsync(string apiUrl)
    {
        using var httpClient = new HttpClient
        {
            BaseAddress = new Uri(apiUrl, UriKind.Absolute)
        };
        var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss");
        using var response = await httpClient.PostAsJsonAsync(
            "/api/projects",
            new
            {
                name = $"Browser smoke {stamp}",
                defaultLocation = "JP",
                defaultLanguage = "ja",
                kpi = new { },
                memo = "Created by BrowserSmokeTests"
            },
            JsonOptions);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        var root = document.RootElement;
        if (!root.GetProperty("result").GetBoolean())
        {
            throw new InvalidOperationException("Project creation API returned result=false.");
        }

        var data = root.GetProperty("data");
        return new SmokeProject(
            data.GetProperty("projectId").GetString() ?? throw new InvalidOperationException("Project creation response did not include projectId."),
            data.GetProperty("name").GetString() ?? throw new InvalidOperationException("Project creation response did not include name."));
    }

    private static bool IsBrowserE2EEnabled()
        => string.Equals(Environment.GetEnvironmentVariable("E2E_BROWSER_ENABLED"), "true", StringComparison.OrdinalIgnoreCase);

    private static bool IsHeadless()
        => !string.Equals(Environment.GetEnvironmentVariable("E2E_HEADLESS"), "false", StringComparison.OrdinalIgnoreCase);

    private static string RequiredEnvironment(string name)
        => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"{name} is required when E2E_BROWSER_ENABLED=true.");

    private sealed record SmokeProject(string ProjectId, string Name);
}
