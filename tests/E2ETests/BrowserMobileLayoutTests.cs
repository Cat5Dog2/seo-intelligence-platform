using Microsoft.Playwright;

namespace E2ETests;

public sealed class BrowserMobileLayoutTests
{
    private static readonly string[] BusinessRoutes =
    [
        "/", "/dashboard", "/keywords", "/search-volume", "/competitors", "/influx",
        "/content-analysis", "/clusters", "/briefs", "/rewrite", "/ai-assistant",
        "/rank-monitoring", "/reports", "/admin"
    ];

    [EnvironmentEnabledFact("E2E_BROWSER_ENABLED")]
    [Trait("Category", "BrowserE2E")]
    public Task MinimumMobileViewportFitsEveryPageAndAllowsProjectAndMenuOperations()
        => VerifyMobileLayoutAsync(375, 667);

    [EnvironmentEnabledFact("E2E_BROWSER_ENABLED")]
    [Trait("Category", "BrowserE2E")]
    public Task NarrowAndroidViewportFitsEveryPageAndAllowsTouchOperations()
        => VerifyMobileLayoutAsync(360, 640, emulateAndroid: true);

    [EnvironmentEnabledFact("E2E_BROWSER_ENABLED")]
    [Trait("Category", "BrowserE2E")]
    public Task MediumAndroidViewportFitsEveryPageAndAllowsTouchOperations()
        => VerifyMobileLayoutAsync(393, 851, emulateAndroid: true);

    [EnvironmentEnabledFact("E2E_BROWSER_ENABLED")]
    [Trait("Category", "BrowserE2E")]
    public Task LargeAndroidViewportFitsEveryPageAndAllowsTouchOperations()
        => VerifyMobileLayoutAsync(412, 915, emulateAndroid: true);

    private static async Task VerifyMobileLayoutAsync(int width, int height, bool emulateAndroid = false)
    {
        var webUrl = RequiredEnvironment("E2E_WEB_URL").TrimEnd('/');
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync();
        var options = emulateAndroid
            ? new BrowserNewContextOptions(playwright.Devices["Pixel 7"])
            : new BrowserNewContextOptions();
        options.ViewportSize = new ViewportSize { Width = width, Height = height };
        options.IsMobile = true;
        options.HasTouch = true;
        await using var context = await browser.NewContextAsync(options);
        var page = await context.NewPageAsync();
        page.SetDefaultTimeout(15_000);

        await NavigateAsync(page, $"{webUrl}/login");
        await AssertFitsAsync(page, "login");
        await page.GetByLabel("メールアドレス", new() { Exact = true }).FillAsync(RequiredEnvironment("E2E_ADMIN_EMAIL"));
        await page.GetByLabel("パスワード", new() { Exact = true }).FillAsync(RequiredEnvironment("E2E_ADMIN_PASSWORD"));
        await page.GetByRole(AriaRole.Button, new() { Name = "ログイン", Exact = true }).TapAsync();
        await page.WaitForURLAsync(url => !url.Contains("/login", StringComparison.Ordinal));
        await NavigateAsync(page, webUrl);

        // A long unbroken name reproduces overflow that short Japanese examples miss.
        var projectName = string.Concat(Enumerable.Repeat("MobileResponsiveValidation", 4)) + Guid.NewGuid().ToString("N")[..16];
        await page.GetByLabel("プロジェクト名（必須）", new() { Exact = true }).FillAsync(projectName);
        await page.GetByLabel("プロジェクト名（必須）", new() { Exact = true }).PressAsync("Tab");
        await page.GetByRole(AriaRole.Button, new() { Name = "プロジェクトを作成", Exact = true }).TapAsync();
        await page.GetByRole(AriaRole.Status).Filter(new() { HasText = "プロジェクトを作成しました" }).WaitForAsync();
        await page.GetByRole(AriaRole.Cell, new() { Name = projectName, Exact = true }).WaitForAsync();
        var projectTable = page.Locator(".data-table-wrap").Filter(new()
        {
            Has = page.GetByRole(AriaRole.Cell, new() { Name = projectName, Exact = true })
        });
        await projectTable.PressAsync("ArrowRight");
        await page.WaitForFunctionAsync("() => document.activeElement?.classList.contains('data-table-wrap') && document.activeElement.scrollLeft > 0");

        foreach (var route in BusinessRoutes)
        {
            await NavigateAsync(page, webUrl + route);
            await page.GetByTestId("project-switcher").SelectOptionAsync(new SelectOptionValue { Label = projectName });
            if (route != "/admin")
            {
                await page.Locator(".page-header p").Filter(new() { HasText = projectName }).WaitForAsync(new() { State = WaitForSelectorState.Attached });
            }
            await AssertFitsAsync(page, route);
        }

        (string Tab, string Heading)[] adminTabs =
        [
            ("ワークスペース", "ワークスペース設定"), ("APIキー", "API認証情報"),
            ("クレジット", "外部API呼び出し・クレジット"), ("Discord通知", "Discord通知設定"),
            ("ジョブ", "ジョブ一覧"), ("監視・アラート", "Phase 2ジョブ"),
            ("外部サービス連携", "外部連携スタブ設定"), ("監査ログ", "監査ログ検索")
        ];
        foreach (var (tab, heading) in adminTabs)
        {
            await page.GetByRole(AriaRole.Button, new() { Name = tab, Exact = true }).TapAsync();
            await page.GetByRole(AriaRole.Heading, new() { Name = heading, Exact = true }).WaitForAsync();
            foreach (var summary in await page.Locator("main details:not([open]) > summary").AllAsync())
            {
                await summary.TapAsync();
            }
            await AssertFitsAsync(page, $"admin/{tab}");
        }

        await page.GetByRole(AriaRole.Button, new() { Name = "その他", Exact = true }).TapAsync();
        await page.GetByRole(AriaRole.Navigation, new() { Name = "その他の機能" }).WaitForAsync();
        await AssertFitsAsync(page, "expanded menu");
        await page.GetByRole(AriaRole.Link, new() { Name = "プロジェクト", Exact = true }).TapAsync();
        await page.GetByRole(AriaRole.Heading, new() { Name = "プロジェクト", Exact = true }).WaitForAsync();
        await page.GetByRole(AriaRole.Navigation, new() { Name = "その他の機能" })
            .WaitForAsync(new() { State = WaitForSelectorState.Hidden });

        await page.Locator(".settings-trigger").TapAsync();
        await page.GetByRole(AriaRole.Dialog, new() { Name = "設定・アカウント" }).WaitForAsync();
        await AssertFitsAsync(page, "settings sheet");
        await page.Locator("#app-settings").GetByRole(AriaRole.Button, new() { Name = "閉じる", Exact = true }).PressAsync("Escape");
        await page.GetByRole(AriaRole.Dialog, new() { Name = "設定・アカウント" })
            .WaitForAsync(new() { State = WaitForSelectorState.Hidden });

        foreach (var route in new[] { "/account", "/forbidden", "/not-found", "/Error" })
        {
            await NavigateAsync(page, webUrl + route);
            await AssertFitsAsync(page, route);
        }
    }

    private static async Task AssertFitsAsync(IPage page, string state)
    {
        var viewport = page.ViewportSize!;
        var violations = await page.EvaluateAsync<string[]>(
            """
            expected => {
                const problems = [];
                const width = document.documentElement.clientWidth;
                if (innerWidth !== expected.width || innerHeight !== expected.height) {
                    problems.push(`viewport must be ${expected.width}x${expected.height}`);
                }
                if (document.documentElement.scrollWidth > width + 1) problems.push('horizontal page overflow');
                for (const e of document.querySelectorAll('input, select, textarea, button')) {
                    const r = e.getBoundingClientRect();
                    if (!e.checkVisibility() || !r.width || !r.height || e.type === 'hidden') continue;
                    if (!e.closest('.data-table-wrap') && (r.left < -1 || r.right > width + 1)) {
                        problems.push(`${e.tagName}: control outside page`);
                    }
                    if (e.type === 'checkbox' || e.type === 'radio') continue;
                    if (r.height < 43.5) problems.push(`${e.tagName}: target smaller than 44px`);
                    if (e.tagName !== 'BUTTON' && parseFloat(getComputedStyle(e).fontSize) < 16) {
                        problems.push(`${e.tagName}: input font smaller than 16px`);
                    }
                }
                return problems;
            }
            """, new { width = viewport.Width, height = viewport.Height });
        Assert.True(violations.Length == 0, $"{viewport.Width}x{viewport.Height} {state}: {string.Join(", ", violations)}");
    }

    private static Task NavigateAsync(IPage page, string url)
        => page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

    private static string RequiredEnvironment(string name)
    {
        DotEnvEnvironment.LoadRepositoryDotEnv();
        return Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"{name} is required when E2E_BROWSER_ENABLED=true.");
    }
}
