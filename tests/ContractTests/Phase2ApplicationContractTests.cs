using System.Text.Json;
using SeoIntelligence.Application.Services;

namespace ContractTests;

public sealed class Phase2ApplicationContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    [Trait("Category", "Contract")]
    public void Phase2RequestContractsSerializeExpectedCamelCaseFields()
    {
        using var competitor = JsonDocument.Parse(JsonSerializer.Serialize(
            new CompetitorAnalyzeRequest("https://own.example"),
            JsonOptions));
        Assert.Equal("https://own.example", competitor.RootElement.GetProperty("target").GetString());
        Assert.True(competitor.RootElement.TryGetProperty("siteId", out _));

        using var content = JsonDocument.Parse(JsonSerializer.Serialize(
            new ContentAnalyzeRequest(
                Keyword: "seo content",
                IncludeContentSearch: true,
                IncludeHeadline: true,
                IncludeCoOccurrence: true,
                Limit: 5),
            JsonOptions));
        Assert.Equal("seo content", content.RootElement.GetProperty("keyword").GetString());
        Assert.True(content.RootElement.GetProperty("includeContentSearch").GetBoolean());
        Assert.Equal(5, content.RootElement.GetProperty("limit").GetInt32());

        using var brief = JsonDocument.Parse(JsonSerializer.Serialize(
            new GenerateBriefRequest(
                TargetKeyword: "seo content",
                CompetitorUrls: ["https://competitor.example/seo"]),
            JsonOptions));
        Assert.Equal("seo content", brief.RootElement.GetProperty("targetKeyword").GetString());
        Assert.Equal("https://competitor.example/seo", brief.RootElement.GetProperty("competitorUrls")[0].GetString());

        using var rank = JsonDocument.Parse(JsonSerializer.Serialize(
            new RankCheckJobRequest(
                Keywords: ["seo"],
                Targets: [new RankCheckTargetRequest("https://example.com", "domain")],
                MatchType: "domain",
                Depth: 100,
                WithMetrics: true,
                Deduplicate: true),
            JsonOptions));
        Assert.Equal("seo", rank.RootElement.GetProperty("keywords")[0].GetString());
        Assert.Equal("https://example.com", rank.RootElement.GetProperty("targets")[0].GetProperty("target").GetString());
        Assert.Equal("domain", rank.RootElement.GetProperty("targets")[0].GetProperty("targetType").GetString());
        Assert.Equal(100, rank.RootElement.GetProperty("depth").GetInt32());
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void Phase2ResponseContractsDeserializeNestedDashboardBriefAndRankShapes()
    {
        var dashboard = JsonSerializer.Deserialize<DashboardSnapshot>(
            """
            {
              "keywordCandidateCount": 0,
              "runningJobCount": 0,
              "failedJobCount": 0,
              "consumedCredit": 0,
              "competitorSummary": {
                "competitorCount": 2,
                "savedCompetitorCount": 1,
                "averageDuplicateRate": 0.25,
                "estimatedTraffic": 123.5,
                "trafficValue": 456.7
              },
              "influxSummary": {
                "keywordCount": 8,
                "gapKeywordCount": 3,
                "pageCount": 4,
                "estimatedTraffic": 789.1,
                "trafficValue": 321.2
              },
              "contentAnalysisSummary": {
                "keywordCount": 2,
                "contentResultCount": 5,
                "headlinePageCount": 6,
                "coOccurrenceWordCount": 7
              },
              "briefSummary": {
                "briefCount": 4,
                "draftCount": 2,
                "pendingReviewCount": 1,
                "reviewedCount": 1
              },
              "rankSummary": {
                "rankCheckJobCount": 3,
                "rankResultCount": 9,
                "distribution": {
                  "top3": 1,
                  "top10": 2,
                  "top20": 3,
                  "top50": 1,
                  "top100": 1,
                  "outOfRange": 1
                }
              },
              "rankAlertSummary": {
                "activeAlertCount": 2,
                "unresolvedEventCount": 1,
                "rankAlertNotificationCount": 1
              }
            }
            """,
            JsonOptions)!;
        Assert.Equal(2, dashboard.CompetitorSummary!.CompetitorCount);
        Assert.Equal(3, dashboard.InfluxSummary!.GapKeywordCount);
        Assert.Equal(7, dashboard.ContentAnalysisSummary!.CoOccurrenceWordCount);
        Assert.Equal(4, dashboard.BriefSummary!.BriefCount);
        Assert.Equal(9, dashboard.RankSummary!.RankResultCount);
        Assert.Equal(1, dashboard.RankAlertSummary!.UnresolvedEventCount);

        var brief = JsonSerializer.Deserialize<ArticleBriefDetails>(
            """
            {
              "briefId": "018f3f12-0001-7000-8000-000000000001",
              "projectId": "018f3f12-0001-7000-8000-000000000002",
              "clusterId": null,
              "title": "SEO content brief",
              "targetKeywordId": "018f3f12-0001-7000-8000-000000000003",
              "targetKeyword": "seo content",
              "currentVersion": 2,
              "content": {
                "targetKeyword": "seo content",
                "requiredTerms": ["search intent"]
              },
              "reviewStatus": "reviewed",
              "status": "draft",
              "createdAt": "2026-06-01T00:00:00Z",
              "updatedAt": "2026-06-02T00:00:00Z"
            }
            """,
            JsonOptions)!;
        Assert.Equal("seo content", brief.Content.GetProperty("targetKeyword").GetString());
        Assert.Equal("reviewed", brief.ReviewStatus);

        var rankResults = JsonSerializer.Deserialize<RankResultList>(
            """
            {
              "items": [
                {
                  "rankResultId": "018f3f12-0002-7000-8000-000000000001",
                  "jobId": "018f3f12-0002-7000-8000-000000000002",
                  "keywordId": "018f3f12-0002-7000-8000-000000000003",
                  "keyword": "seo",
                  "target": "example.com",
                  "position": 8,
                  "previousPosition": 3,
                  "positionDelta": 5,
                  "rankedUrl": "https://example.com/seo",
                  "estimatedTraffic": 75.5,
                  "metrics": { "searchVolume": 1200 },
                  "contractScopeKey": "rakko-standard-internal-2026",
                  "checkedAt": "2026-06-01T00:00:00Z"
                }
              ],
              "distribution": {
                "top3": 0,
                "top10": 1,
                "top20": 0,
                "top50": 0,
                "top100": 0,
                "outOfRange": 0
              },
              "page": 1,
              "pageSize": 100,
              "totalCount": 1,
              "totalPages": 1
            }
            """,
            JsonOptions)!;
        Assert.Equal("seo", rankResults.Items.Single().Keyword);
        Assert.Equal(1, rankResults.Distribution.Top10);

        var alertEvent = JsonSerializer.Deserialize<RankAlertEventDetails>(
            """
            {
              "alertEventId": "018f3f12-0003-7000-8000-000000000001",
              "alertId": "018f3f12-0003-7000-8000-000000000002",
              "projectId": "018f3f12-0003-7000-8000-000000000003",
              "jobId": "018f3f12-0003-7000-8000-000000000004",
              "keywordId": "018f3f12-0003-7000-8000-000000000005",
              "keyword": "seo",
              "eventType": "rank_drop",
              "previousValue": { "position": 3 },
              "currentValue": { "position": 8 },
              "evidence": { "delta": 5 },
              "notificationDeliveryId": "018f3f12-0003-7000-8000-000000000006",
              "triggeredAt": "2026-06-01T00:00:00Z",
              "resolvedAt": null
            }
            """,
            JsonOptions)!;
        Assert.Equal("rank_drop", alertEvent.EventType);
        Assert.Equal(3, alertEvent.PreviousValue.GetProperty("position").GetInt32());
        Assert.NotNull(alertEvent.NotificationDeliveryId);
    }
}
