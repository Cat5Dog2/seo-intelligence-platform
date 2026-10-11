using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace E2ETests;

internal sealed class BrowserSmokeFlow(
    IPage page,
    string webUrl,
    BrowserSmokeApi api,
    SmokeProject project)
{
    public async Task CompleteAsync()
    {
        await SignInAsync();
        await NavigateToProjectPageAsync(webUrl);
        await CompleteKeywordDiscoveryFlowAsync();
        await CompleteSearchVolumeFlowAsync();
        await CompleteAdminCredentialFlowAsync();
        await CompleteRankMonitoringFlowAsync();
        await CompleteReportFlowAsync();
    }

    /// <summary>
    /// Every page except the sign-in page requires an authenticated session, so the flow starts by
    /// signing in with the seeded administrator account.
    /// </summary>
    private async Task SignInAsync()
    {
        await NavigateAsync($"{webUrl}/login");
        await page.FillAsync("#email", RequiredEnvironment("E2E_ADMIN_EMAIL"));
        await page.FillAsync("#password", RequiredEnvironment("E2E_ADMIN_PASSWORD"));

        // By name, not the first submit button: the guest demo form comes first on the page, and
        // its button signs in as a guest with only the demo project.
        await page.GetByRole(AriaRole.Button, new() { Name = "ログイン", Exact = true }).ClickAsync();
        await page.WaitForURLAsync(
            url => !url.Contains("/login", StringComparison.Ordinal),
            new PageWaitForURLOptions { WaitUntil = WaitUntilState.NetworkIdle });
    }

    private static string RequiredEnvironment(string name)
    {
        DotEnvEnvironment.LoadRepositoryDotEnv();
        return Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"{name} is required when E2E_BROWSER_ENABLED=true.");
    }

    /// <summary>
    /// Picking an option changes the select's value and text in the browser at once, so neither
    /// shows that the server switched; the page header, rendered from the server's selection, does.
    /// </summary>
    private async Task SelectProjectAsync()
    {
        // The whole text, not a substring: another project's name can contain this one's.
        var selectedProjectHeader = page.Locator(".page-header p").Filter(new()
        {
            HasTextRegex = new Regex($@"^\s*選択中プロジェクト: {Regex.Escape(project.Name)}\s*$")
        });

        // Waits until the switcher is enabled (it is disabled while the projects load) and lists
        // the project.
        await page.GetByTestId("project-switcher").SelectOptionAsync(project.ProjectId);
        try
        {
            await selectedProjectHeader.WaitForAsync(new() { State = WaitForSelectorState.Attached });
        }
        catch (TimeoutException exception)
        {
            throw new TimeoutException(
                $"The page header did not show project {project.Name}. Current page text: {await ReadBodyTextAsync()}",
                exception);
        }
    }

    private async Task CompleteKeywordDiscoveryFlowAsync()
    {
        var seed = $"browser smoke {DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
        await NavigateToProjectPageAsync($"{webUrl}/keywords");
        await page.GetByTestId("keyword-seed-input").FillAsync(seed);
        await page.Locator("summary").Filter(new() { HasText = "詳細条件" }).ClickAsync();
        await page.GetByTestId("keyword-limit-input").FillAsync("10");
        await WaitForEnabledAsync("keyword-discovery-run-button");
        await page.GetByTestId("keyword-discovery-run-button").ClickAsync();

        // More than one source makes discovery a job, and the page loads the candidates (which the
        // export button waits for) only when 状態更新 is pressed after the job has finished.
        await ClickUntilEnabledAsync(
            page.GetByRole(AriaRole.Button, new() { Name = "状態更新", Exact = true }),
            "keyword-candidates-export-button");
        await page.GetByTestId("keyword-candidates-export-button").ClickAsync();
        await WaitForElementTextContainsAsync("keyword-status-message", "CSV");
    }

    private async Task CompleteSearchVolumeFlowAsync()
    {
        await NavigateToProjectPageAsync($"{webUrl}/search-volume");
        await page.GetByTestId("search-volume-keywords-input").FillAsync(
            """
            browser smoke keyword
            browser smoke keyword 2
            """);
        await page.Locator("summary").Filter(new() { HasText = "CSV・調査条件" }).ClickAsync();
        await page.GetByTestId("search-volume-location-input").SelectOptionAsync(new SelectOptionValue { Label = "日本" });
        await page.GetByTestId("search-volume-language-input").SelectOptionAsync(new SelectOptionValue { Label = "日本語" });
        await WaitForEnabledAsync("search-volume-register-button");
        await page.GetByTestId("search-volume-register-button").ClickAsync();
        await WaitForInputValueAsync("search-volume-job-id-input");

        // The export button waits for results, which arrive through a poll the worker runs
        // SearchVolumeService.PollInterval (60 s) after registration and Hangfire picks up within
        // 15 s of that. The page refreshes the job by itself, so waiting longer is enough.
        await WaitForEnabledAsync("search-volume-export-button", timeoutMilliseconds: 120_000);
        await page.GetByTestId("search-volume-export-button").ClickAsync();
        await WaitForElementTextContainsAsync("search-volume-status-message", "CSV");
    }

    private async Task CompleteAdminCredentialFlowAsync()
    {
        var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        var provider = $"browser_smoke_{stamp}";
        var secret = $"browser-smoke-secret-{stamp}";

        await NavigateAsync($"{webUrl}/admin");
        await page.GetByTestId("admin-credentials-tab").ClickAsync();
        await page.GetByTestId("admin-credential-provider-input").FillAsync(provider);
        await page.GetByTestId("admin-credential-secret-input").FillAsync(secret);
        await WaitForEnabledAsync("admin-credential-save-button");
        await page.GetByTestId("admin-credential-save-button").ClickAsync();
        await WaitForAnyTextAsync(provider);
        await AssertPageDoesNotExposeSecretAsync(secret);
    }

    private async Task CompleteRankMonitoringFlowAsync()
    {
        var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        await NavigateToProjectPageAsync($"{webUrl}/rank-monitoring");
        await page.GetByTestId("rank-keywords-input").FillAsync(
            $"""
            browser rank {stamp}
            browser rank secondary {stamp}
            """);
        await page.GetByTestId("rank-targets-input").FillAsync(
            """
            example.com
            """);
        await WaitForEnabledAsync("rank-register-button");
        await page.GetByTestId("rank-register-button").ClickAsync();
        await WaitForElementTextNonEmptyAsync("rank-status-message");
    }

    private async Task CompleteReportFlowAsync()
    {
        await NavigateToProjectPageAsync($"{webUrl}/reports");
        await page.GetByTestId("report-period-input").FillAsync(DateTime.UtcNow.ToString("yyyy-MM", CultureInfo.InvariantCulture));
        await page.GetByTestId("report-format-select").SelectOptionAsync("pdf");
        await page.GetByTestId("report-sections-input").FillAsync("summary, rank, rewrite, cannibalization");
        await page.GetByTestId("report-share-expires-at-input").FillAsync(
            DateTimeOffset.Now.AddDays(7).ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture));
        await WaitForEnabledAsync("report-create-button");
        await page.GetByTestId("report-create-button").ClickAsync();
        await WaitForInputValueAsync("report-id-input");

        var reportId = await ReadInputValueAsync("report-id-input");
        await api.WaitForReportCompletedAsync(project.ProjectId, reportId);

        await page.GetByTestId("report-load-button").ClickAsync();
        await WaitForElementTextAsync("report-status-value", "completed");
        await WaitForElementTextContainsAsync("report-file-uri-value", "storage://local/reports/");

        await WaitForEnabledAsync("report-download-button");
        await page.GetByTestId("report-download-button").ClickAsync();
        var downloadLink = page.GetByTestId("report-download-url-link");
        await downloadLink.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        // Visible is not enough: the link used to point at a storage:// URI that no browser can
        // open. It has to target the Web host's own download route, which carries the operator's
        // cookie and fetches the file from the API with the service key.
        var downloadHref = await downloadLink.GetAttributeAsync("href");
        Assert.Equal(
            $"/downloads/projects/{project.ProjectId}/reports/{reportId}",
            downloadHref);

        var download = await page.RunAndWaitForDownloadAsync(async () => await downloadLink.ClickAsync());
        var downloadedPath = await download.PathAsync();
        Assert.False(string.IsNullOrWhiteSpace(downloadedPath), "The report download produced no file.");
        Assert.StartsWith("monthly-", download.SuggestedFilename, StringComparison.Ordinal);
        Assert.EndsWith(".pdf", download.SuggestedFilename, StringComparison.Ordinal);
        Assert.StartsWith("%PDF-", await ReadFirstBytesAsync(downloadedPath!, 5), StringComparison.Ordinal);

        await WaitForEnabledAsync("report-share-button");
        await page.GetByTestId("report-share-button").ClickAsync();
        await WaitForElementTextAsync("report-share-status-value", "active");
        await WaitForElementTextContainsAsync("report-share-url-value", "/api/report-shares/");
    }

    /// <summary>
    /// Reads the first bytes of a downloaded file as text, so the smoke can tell a real artifact
    /// from an error page that happened to be saved with the right name.
    /// </summary>
    private static async Task<string> ReadFirstBytesAsync(string path, int count)
    {
        await using var stream = File.OpenRead(path);
        var buffer = new byte[count];
        var read = await stream.ReadAtLeastAsync(buffer, count, throwOnEndOfStream: false);
        return System.Text.Encoding.ASCII.GetString(buffer, 0, read);
    }

    /// <summary>
    /// A full navigation starts a new Blazor circuit, which selects the newest active project
    /// again. The other browser tests create projects in parallel, so the flow selects its own
    /// project on every project-scoped page instead of relying on the default.
    /// </summary>
    private async Task NavigateToProjectPageAsync(string url)
    {
        await NavigateAsync(url);
        await SelectProjectAsync();
    }

    private Task NavigateAsync(string url)
        => BlazorInteractivity.GotoAsync(page, url);

    // :disabled, not the disabled property: a control inside a disabled fieldset keeps the
    // property false.
    private Task WaitForEnabledAsync(string testId, float? timeoutMilliseconds = null)
        => page.WaitForFunctionAsync(
            """
            selector => {
                const element = document.querySelector(selector);
                return !!element && !element.matches(':disabled');
            }
            """,
            TestIdSelector(testId),
            new PageWaitForFunctionOptions { Timeout = timeoutMilliseconds });

    /// <summary>
    /// Presses a refresh control, as an operator would, until the job behind the page has
    /// finished and the element it unlocks is enabled.
    /// </summary>
    private async Task ClickUntilEnabledAsync(ILocator refreshButton, string testId)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(60);
        var target = page.GetByTestId(testId);
        while (!await target.IsEnabledAsync())
        {
            if (DateTimeOffset.UtcNow >= deadline)
            {
                throw new TimeoutException($"{testId} was not enabled within 60 seconds. Current page text: {await ReadBodyTextAsync()}");
            }

            await refreshButton.ClickAsync();
            await page.WaitForTimeoutAsync(1_000);
        }
    }

    private Task WaitForInputValueAsync(string testId)
        => page.WaitForFunctionAsync(
            """
            selector => {
                const element = document.querySelector(selector);
                return !!element && element.value.length > 0;
            }
            """,
            TestIdSelector(testId));

    private Task WaitForElementTextAsync(string testId, string expected)
        => page.WaitForFunctionAsync(
            """
            ([selector, expected]) => {
                const element = document.querySelector(selector);
                return element?.textContent?.trim() === expected;
            }
            """,
            new[] { TestIdSelector(testId), expected });

    private Task WaitForElementTextContainsAsync(string testId, string expected)
        => page.WaitForFunctionAsync(
            """
            ([selector, expected]) => {
                const element = document.querySelector(selector);
                return element?.textContent?.includes(expected) === true;
            }
            """,
            new[] { TestIdSelector(testId), expected });

    private Task WaitForElementTextNonEmptyAsync(string testId)
        => page.WaitForFunctionAsync(
            """
            selector => {
                const element = document.querySelector(selector);
                return !!element?.textContent?.trim();
            }
            """,
            TestIdSelector(testId));

    private async Task WaitForAnyTextAsync(params string[] texts)
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
            var bodyText = await ReadBodyTextAsync();
            throw new TimeoutException($"Timed out waiting for one of [{string.Join(", ", texts)}]. Current page text: {bodyText}", exception);
        }
    }

    private async Task<string> ReadBodyTextAsync()
    {
        var bodyText = await page.Locator("body").InnerTextAsync();
        return bodyText.Length <= 2_000 ? bodyText : bodyText[..2_000];
    }

    private Task<string> ReadInputValueAsync(string testId)
        => page.GetByTestId(testId).InputValueAsync();

    private async Task AssertPageDoesNotExposeSecretAsync(string secret)
    {
        var exposesSecret = await page.EvaluateAsync<bool>(
            """
            secret => {
                const text = document.body?.innerText ?? "";
                const html = document.body?.innerHTML ?? "";
                const formValues = Array
                    .from(document.querySelectorAll("input, textarea"))
                    .map(element => element.value ?? "")
                    .join("\n");
                return text.includes(secret) || html.includes(secret) || formValues.includes(secret);
            }
            """,
            secret);
        Assert.False(exposesSecret, "The API credential secret was rendered or left in a form value.");
    }

    private static string TestIdSelector(string testId)
        => $"[data-testid='{testId}']";
}
