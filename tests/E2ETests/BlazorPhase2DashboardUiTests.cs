using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using SeoIntelligence.Application.Services;
using SeoIntelligence.Contracts.Api;
using SeoIntelligence.Web.Services;

namespace E2ETests;

public sealed class BlazorPhase2DashboardUiTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    [Trait("Category", "E2E")]
    public async Task DashboardClientDeserializesPhase2SummariesAndUsesProjectScopedEndpoint()
    {
        var projectId = Guid.NewGuid();
        using var handler = new RecordingHandler((_, _) =>
            JsonResponse(
                HttpStatusCode.OK,
                ApiResponseEnvelope<DashboardSnapshot>.Success(
                    "req-ui-dashboard",
                    new DashboardSnapshot(
                        KeywordCandidateCount: 12,
                        RunningJobCount: 1,
                        FailedJobCount: 0,
                        ConsumedCredit: 7,
                        KeywordDiscoveryCount: 2,
                        SearchVolumeJobCount: 3,
                        SearchVolumeResultCount: 4,
                        OpportunityScoreCount: 5,
                        TopOpportunityScores: [],
                        NotificationFailureCount: 0,
                        CompetitorSummary: new DashboardCompetitorSummary(
                            CompetitorCount: 2,
                            SavedCompetitorCount: 1,
                            AverageDuplicateRate: 0.25m,
                            EstimatedTraffic: 123m,
                            TrafficValue: 456m),
                        InfluxSummary: new DashboardInfluxSummary(
                            KeywordCount: 8,
                            GapKeywordCount: 3,
                            PageCount: 4,
                            EstimatedTraffic: 789m,
                            TrafficValue: 321m),
                        ContentAnalysisSummary: new DashboardContentAnalysisSummary(
                            KeywordCount: 2,
                            ContentResultCount: 5,
                            HeadlinePageCount: 6,
                            CoOccurrenceWordCount: 7),
                        BriefSummary: new DashboardBriefSummary(
                            BriefCount: 4,
                            DraftCount: 2,
                            PendingReviewCount: 1,
                            ReviewedCount: 1),
                        RankSummary: new DashboardRankSummary(
                            RankCheckJobCount: 3,
                            RankResultCount: 9,
                            Distribution: new RankDistribution(
                                Top3: 1,
                                Top10: 2,
                                Top20: 3,
                                Top50: 1,
                                Top100: 1,
                                OutOfRange: 1)),
                        RankAlertSummary: new DashboardRankAlertSummary(
                            ActiveAlertCount: 2,
                            UnresolvedEventCount: 1,
                            RankAlertNotificationCount: 1)))));
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://localhost")
        };
        var client = new SeoIntelligenceApiClient(
            httpClient,
            NullLogger<SeoIntelligenceApiClient>.Instance);

        var result = await client.GetDashboardAsync(projectId);

        Assert.True(result.IsSuccess, result.ErrorSummary);
        Assert.Equal($"/api/projects/{projectId:D}/dashboard", handler.RequestUris.Single());
        var snapshot = result.Data!;
        Assert.Equal(2, snapshot.CompetitorSummary!.CompetitorCount);
        Assert.Equal(3, snapshot.InfluxSummary!.GapKeywordCount);
        Assert.Equal(2, snapshot.ContentAnalysisSummary!.KeywordCount);
        Assert.Equal(4, snapshot.BriefSummary!.BriefCount);
        Assert.Equal(9, snapshot.RankSummary!.RankResultCount);
        Assert.Equal(2, snapshot.RankSummary.Distribution.Top10);
        Assert.Equal(1, snapshot.RankAlertSummary!.UnresolvedEventCount);
    }

    private static HttpResponseMessage JsonResponse<T>(HttpStatusCode statusCode, ApiResponseEnvelope<T> envelope)
        => new(statusCode)
        {
            Content = JsonContent.Create(envelope, options: JsonOptions)
        };

    private sealed class RecordingHandler : HttpMessageHandler, IDisposable
    {
        private readonly Func<HttpRequestMessage, string, HttpResponseMessage> responseFactory;

        public RecordingHandler(Func<HttpRequestMessage, string, HttpResponseMessage> responseFactory)
        {
            this.responseFactory = responseFactory;
        }

        public List<string> RequestUris { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            RequestUris.Add(request.RequestUri?.PathAndQuery ?? string.Empty);
            return responseFactory(request, body);
        }
    }
}
