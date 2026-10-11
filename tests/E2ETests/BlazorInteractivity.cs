using System.Globalization;
using Microsoft.Playwright;

namespace E2ETests;

/// <summary>
/// The Web host prerenders every page, and nothing done on the prerendered markup reaches the
/// server: the Blazor circuit starts afterwards and replaces it.
/// </summary>
internal static class BlazorInteractivity
{
    private const string CircuitStartDelayVariable = "E2E_BROWSER_CIRCUIT_START_DELAY_MS";

    /// <summary>
    /// Opens <paramref name="url"/> and waits until the page is interactive, so every later step
    /// acts on the page the circuit rendered.
    /// </summary>
    public static async Task GotoAsync(IPage page, string url)
    {
        await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await WaitForInteractiveAsync(page);
    }

    /// <summary>
    /// The gate around every page keeps the controls disabled until the circuit renders it, and
    /// marks the moment with data-interactive.
    /// </summary>
    public static Task WaitForInteractiveAsync(IPage page)
        => page.Locator("[data-testid='interaction-gate'][data-interactive='true']")
            .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached });

    /// <summary>
    /// Holds back everything the pages of <paramref name="context"/> send over a WebSocket for
    /// E2E_BROWSER_CIRCUIT_START_DELAY_MS, so circuits start as late as on a slow connection.
    /// Without the variable nothing changes.
    /// </summary>
    public static async Task DelayCircuitStartAsync(IBrowserContext context)
    {
        DotEnvEnvironment.LoadRepositoryDotEnv();
        var configured = Environment.GetEnvironmentVariable(CircuitStartDelayVariable);
        if (string.IsNullOrWhiteSpace(configured))
        {
            return;
        }

        if (!int.TryParse(configured, NumberStyles.None, CultureInfo.InvariantCulture, out var delayMilliseconds)
            || delayMilliseconds <= 0)
        {
            throw new InvalidOperationException(
                $"{CircuitStartDelayVariable} must be a positive number of milliseconds, not '{configured}'.");
        }

        // The page's own WebSocket is wrapped because RouteWebSocketAsync combined with
        // ConnectToServer failed with KeyNotFoundException in Playwright .NET 1.62. The held-back
        // messages are sent in their original order.
        await context.AddInitScriptAsync(
            $$"""
            (() => {
              const Native = window.WebSocket;
              window.WebSocket = class extends Native {
                constructor(...args) {
                  super(...args);
                  const nativeSend = Native.prototype.send.bind(this);
                  let chain = new Promise(resolve => setTimeout(resolve, {{delayMilliseconds}}));
                  this.send = data => { chain = chain.then(() => nativeSend(data)); };
                }
              };
            })();
            """);
    }
}
