using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using SeoIntelligence.Application.Jobs;
using SeoIntelligence.Application.Services;
using SeoIntelligence.Contracts.Api;
using SeoIntelligence.Web.Components.Pages;
using SeoIntelligence.Web.Components.Common;
using SeoIntelligence.Web.Services;

namespace E2ETests;

public sealed class BrowserQaRegressionTests
{
    [Fact]
    [Trait("Category", "UI")]
    public async Task FailedResultDownloadKeepsResearchOpenAndDoesNotOfferAResultJump()
    {
        using var fixture = new UiFixture { FailResults = true };
        await fixture.RenderSearchVolumeAsync(async (page, state) =>
        {
            Set(page, "KeywordsText", "SEO");
            await DispatchAsync(page, "RegisterJobAsync");
            var html = fixture.ReadHtml!();
            Assert.Contains("結果を取得できません。", html);
            Assert.DoesNotContain("結果を見る", html);
            Assert.Matches("<details[^>]*data-testid=\"research-input\"[^>]*open", html);
        });
    }

    [Fact]
    [Trait("Category", "UI")]
    public async Task UnavailableLocaleChoicesPreserveCurrentSelectionAndOfferRetry()
    {
        using var fixture = new UiFixture { FailLocales = true };
        await fixture.RenderAsync<LocationLanguageSelector>((page, state) =>
        {
            var html = fixture.ReadHtml!();
            Assert.Contains("選択肢を再取得", html);
            Assert.Contains("現在の設定はそのまま使えます。", html);
            Assert.Matches("value=\"JP\"[^>]*>日本</option>", html);
            Assert.Matches("value=\"ja\"[^>]*>日本語</option>", html);
            return Task.CompletedTask;
        });
    }

    [Theory]
    [Trait("Category", "UI")]
    [InlineData("succeeded", true)]
    [InlineData("failed_retryable", false)]
    [InlineData("canceled", false)]
    public async Task CompletedResearchOffersAResultJumpAndCollapsesOnlySuccessfulInput(string status, bool completed)
    {
        using var fixture = new UiFixture { JobStatus = status };
        await fixture.RenderSearchVolumeAsync(async (page, state) =>
        {
            Set(page, "KeywordsText", "SEO");
            await DispatchAsync(page, "RegisterJobAsync");
            var html = fixture.ReadHtml!();
            Assert.Equal(completed, html.Contains("結果を見る", StringComparison.Ordinal));
            Assert.Matches(completed
                ? "<details[^>]*data-testid=\"research-input\"[^>]*>"
                : "<details[^>]*data-testid=\"research-input\"[^>]*open", html);
            if (completed) Assert.DoesNotMatch("<details[^>]*data-testid=\"research-input\"[^>]*open", html);
            await ((IHandleEvent)page).HandleEventAsync(new EventCallbackWorkItem((Action)(() =>
                page.GetType().GetMethod("ClearInput", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(page, null))), null);
            Assert.DoesNotContain("結果を見る", fixture.ReadHtml!());
            Assert.Matches("<details[^>]*data-testid=\"research-input\"[^>]*open", fixture.ReadHtml!());
        });
    }

    [Fact]
    [Trait("Category", "UI")]
    public async Task LocaleSelectionShowsJapaneseLabelsWithoutChangingStoredCodes()
    {
        using var fixture = new UiFixture();
        await fixture.RenderAsync<LocationLanguageSelector>((page, state) =>
        {
            var html = fixture.ReadHtml!();
            Assert.Matches("value=\"JP\"[^>]*>日本</option>", html);
            Assert.Matches("value=\"ja\"[^>]*>日本語</option>", html);
            Assert.Equal("JP", state.SelectedProject!.DefaultLocation);
            Assert.Equal("ja", state.SelectedProject.DefaultLanguage);
            return Task.CompletedTask;
        });
    }

    [Theory]
    [Trait("Category", "UI")]
    [InlineData(0, 0, true)]
    [InlineData(1, 0, false)]
    [InlineData(0, 1, false)]
    public async Task DashboardShowsActiveOrFailedWorkEvenWhenNoResultsExist(int running, int failed, bool empty)
    {
        using var fixture = new UiFixture { Dashboard = new DashboardSnapshot(0, running, failed, 0) };
        await fixture.RenderAsync<Dashboard>((page, state) =>
        {
            var html = fixture.ReadHtml!();
            Assert.Equal(empty, html.Contains("最初のキーワードを見つけましょう", StringComparison.Ordinal));
            Assert.Equal(!empty, html.Contains("調査の状況", StringComparison.Ordinal));
            return Task.CompletedTask;
        });
    }

    [Fact]
    [Trait("Category", "UI")]
    public async Task ExportCompletionUpdatesTheRenderedDownloadLinkWithoutManualRefresh()
    {
        using var fixture = new UiFixture();
        await fixture.RenderSearchVolumeAsync(async (page, state) =>
        {
            Set(page, "JobIdText", fixture.JobId.ToString());
            await DispatchAsync(page, "CreateResultsExportAsync");
            Assert.Contains("data-testid=\"job-download-link\"", fixture.ReadHtml!());
            Assert.Contains($"/downloads/projects/{fixture.ProjectId:D}/exports/{fixture.ExportId:D}", fixture.ReadHtml!());
        });
    }

    [Theory]
    [Trait("Category", "UI")]
    [InlineData("queued")]
    [InlineData("running")]
    [InlineData("waiting_external")]
    public async Task SearchVolumePollsPendingJobsUntilResultsAreReady(string initialStatus)
    {
        using var fixture = new UiFixture { JobStatus = initialStatus };
        await fixture.RenderSearchVolumeAsync(async (page, state) =>
        {
            Set(page, "KeywordsText", "SEO");
            await InvokeAsync(page, "RegisterJobAsync");
            Assert.Empty((IReadOnlyList<SearchVolumeResultRow>)Get(page, "Results")!);
            await InvokeAsync(page, "RegisterJobAsync");
            Assert.Single(fixture.Posts);
            fixture.JobStatus = "succeeded";
            await fixture.ResultsRequested.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal("QA volume", Assert.Single((IReadOnlyList<SearchVolumeResultRow>)Get(page, "Results")!).Keyword);
            Assert.Single(fixture.Posts);
        });
    }

    [Theory]
    [Trait("Category", "UI")]
    [InlineData("project")]
    [InlineData("clear")]
    [InlineData("dispose")]
    public async Task SearchVolumeDiscardsAnInFlightPollAfterLeavingTheResearch(string action)
    {
        using var fixture = new UiFixture { JobStatus = "running" };
        await fixture.RenderSearchVolumeAsync(async (page, state) =>
        {
            Set(page, "KeywordsText", "SEO");
            await InvokeAsync(page, "RegisterJobAsync");
            var response = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            fixture.PendingResponse = response;
            Assert.Equal($"/api/jobs/{fixture.JobId:D}", await fixture.PendingRequestStarted.Task.WaitAsync(TimeSpan.FromSeconds(10)));
            var polling = (Task)Get(page, "PollingTask")!;
            if (action == "project")
            {
                await state.SelectAsync(fixture.OtherProjectId);
                await state.SelectAsync(fixture.ProjectId);
            }
            else if (action == "clear")
            {
                page.GetType().GetMethod("ClearInput", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(page, null);
            }
            else page.Dispose();
            fixture.JobStatus = "succeeded";
            response.SetResult(UiFixture.Reply(fixture.Job()));
            await polling.WaitAsync(TimeSpan.FromSeconds(10));
            if (action != "dispose") Assert.Null(Get(page, "CurrentJob"));
            Assert.Empty((IReadOnlyList<SearchVolumeResultRow>)Get(page, "Results")!);
            Assert.False(fixture.ResultsRequested.Task.IsCompleted);
        });
    }

    [Theory]
    [Trait("Category", "UI")]
    [InlineData("succeeded", true)]
    [InlineData("failed_retryable", false)]
    [InlineData("failed_fatal", false)]
    [InlineData("canceled", false)]
    public async Task SearchVolumeCompletionAutomaticallyLoadsResults(string status, bool hasResults)
    {
        using var fixture = new UiFixture { JobStatus = status };
        await fixture.RenderSearchVolumeAsync(async (page, state) =>
        {
            Set(page, "KeywordsText", "SEO");
            await InvokeAsync(page, "RegisterJobAsync");
            var results = (IReadOnlyList<SearchVolumeResultRow>)Get(page, "Results")!;
            Assert.Equal(hasResults ? 1 : 0, results.Count);
            if (hasResults) Assert.Equal("QA volume", results[0].Keyword);
            Assert.Single(fixture.Posts);
        });
    }

    [Fact]
    [Trait("Category", "UI")]
    public async Task SearchVolumeDoesNotSubmitTwiceWhileRegistrationIsPending()
    {
        using var fixture = new UiFixture();
        await fixture.RenderSearchVolumeAsync(async (page, state) =>
        {
            Set(page, "KeywordsText", "SEO");
            var response = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            fixture.PendingResponse = response;
            var pending = InvokeAsync(page, "RegisterJobAsync");
            await InvokeAsync(page, "RegisterJobAsync");
            Assert.Empty(fixture.Posts);
            response.SetResult(UiFixture.Reply(new JobReference(fixture.JobId, "queued")));
            await pending;
        });
    }

    [Theory]
    [Trait("Category", "UI")]
    [InlineData("LoadResultsAsync", "IsResultLoading")]
    [InlineData("CreateResultsExportAsync", "IsExporting")]
    public async Task SearchVolumeInvalidatedRequestDoesNotLeaveLoadingState(string operation, string loadingProperty)
    {
        using var fixture = new UiFixture { JobStatus = "running" };
        await fixture.RenderSearchVolumeAsync(async (page, state) =>
        {
            Set(page, "JobIdText", fixture.JobId.ToString());
            var response = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            fixture.PendingResponse = response;
            var pending = InvokeAsync(page, operation);
            Assert.True((bool)Get(page, loadingProperty)!);
            if (operation == "LoadResultsAsync") await InvokeAsync(page, "RefreshJobAsync");
            else Set(page, "JobIdText", Guid.NewGuid().ToString());
            response.SetResult(operation == "LoadResultsAsync"
                ? UiFixture.Reply(new[] { new SearchVolumeResultRow("stale", 99, null, null, null) })
                : UiFixture.Reply(new JobReference(fixture.JobId, "queued")));
            await pending;
            Assert.False((bool)Get(page, loadingProperty)!);
            Assert.Empty((IReadOnlyList<SearchVolumeResultRow>)Get(page, "Results")!);
            Assert.Null(Get(page, "Message"));
        });
    }

    [Theory]
    [Trait("Category", "UI")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BriefProjectChangeClearsEditorAndDiscardsLateDetails(bool delayDetails)
    {
        using var fixture = new UiFixture();
        await fixture.RenderAsync<ArticleBriefs>(async (page, state) =>
        {
            var response = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (delayDetails) fixture.PendingResponse = response;
            var pending = InvokeAsync(page, "SelectBriefAsync", fixture.BriefId);
            if (!delayDetails)
            {
                await pending;
                Assert.NotNull(Get(page, "SelectedBrief"));
            }
            await state.SelectAsync(fixture.OtherProjectId);
            await state.SelectAsync(fixture.ProjectId);
            if (delayDetails)
            {
                response.SetResult(UiFixture.Reply(fixture.Brief()));
                await pending;
            }
            Assert.Null(Get(page, "SelectedBrief"));
            Assert.Empty((IReadOnlyList<ArticleBriefVersionDetails>)Get(page, "Versions")!);
            Assert.Null(Get(Get(page, "EditForm")!, "Title"));
            await InvokeAsync(page, "SaveAsync");
            Assert.Empty(fixture.Posts);
        });
    }

    [Fact]
    [Trait("Category", "UI")]
    public async Task SearchVolumeProjectChangeClearsJobResultsInputAndExportState()
    {
        using var fixture = new UiFixture();
        await fixture.RenderSearchVolumeAsync(async (page, state) =>
        {
            Set(page, "JobIdText", fixture.JobId.ToString());
            Set(page, "KeywordsText", "private project keywords");
            Set(page, "Message", "old export");
            await InvokeAsync(page, "RefreshJobAsync");
            await InvokeAsync(page, "LoadResultsAsync");
            Assert.Single((IReadOnlyList<SearchVolumeResultRow>)Get(page, "Results")!);

            await state.SelectAsync(fixture.OtherProjectId);

            Assert.Null(Get(page, "CurrentJob"));
            Assert.Null(Get(page, "JobIdText"));
            Assert.Null(Get(page, "KeywordsText"));
            Assert.Null(Get(page, "Message"));
            Assert.Empty((IReadOnlyList<SearchVolumeResultRow>)Get(page, "Results")!);
        });
    }

    [Theory]
    [Trait("Category", "UI")]
    [InlineData("LoadResultsAsync", "results")]
    [InlineData("RefreshJobAsync", "job")]
    [InlineData("RegisterJobAsync", "register")]
    public async Task SearchVolumeDiscardsResponsesAfterSwitchingAwayAndBack(string method, string operation)
    {
        using var fixture = new UiFixture();
        await fixture.RenderSearchVolumeAsync(async (page, state) =>
        {
            Set(page, "JobIdText", fixture.JobId.ToString());
            Set(page, "KeywordsText", "private project keywords");
            var response = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            fixture.PendingResponse = response;
            var pending = InvokeAsync(page, method);
            await state.SelectAsync(fixture.OtherProjectId);
            await state.SelectAsync(fixture.ProjectId);
            response.SetResult(operation switch
            {
                "results" => UiFixture.Reply(new[] { new SearchVolumeResultRow("private result", 900, null, null, null) }),
                "register" => UiFixture.Reply(new JobReference(fixture.JobId, "queued")),
                _ => UiFixture.Reply(fixture.Job())
            });
            await pending;
            Assert.Null(Get(page, "CurrentJob"));
            Assert.Null(Get(page, "JobIdText"));
            Assert.Null(Get(page, "Message"));
            Assert.Empty((IReadOnlyList<SearchVolumeResultRow>)Get(page, "Results")!);
        });
    }

    [Fact]
    [Trait("Category", "UI")]
    public async Task SearchVolumeRejectsAnotherProjectsJobAndCannotCancelIt()
    {
        using var fixture = new UiFixture();
        await fixture.RenderSearchVolumeAsync(async (page, state) =>
        {
            await state.SelectAsync(fixture.OtherProjectId);
            Set(page, "JobIdText", fixture.JobId.ToString());
            await InvokeAsync(page, "RefreshJobAsync");
            Assert.Null(Get(page, "CurrentJob"));
            Assert.NotEmpty((IReadOnlyList<ApiError>)Get(page, "Errors")!);
            await InvokeAsync(page, "CancelJobAsync");
            Assert.Empty(fixture.Posts);
        });
    }

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

    [Theory]
    [Trait("Category", "UI")]
    [InlineData("succeeded")]
    [InlineData("failed_retryable")]
    [InlineData("failed_fatal")]
    public async Task CompletedKeywordDiscoveryRefreshLoadsCandidatesWithoutRunningAnotherSearch(string status)
    {
        using var fixture = new UiFixture();
        fixture.JobStatus = status;
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
        public Guid JobId { get; } = Guid.NewGuid();
        private Guid jobId => JobId;
        public TaskCompletionSource<HttpResponseMessage>? PendingResponse { get; set; }
        public TaskCompletionSource<string> PendingRequestStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ResultsRequested { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string JobStatus { get; set; } = "succeeded";
        public Guid BriefId { get; } = Guid.NewGuid();
        public Guid ExportId { get; } = Guid.NewGuid();
        public bool ExportCreated { get; set; }
        public bool FailResults { get; set; }
        public bool FailLocales { get; set; }
        public DashboardSnapshot Dashboard { get; set; } = new(0, 0, 0, 0);
        public Func<string>? ReadHtml { get; set; }
        public ArticleBriefDetails Brief() => new(BriefId, ProjectId, null, "Private brief", null, "SEO", 1,
            JsonSerializer.SerializeToElement(new { title = "Private brief" }), "pending", "draft", DateTime.UtcNow, DateTime.UtcNow);
        private readonly HttpClient http;

        public UiFixture() => http = new HttpClient(this, disposeHandler: false) { BaseAddress = new Uri("https://localhost") };

        public SeoIntelligenceApiClient CreateClient()
            => new(http, NullLogger<SeoIntelligenceApiClient>.Instance);

        public JobDetails Job() => new(jobId, Guid.NewGuid(), ProjectId, "RegisterSearchVolumeJob", JobStatus, 100,
            $"/api/jobs/{jobId:D}", null, null, 0, null, null, "developer", DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow);

        public Task RenderSearchVolumeAsync(Func<SearchVolume, ProjectSelectionState, Task> action)
            => RenderAsync(action);

        public async Task RenderAsync<T>(Func<T, ProjectSelectionState, Task> action) where T : IComponent
        {
            var api = CreateClient();
            var state = new ProjectSelectionState(api);
            await state.LoadAsync();
            var activator = new CapturingActivator();
            var services = new ServiceCollection().AddLogging();
            services.AddSingleton<ISeoIntelligenceApiClient>(api);
            services.AddSingleton(state);
            services.AddSingleton<IComponentActivator>(activator);
            services.AddSingleton<IJSRuntime, UnusedJsRuntime>();
            await using var provider = services.BuildServiceProvider();
            await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
            await renderer.Dispatcher.InvokeAsync(async () =>
            {
                var root = await renderer.RenderComponentAsync<T>();
                ReadHtml = () => WebUtility.HtmlDecode(root.ToHtmlString());
                await action(Assert.Single(activator.Components.OfType<T>()), state);
            });
        }

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
            if (PendingResponse is { } pending && path != "/api/jobs")
            {
                PendingResponse = null;
                PendingRequestStarted.TrySetResult(path);
                return await pending.Task;
            }
            if (request.Method == HttpMethod.Get)
            {
                GetPaths.Add(path);
                if (FailLocales && path.StartsWith("/api/master-data/", StringComparison.Ordinal))
                    return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                    { Content = JsonContent.Create(ApiResponseEnvelope<object>.Failure("qa", [new ApiError("Unavailable", "選択肢を取得できません。")])) };
                if (path == "/api/master-data/locations") return Reply(new[] { new LocationSummary("rakko_keyword", "JP", "Japan", "JP", "active") });
                if (path == "/api/master-data/languages") return Reply(new[] { new LanguageSummary("rakko_keyword", "ja", "Japanese", "active") });
                if (path == "/api/projects")
                {
                    return Reply(new[] { ProjectId, OtherProjectId }.Select(projectId =>
                        new ProjectDetails(projectId, Guid.NewGuid(), "QA", "JP", "ja",
                            JsonSerializer.SerializeToElement(new { }), null, "active", DateTime.UtcNow, DateTime.UtcNow, null)).ToArray());
                }
                if (path == "/api/jobs") return Reply(ExportCreated
                    ? new[] { Job() with { JobType = "DataExportJob", ResultResource = new JobResultResource("data_export", ExportId) } }
                    : Array.Empty<JobDetails>());
                if (path.EndsWith("/dashboard", StringComparison.Ordinal)) return Reply(Dashboard);
                if (path.EndsWith("/briefs", StringComparison.Ordinal)) return Reply(Array.Empty<ArticleBriefSummary>());
                if (path.EndsWith($"/briefs/{BriefId:D}", StringComparison.Ordinal)) return Reply(Brief());
                if (path.EndsWith($"/briefs/{BriefId:D}/versions", StringComparison.Ordinal)) return Reply(Array.Empty<ArticleBriefVersionDetails>());
                if (path == $"/api/jobs/{jobId:D}")
                {
                    return Reply(Job());
                }
                if (path.EndsWith($"/search-volume/jobs/{jobId:D}/results", StringComparison.Ordinal))
                {
                    if (FailResults) return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                    { Content = JsonContent.Create(ApiResponseEnvelope<object>.Failure("qa", [new ApiError("Unavailable", "結果を取得できません。")])) };
                    ResultsRequested.TrySetResult();
                    return Reply(new[] { new SearchVolumeResultRow("QA volume", 900, null, null, null) });
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
                if (path.EndsWith("/exports/csv", StringComparison.Ordinal)) ExportCreated = true;
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

        public static HttpResponseMessage Reply<T>(T data)
            => new(HttpStatusCode.OK) { Content = JsonContent.Create(ApiResponseEnvelope<T>.Success("qa", data)) };

        protected override void Dispose(bool disposing)
        {
            if (disposing) http.Dispose();
            base.Dispose(disposing);
        }
    }

    private sealed class UnusedJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw new NotSupportedException();
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken token, object?[]? args) => throw new NotSupportedException();
    }
}
