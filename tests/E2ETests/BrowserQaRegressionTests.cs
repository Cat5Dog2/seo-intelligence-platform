using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SeoIntelligence.Application.Jobs;
using SeoIntelligence.Application.Services;
using SeoIntelligence.Contracts.Api;
using SeoIntelligence.Web.Components.Pages;
using SeoIntelligence.Web.Components.Common;
using SeoIntelligence.Web.Services;

namespace E2ETests;

public sealed class BrowserQaRegressionTests
{
    [Theory]
    [Trait("Category", "UI")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ClusterBriefActionSendsExactlyOneKeywordSelector(bool hasKeywordId)
    {
        using var fixture = new UiFixture();
        var page = await fixture.CreateAsync<TopicClusters>();
        var keywordId = hasKeywordId ? Guid.NewGuid() : (Guid?)null;
        var cluster = new TopicClusterSummary(Guid.NewGuid(), fixture.ProjectId, "SEO", null, null,
            keywordId, "SEO guide", 85m, 2, "informational", 0, [], [], DateTime.UtcNow, DateTime.UtcNow);

        await InvokeAsync(page, "GenerateBriefAsync", cluster);

        var request = Assert.Single(fixture.Posts).Deserialize<GenerateBriefRequest>(UiFixture.JsonOptions)!;
        Assert.Equal(keywordId, request.TargetKeywordId);
        Assert.Equal(hasKeywordId ? null : "SEO guide", request.TargetKeyword);
        Assert.Equal(cluster.ClusterId, request.ClusterId);
    }

    [Fact]
    [Trait("Category", "UI")]
    public async Task ContentBriefActionUsesKeywordIdAndKeepsCompetitorUrls()
    {
        using var fixture = new UiFixture();
        var page = await fixture.CreateAsync<ContentAnalysis>();
        var competitors = Enumerable.Range(1, 6).Select(index => new ContentSearchResultRow(
            Guid.NewGuid(), $"https://example.com/{index}", "example.com", "SEO guide", "description",
            100, 10, null, null, DateTime.UtcNow)).ToArray();
        var analysis = new ContentAnalysisResultRow(Guid.NewGuid(), "SEO guide", competitors, [], [], DateTime.UtcNow);

        await InvokeAsync(page, "GenerateBriefAsync", analysis);

        var request = Assert.Single(fixture.Posts).Deserialize<GenerateBriefRequest>(UiFixture.JsonOptions)!;
        Assert.Equal(analysis.KeywordId, request.TargetKeywordId);
        Assert.Null(request.TargetKeyword);
        Assert.Equal(competitors.Take(5).Select(row => row.Url), request.CompetitorUrls);
    }

    [Fact]
    [Trait("Category", "UI")]
    public async Task AiDefaultFormSendsSupportedTools()
    {
        using var fixture = new UiFixture();
        var page = await fixture.CreateAsync<AiAssistant>();
        Set(Get(page, "Form")!, "Message", "Summarize the project.");

        await InvokeAsync(page, "SendAsync");

        var request = Assert.Single(fixture.Posts).Deserialize<AiChatRequest>(UiFixture.JsonOptions)!;
        Assert.Equal(new[] { "keyword-discovery", "brief-generation", "rewrite-analysis", "report-summary" }, request.AllowedTools);
    }

    [Fact]
    [Trait("Category", "UI")]
    public async Task RefreshAiResponseLoadsSavedAnswerWithoutSubmittingAnotherMessage()
    {
        using var fixture = new UiFixture();
        var page = await fixture.CreateAsync<AiAssistant>();
        Set(Get(page, "Form")!, "Message", "Summarize the project.");
        await InvokeAsync(page, "SendAsync");
        Assert.Contains("queued", ((AiChatResponse)Get(page, "Response")!).Response);

        await InvokeAsync(page, "RefreshResponseAsync");

        var response = (AiChatResponse)Get(page, "Response")!;
        Assert.Equal("Completed draft", response.Response);
        Assert.Equal(42, response.TokenUsage.GetProperty("totalTokens").GetInt32());
        Assert.Single(fixture.Posts);
        Assert.Contains($"/api/projects/{fixture.ProjectId:D}/ai/messages/{fixture.MessageId:D}", fixture.GetPaths);
    }

    [Fact]
    [Trait("Category", "UI")]
    public async Task JobRefreshUpdatesRenderedAiAnswerAndProjectChangeClearsConversation()
    {
        using var fixture = new UiFixture();
        var api = fixture.CreateClient();
        var state = new ProjectSelectionState(api);
        await state.LoadAsync();
        var activator = new CapturingActivator();
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton<ISeoIntelligenceApiClient>(api);
        services.AddSingleton(state);
        services.AddSingleton<IComponentActivator>(activator);
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var rendered = await renderer.RenderComponentAsync<AiAssistant>();
            var page = Assert.Single(activator.Components.OfType<AiAssistant>());
            Set(Get(page, "Form")!, "Message", "Summarize the project.");
            await DispatchAsync(page, "SendAsync");
            Assert.Contains("queued", rendered.ToHtmlString());

            var panel = Assert.Single(activator.Components.OfType<JobProgressPanel>());
            await DispatchAsync(panel, "LoadAsync");
            Assert.Contains("Completed draft", rendered.ToHtmlString());
            Assert.Single(fixture.Posts);

            await state.SelectAsync(fixture.OtherProjectId);
            Assert.DoesNotContain("Completed draft", rendered.ToHtmlString());
            Assert.Null(Get(Get(page, "Form")!, "ConversationId"));
        });
    }

    [Fact]
    [Trait("Category", "UI")]
    public async Task CompletedKeywordDiscoveryRefreshLoadsCandidatesWithoutRunningAnotherSearch()
    {
        using var fixture = new UiFixture();
        var page = await fixture.CreateAsync<Keywords>();
        Set(Get(page, "DiscoveryForm")!, "SeedKeyword", "QA search");
        await InvokeAsync(page, "DiscoverAsync");
        await InvokeAsync(page, "RefreshDiscoveryJobAsync");
        var candidates = (IReadOnlyList<KeywordCandidate>)Get(page, "Candidates")!;
        Assert.Equal("QA keyword", Assert.Single(candidates).Keyword);
        Assert.Single(fixture.Posts);
    }

    private static Task DispatchAsync(IHandleEvent component, string method)
        => component.HandleEventAsync(new EventCallbackWorkItem((Func<Task>)(() => InvokeAsync(component, method))), null);

    private sealed class CapturingActivator : IComponentActivator
    {
        public List<IComponent> Components { get; } = [];
        public IComponent CreateInstance(Type componentType)
        {
            var component = (IComponent)Activator.CreateInstance(componentType)!;
            Components.Add(component);
            return component;
        }
    }

    private static object? Get(object target, string name)
        => target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(target);

    private static void Set(object target, string name, object? value)
        => target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.SetValue(target, value);

    private static Task InvokeAsync(object target, string name, params object[] arguments)
    {
        var method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return (Task)method.Invoke(target, arguments)!;
    }

    // Exercise the actual Razor event handlers and HTTP client without replacing them with request builders.
    private sealed class UiFixture : HttpMessageHandler
    {
        public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        public Guid ProjectId { get; } = Guid.NewGuid();
        public Guid OtherProjectId { get; } = Guid.NewGuid();
        public Guid MessageId { get; } = Guid.NewGuid();
        public List<JsonElement> Posts { get; } = [];
        public List<string> GetPaths { get; } = [];
        private readonly Guid sessionId = Guid.NewGuid();
        private readonly Guid jobId = Guid.NewGuid();
        private readonly HttpClient http;

        public UiFixture() => http = new HttpClient(this, disposeHandler: false) { BaseAddress = new Uri("https://localhost") };

        public SeoIntelligenceApiClient CreateClient()
            => new(http, NullLogger<SeoIntelligenceApiClient>.Instance);

        public async Task<T> CreateAsync<T>() where T : new()
        {
            var api = CreateClient();
            var state = new ProjectSelectionState(api);
            await state.LoadAsync();
            var page = new T();
            Set(page, "ApiClient", api);
            Set(page, "ProjectState", state);
            return page;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Get)
            {
                GetPaths.Add(path);
                if (path == "/api/projects")
                {
                    return Reply(new[] { ProjectId, OtherProjectId }.Select(projectId =>
                        new ProjectDetails(projectId, Guid.NewGuid(), "QA", "JP", "ja",
                            JsonSerializer.SerializeToElement(new { }), null, "active", DateTime.UtcNow, DateTime.UtcNow, null)).ToArray());
                }
                if (path == "/api/jobs") return Reply(Array.Empty<JobDetails>());
                if (path == $"/api/jobs/{jobId:D}")
                {
                    return Reply(new JobDetails(jobId, Guid.NewGuid(), ProjectId, "KeywordDiscoveryJob", "succeeded", 100,
                        $"/api/jobs/{jobId:D}", null, null, 0, null, null, "developer", DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow));
                }
                if (path.EndsWith($"/keyword-discovery/jobs/{jobId:D}/results", StringComparison.Ordinal))
                {
                    return Reply(new KeywordDiscoveryResult([new KeywordCandidate("QA keyword", "suggest", null, 80m)], JobId: jobId));
                }
                if (path.EndsWith($"/ai/messages/{MessageId:D}", StringComparison.Ordinal))
                {
                    return Reply(Response("Completed draft", new { totalTokens = 42 }));
                }
            }
            else if (request.Method == HttpMethod.Post)
            {
                Posts.Add(JsonSerializer.Deserialize<JsonElement>(await request.Content!.ReadAsStringAsync(cancellationToken)));
                if (path.EndsWith("/ai/chat", StringComparison.Ordinal))
                {
                    return Reply(Response("AI response generation has been queued.", new { }));
                }
                if (path.EndsWith("/keyword-discovery/suggest", StringComparison.Ordinal))
                {
                    return Reply(new KeywordDiscoveryResult([], IsAccepted: true, JobId: jobId));
                }
                return Reply(new JobReference(jobId, "queued"));
            }
            throw new InvalidOperationException($"Unexpected request: {request.Method} {path}");
        }

        private AiChatResponse Response(string response, object tokenUsage)
            => new(sessionId, MessageId, jobId, response, [], JsonSerializer.SerializeToElement(new { }),
                JsonSerializer.SerializeToElement(tokenUsage), "not_required", "pending");

        private static HttpResponseMessage Reply<T>(T data)
            => new(HttpStatusCode.OK) { Content = JsonContent.Create(ApiResponseEnvelope<T>.Success("qa", data)) };

        protected override void Dispose(bool disposing)
        {
            if (disposing) http.Dispose();
            base.Dispose(disposing);
        }
    }
}
