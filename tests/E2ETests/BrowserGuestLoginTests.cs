using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace E2ETests;

public sealed class BrowserGuestLoginTests
{
    [EnvironmentEnabledFact("E2E_GUEST_BROWSER_ENABLED")]
    [Trait("Category", "BrowserE2E")]
    public async Task GuestCanResearchAndDownloadAnIsolatedMockCsv()
    {
        var webUrl = Environment.GetEnvironmentVariable("E2E_WEB_URL")?.TrimEnd('/')
            ?? throw new InvalidOperationException("E2E_WEB_URL is required.");
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using var context = await browser.NewContextAsync(new() { AcceptDownloads = true, ViewportSize = new() { Width = 1280, Height = 900 } });
        var page = await context.NewPageAsync();
        page.SetDefaultTimeout(15_000);
        await page.GotoAsync(webUrl + "/login");
        await page.GetByRole(AriaRole.Button, new() { Name = "登録不要でデモを試す", Exact = true }).ClickAsync();
        await Expect(page.Locator(".brand-row .guest-mode-badge")).ToHaveTextAsync("ゲスト・Mock");
        await Expect(page.GetByRole(AriaRole.Link, new() { Name = "管理", Exact = true })).ToHaveCountAsync(0);
        // Wait for the newly signed-in page to finish loading its Blazor circuit before typing.
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        const string projectName = "ブラウザ専用デモ";
        await page.GetByLabel("プロジェクト名（必須）", new() { Exact = true }).FillAsync(projectName);
        await page.GetByLabel("プロジェクト名（必須）", new() { Exact = true }).PressAsync("Tab");
        await page.GetByRole(AriaRole.Button, new() { Name = "プロジェクトを作成", Exact = true }).ClickAsync();
        await Expect(page.GetByRole(AriaRole.Cell, new() { Name = projectName, Exact = true })).ToBeVisibleAsync();

        await page.GetByRole(AriaRole.Link, new() { Name = "キーワード", Exact = true }).ClickAsync();
        await page.GetByLabel("シードキーワード", new() { Exact = true }).FillAsync("珈琲");
        await page.GetByTestId("keyword-discovery-run-button").ClickAsync();
        await Expect(page.GetByRole(AriaRole.Cell, new() { Name = "珈琲 とは", Exact = true })).ToBeVisibleAsync();
        await page.GetByTestId("keyword-candidates-export-button").ClickAsync();
        var download = await page.RunAndWaitForDownloadAsync(async () =>
            await page.GetByTestId("job-download-link").First.ClickAsync());
        var path = await download.PathAsync();
        Assert.NotNull(path);
        var csv = await File.ReadAllTextAsync(path);
        Assert.Contains("珈琲", csv);
        Assert.Contains("Mock", csv);

        await page.GetByRole(AriaRole.Link, new() { Name = "検索ボリューム", Exact = true }).ClickAsync();
        await page.GetByTestId("search-volume-keywords-input").FillAsync("珈琲 豆\n珈琲 入れ方");
        await page.GetByTestId("search-volume-register-button").ClickAsync();
        await Expect(page.GetByRole(AriaRole.Cell, new() { Name = "珈琲 豆", Exact = true })).ToBeVisibleAsync();

        await page.GotoAsync(webUrl + "/admin");
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "権限がありません", Exact = true })).ToBeVisibleAsync();
        await page.Locator(".settings-trigger").ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "ログアウト", Exact = true }).ClickAsync();
        await Expect(page.GetByRole(AriaRole.Button, new() { Name = "登録不要でデモを試す", Exact = true })).ToBeVisibleAsync();

        await using var otherContext = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 390, Height = 844 } });
        var other = await otherContext.NewPageAsync();
        await other.GotoAsync(webUrl + "/login");
        await other.GetByRole(AriaRole.Button, new() { Name = "登録不要でデモを試す", Exact = true }).ClickAsync();
        await Expect(other.Locator(".brand-row .guest-mode-badge")).ToBeVisibleAsync();
        await Expect(other.GetByRole(AriaRole.Cell, new() { Name = projectName, Exact = true })).ToHaveCountAsync(0);
        Assert.True(await other.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth"));
    }
}
