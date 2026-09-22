using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SeoIntelligence.Contracts.Api;
using SeoIntelligence.Web.Components.Common;
using SeoIntelligence.Web.Services;

namespace E2ETests;

public sealed class BlazorUsabilityTests
{
    [Theory]
    [Trait("Category", "UI")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GettingStartedOnlyLinksToResearchWhenAProjectIsAvailable(bool hasProject)
    {
        var html = await RenderAsync<GettingStarted>(new() { [nameof(GettingStarted.HasProject)] = hasProject });
        Assert.Contains("href=\"/#project-form\"", html);
        Assert.Equal(hasProject, html.Contains("href=\"/keywords\"", StringComparison.Ordinal));
        Assert.Equal(hasProject, html.Contains("href=\"/search-volume\"", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Category", "UI")]
    public async Task SuccessfulSaveIsAnnouncedAsStatusInsteadOfAnError()
    {
        var html = await RenderAsync<ActionFeedback>(new() { [nameof(ActionFeedback.SuccessMessage)] = "保存しました。" });
        Assert.Contains("role=\"status\"", html);
        Assert.Contains("保存しました。", html);
        Assert.DoesNotContain("role=\"alert\"", html);
    }

    [Fact]
    [Trait("Category", "UI")]
    public async Task FailedSaveShowsTheApiErrorInsteadOfAStaleSuccessMessage()
    {
        var html = await RenderAsync<ActionFeedback>(new()
        {
            [nameof(ActionFeedback.SuccessMessage)] = "前の保存は成功しました。",
            [nameof(ActionFeedback.Errors)] = new ApiError[] { new("Conflict", "同名のプロジェクトが存在します。", "名称") }
        });
        Assert.Contains("role=\"alert\"", html);
        Assert.Contains("同名のプロジェクトが存在します。", html);
        Assert.DoesNotContain("前の保存は成功しました。", html);
    }

    [Fact]
    [Trait("Category", "UI")]
    public async Task BusyFeedbackDoesNotAnnounceThePreviousSaveResult()
    {
        var html = await RenderAsync<ActionFeedback>(new()
        {
            [nameof(ActionFeedback.IsBusy)] = true,
            [nameof(ActionFeedback.SuccessMessage)] = "前の結果"
        });
        Assert.Contains("保存中", html);
        Assert.DoesNotContain("前の結果", html);
    }

    [Fact]
    [Trait("Category", "UI")]
    public async Task StatusFilterLocalizesLabelsAndPreservesApiValuesAndUnknownOptions()
    {
        var html = await RenderAsync<StatusFilter>(new()
        {
            [nameof(StatusFilter.Statuses)] = new StatusFilterOption[] { new("active", "active"), new("future_state", "future_state") }
        });
        Assert.Matches("value=\"active\"[^>]*>有効</option>", html);
        Assert.Contains("value=\"future_state\">future_state</option>", html);
    }

    [Fact]
    [Trait("Category", "UI")]
    public async Task CreditFailureIsNotPresentedAsZeroUsage()
    {
        using var handler = new FailureHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://localhost") };
        var client = new SeoIntelligenceApiClient(http, NullLogger<SeoIntelligenceApiClient>.Instance);
        var html = await RenderAsync<CreditBadge>([], services => services.AddSingleton<ISeoIntelligenceApiClient>(client));
        Assert.Contains("取得できませんでした", html);
        Assert.DoesNotContain("<strong>日 0</strong>", html);
    }

    private static async Task<string> RenderAsync<T>(Dictionary<string, object?> parameters, Action<IServiceCollection>? configure = null)
        where T : IComponent
    {
        var services = new ServiceCollection().AddLogging();
        configure?.Invoke(services);
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var result = await renderer.RenderComponentAsync<T>(ParameterView.FromDictionary(parameters));
            return WebUtility.HtmlDecode(result.ToHtmlString());
        });
    }

    private sealed class FailureHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = JsonContent.Create(ApiResponseEnvelope<object>.Failure("usage-unavailable", [new ApiError("Unavailable", "利用量を取得できません。")]))
            });
    }
}
