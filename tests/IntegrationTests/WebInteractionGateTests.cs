using System.Net;
using System.Text.RegularExpressions;
using IntegrationTests.Support;

namespace IntegrationTests;

/// <summary>
/// Nothing done on prerendered markup reaches the server, and the circuit's first render replaces
/// it. The browser tests show the gate opening once the circuit is interactive; these show that
/// every control the server prerenders is behind it.
/// </summary>
public sealed partial class WebInteractionGateTests
{
    [Fact]
    [Trait("Category", "UI")]
    public async Task PrerenderedSignInFormStaysDisabledUntilTheCircuitIsInteractive()
    {
        await using var factory = new WebAuthenticationFactory();
        using var client = factory.CreateAnonymousClient();

        var html = await client.GetStringAsync("/login");

        AssertControlsAreBehindAClosedGate(html, "id=\"email\"", "id=\"password\"", "登録不要でデモを試す");
    }

    [Fact]
    [Trait("Category", "UI")]
    public async Task PrerenderedLayoutAndPageControlsStayDisabledUntilTheCircuitIsInteractive()
    {
        await using var factory = new WebAuthenticationFactory();
        using var client = factory.CreateAnonymousClient();
        using (var signIn = await WebGuestLoginTests.SignInAsync(client))
        {
            Assert.Equal(HttpStatusCode.Redirect, signIn.StatusCode);
        }

        var html = await client.GetStringAsync("/keywords");

        AssertControlsAreBehindAClosedGate(
            html,
            "data-testid=\"project-switcher\"",
            "class=\"text-button settings-trigger\"",
            "data-testid=\"keyword-seed-input\"",
            "data-testid=\"keyword-discovery-run-button\"");
    }

    private static void AssertControlsAreBehindAClosedGate(string html, params string[] controls)
    {
        var gate = Assert.Single(GateRegex().Matches(html));
        var attributes = gate.Groups["attributes"].Value;
        Assert.Matches(@"\sdisabled(\s|$)", attributes);
        Assert.Contains("data-interactive=\"false\"", attributes, StringComparison.Ordinal);
        Assert.Contains("aria-busy=\"true\"", attributes, StringComparison.Ordinal);

        var gateEnd = FindClosingFieldset(html, gate.Index);
        foreach (var control in controls)
        {
            var position = html.IndexOf(control, StringComparison.Ordinal);
            Assert.True(position >= 0, $"The prerendered page has no {control}.");
            Assert.True(
                position > gate.Index && position < gateEnd,
                $"{control} is outside the interaction gate.");
        }
    }

    /// <summary>
    /// Pages nest fieldsets of their own inside the gate, so its end is where the depth returns to 0.
    /// </summary>
    private static int FindClosingFieldset(string html, int start)
    {
        var depth = 0;
        foreach (Match tag in FieldsetTagRegex().Matches(html, start))
        {
            depth += tag.Value.StartsWith("</", StringComparison.Ordinal) ? -1 : 1;
            if (depth == 0)
            {
                return tag.Index;
            }
        }

        Assert.Fail("The interaction gate is never closed.");
        return -1;
    }

    [GeneratedRegex("""<fieldset(?<attributes>[^>]*data-testid="interaction-gate"[^>]*)>""")]
    private static partial Regex GateRegex();

    [GeneratedRegex(@"</?fieldset\b")]
    private static partial Regex FieldsetTagRegex();
}
