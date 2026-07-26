using System.Globalization;
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
        await SelectProjectAsync();
        await CompleteKeywordDiscoveryFlowAsync();
        await CompleteSearchVolumeFlowAsync();
        await CompleteAdminCredentialFlowAsync();
        await CompleteRankMonitoringFlowAsync();
        await CompleteReportFlowAsync();
    }

    private async Task SelectProjectAsync()
    {
        await NavigateAsync(webUrl);
        var switcher = page.GetByTestId("project-switcher");
        await switcher.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await page.WaitForFunctionAsync(
            """
            ([selector, expectedValue]) => {
                const select = document.querySelector(selector);
                return !!select && Array.from(select.options).some(option => option.value === expectedValue);
            }
            """,
            new[] { "[data-testid='project-switcher']", project.ProjectId });
        await switcher.SelectOptionAsync(project.ProjectId);
        await page.WaitForFunctionAsync(
            """
            ([selector, expectedValue, expectedText]) => {
                const select = document.querySelector(selector);
                const option = select?.selectedOptions?.[0];
                return select?.value === expectedValue && option?.textContent?.trim() === expectedText;
            }
            """,
            new[] { "[data-testid='project-switcher']", project.ProjectId, project.Name });
    }

    private async Task CompleteKeywordDiscoveryFlowAsync()
    {
        var seed = $"browser smoke {DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
        await NavigateAsync($"{webUrl}/keywords");
        await page.GetByTestId("keyword-seed-input").FillAsync(seed);
        await page.GetByTestId("keyword-limit-input").FillAsync("10");
        await WaitForEnabledAsync("keyword-discovery-run-button");
        await page.GetByTestId("keyword-discovery-run-button").ClickAsync();

        await WaitForEnabledAsync("keyword-candidates-export-button");
        await page.GetByTestId("keyword-candidates-export-button").ClickAsync();
        await WaitForElementTextContainsAsync("keyword-status-message", "CSV");
    }

    private async Task CompleteSearchVolumeFlowAsync()
    {
        await NavigateAsync($"{webUrl}/search-volume");
        await page.GetByTestId("search-volume-keywords-input").FillAsync(
            """
            browser smoke keyword
            browser smoke keyword 2
            """);
        await page.GetByTestId("search-volume-location-input").FillAsync("Japan");
        await page.GetByTestId("search-volume-language-input").FillAsync("Japanese");
        await WaitForEnabledAsync("search-volume-register-button");
        await page.GetByTestId("search-volume-register-button").ClickAsync();
        await WaitForInputValueAsync("search-volume-job-id-input");

        await WaitForEnabledAsync("search-volume-export-button");
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
        await page.GetByTestId("admin-credential-key-ref-input").FillAsync(string.Empty);
        await page.GetByTestId("admin-credential-secret-input").FillAsync(secret);
        await WaitForEnabledAsync("admin-credential-save-button");
        await page.GetByTestId("admin-credential-save-button").ClickAsync();
        await WaitForAnyTextAsync(provider);
        await AssertPageDoesNotExposeSecretAsync(secret);
    }

    private async Task CompleteRankMonitoringFlowAsync()
    {
        var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        await NavigateAsync($"{webUrl}/rank-monitoring");
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
        await NavigateAsync($"{webUrl}/reports");
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
        await page.GetByTestId("report-download-url-link").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        await WaitForEnabledAsync("report-share-button");
        await page.GetByTestId("report-share-button").ClickAsync();
        await WaitForElementTextAsync("report-share-status-value", "active");
        await WaitForElementTextContainsAsync("report-share-url-value", "/api/report-shares/");
    }

    private async Task NavigateAsync(string url)
    {
        await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForFunctionAsync("() => typeof window !== 'undefined' && !!window.Blazor");
        await page.WaitForTimeoutAsync(500);
    }

    private Task WaitForEnabledAsync(string testId)
        => page.WaitForFunctionAsync(
            """
            selector => {
                const element = document.querySelector(selector);
                return !!element && !element.disabled;
            }
            """,
            TestIdSelector(testId));

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
