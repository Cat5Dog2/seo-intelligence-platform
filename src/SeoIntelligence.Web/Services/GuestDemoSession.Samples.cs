using System.Text.Json;
using SeoIntelligence.Application.Services;

namespace SeoIntelligence.Web.Services;

public sealed partial class GuestDemoSession
{
    private readonly Dictionary<Guid, (Guid KeywordId, Guid ResourceId)> _sampleIds = [];
    private readonly DateTime _sampleTime = DateTime.UtcNow;

    private ApiClientResult<T> ReadSample<T>(string route, Guid projectId, Dictionary<string, string> query)
    {
        if (!_sampleIds.TryGetValue(projectId, out var ids))
        {
            ids = (Guid.NewGuid(), Guid.NewGuid());
            _sampleIds.Add(projectId, ids);
        }
        var sampleKeywordId = ids.KeywordId;
        var sampleResourceId = ids.ResourceId;
        var sampleMetrics = JsonSerializer.SerializeToElement(new { searchVolume = 1200, seoDifficulty = 30, dataSource = "Mock" });
        const string keyword = "SEO 対策";
        const string domain = "example.com";
        const string url = "https://example.com/seo";
        RewriteTaskDetails RewriteSample() => new(sampleResourceId, projectId, url, 80,
            JsonSerializer.SerializeToElement(new { summary = "検索意図に合わせて見出しを改善" }),
            "active", "guest", "デモの提案です。", _sampleTime, _sampleTime);
        switch (route)
        {
            case "competitors":
                return Page<T, CompetitorResultRow>([new(sampleResourceId, domain, 35, 2400, 120, 120, 40, 80, 60, false, _sampleTime)], query, row => row.Domain, row => row.DuplicateRate);
            case "influx-keywords":
                return Page<T, InfluxKeywordResultRow>([new(sampleResourceId, sampleKeywordId, domain, keyword, 5, url, 250, sampleMetrics, true, "competitor_only", _sampleTime)], query, row => row.Keyword, row => row.Rank);
            case "influx-pages":
                return Page<T, InfluxPageResultRow>([new(sampleResourceId, domain, url, "SEO入門ガイド（デモ）", 12, 250, 100, sampleKeywordId, keyword, _sampleTime)], query, row => row.Title, row => row.EstimatedTraffic);
            case "content-analyses":
                return Page<T, ContentAnalysisResultRow>([new(sampleKeywordId, keyword,
                    [new(sampleResourceId, url, domain, "SEO入門ガイド（デモ）", "検索意図とコンテンツ改善のサンプルです。", 250, 100, sampleKeywordId, keyword, _sampleTime)],
                    [new(sampleResourceId, 1, url, "SEO入門ガイド（デモ）", "デモの見出し構成", 2, 1200,
                        [new(sampleKeywordId, 2, "検索意図を理解する", 1), new(sampleResourceId, 2, "記事を改善する", 2)], _sampleTime)], [], _sampleTime)], query, row => row.Keyword, row => row.LastAnalyzedAt);
            case "rank-results":
                return Ok<T>(new RankResultList([new(sampleResourceId, sampleResourceId, sampleKeywordId, keyword, domain, 5, 8, 3, url, 250, sampleMetrics, "guest-mock", _sampleTime)], new(0, 1, 0, 0, 0, 0), 1, 50, 1, 1));
            case "alerts":
                return Ok<T>(Array.Empty<RankAlertDetails>());
            case "alert-events":
                return Ok<T>(Array.Empty<RankAlertEventDetails>());
            case "clusters":
                return Page<T, TopicClusterSummary>([new(sampleResourceId, projectId, "SEOの基礎（デモ）", null, null, sampleKeywordId, keyword, 80, 1, "informational", 0, [], [], _sampleTime, _sampleTime)], query, row => row.Name, row => row.Score);
            case "briefs":
                ArticleBriefSummary[] briefs = [new(sampleResourceId, projectId, null, "SEO入門記事（デモ）", sampleKeywordId, keyword, 1, SampleBrief(), "pending", "active", _sampleTime, _sampleTime)];
                var reviewStatus = query.GetValueOrDefault("reviewStatus");
                return Page<T, ArticleBriefSummary>(briefs.Where(row => string.IsNullOrWhiteSpace(reviewStatus) || row.ReviewStatus == reviewStatus), query, row => row.Title, row => row.UpdatedAt);
            case "rewrite/tasks":
                return Page<T, RewriteTaskDetails>([RewriteSample()], query, row => row.TargetUrl, row => row.PriorityScore);
            case "cannibalization/candidates":
                return Ok<T>(Array.Empty<CannibalizationCandidateDetails>());
            case "reports":
                return Ok<T>(Array.Empty<ReportDetails>());
            case "connectors":
                return Ok<T>(Array.Empty<ConnectorSettingsDetails>());
        }
        if (route == $"clusters/{sampleResourceId}")
            return Ok<T>(new TopicClusterDetails(sampleResourceId, projectId, "SEOの基礎（デモ）", null, null, sampleKeywordId, keyword, 80, 1, "informational",
                [new(sampleKeywordId, keyword, "representative", 80, "informational", new(1, 0, 0, ["Mock"]))], [], [], [], _sampleTime, _sampleTime));
        if (route == $"briefs/{sampleResourceId}")
            return Ok<T>(new ArticleBriefDetails(sampleResourceId, projectId, null, "SEO入門記事（デモ）", sampleKeywordId, keyword, 1, SampleBrief(), "pending", "active", _sampleTime, _sampleTime));
        if (route == $"briefs/{sampleResourceId}/versions")
            return Ok<T>(new ArticleBriefVersionDetails[] { new(sampleResourceId, 1, "guest-mock", null, SampleBrief(), "guest", "pending", "初期デモ", _sampleTime) });
        if (route == $"rewrite/tasks/{sampleResourceId}")
            return Ok<T>(RewriteSample());
        return Missing<T>();
    }

    private static JsonElement SampleBrief() => JsonSerializer.SerializeToElement(new
    {
        summary = "SEOを始める読者向けのサンプル構成です。",
        targetKeyword = "SEO 対策",
        searchIntent = "informational",
        headings = new[] { "SEOとは", "検索意図を調べる", "改善の効果を測る" },
        dataSource = "Mock"
    });
}
