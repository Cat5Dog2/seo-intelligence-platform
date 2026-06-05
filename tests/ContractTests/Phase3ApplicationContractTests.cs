using System.Text.Json;
using SeoIntelligence.Application.Services;

namespace ContractTests;

public sealed class Phase3ApplicationContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    [Trait("Category", "Contract")]
    public void Phase3RequestContractsSerializeExpectedCamelCaseFieldsWithoutSecretValues()
    {
        using var report = JsonDocument.Parse(JsonSerializer.Serialize(
            new ReportRequest(
                ReportType: "monthly",
                Period: "2026-06",
                Format: "pdf",
                Sections: ["summary", "rankings"],
                ShareExpiresAt: DateTimeOffset.Parse("2026-07-01T00:00:00Z")),
            JsonOptions));
        Assert.Equal("monthly", report.RootElement.GetProperty("reportType").GetString());
        Assert.Equal("rankings", report.RootElement.GetProperty("sections")[1].GetString());
        Assert.True(report.RootElement.TryGetProperty("shareExpiresAt", out _));

        using var sharedReport = JsonDocument.Parse(JsonSerializer.Serialize(
            new ReportShareAccessDetails(
                ReportId: Guid.Parse("018f4dd8-0101-7000-8000-000000000101"),
                ReportType: "monthly",
                Period: "2026-06",
                Format: "pdf",
                DownloadUrl: "storage://local/reports/report.pdf?expiresAt=2026-06-05T00%3A15%3A00Z",
                DownloadExpiresAt: DateTimeOffset.Parse("2026-06-05T00:15:00Z")),
            JsonOptions));
        Assert.Equal("monthly", sharedReport.RootElement.GetProperty("reportType").GetString());
        Assert.Equal("pdf", sharedReport.RootElement.GetProperty("format").GetString());
        Assert.True(sharedReport.RootElement.TryGetProperty("downloadUrl", out _));

        using var connector = JsonDocument.Parse(JsonSerializer.Serialize(
            new ConnectorSettingsRequest(
                ConnectorType: "gsc",
                Name: "GSC stub",
                AuthRef: "gsc-oauth-ref",
                Settings: JsonSerializer.SerializeToElement(new { siteUrl = "https://example.com" }),
                Status: "active"),
            JsonOptions));
        Assert.Equal("gsc-oauth-ref", connector.RootElement.GetProperty("authRef").GetString());
        Assert.False(connector.RootElement.TryGetProperty("secretValue", out _));
        Assert.False(connector.RootElement.TryGetProperty("oauthToken", out _));

        using var ai = JsonDocument.Parse(JsonSerializer.Serialize(
            new AiChatRequest(
                Message: "Find rewrite priorities.",
                ConversationId: Guid.Parse("018f4dd8-0001-7000-8000-000000000001"),
                AllowedTools: ["rank-results"],
                ReferenceScope: JsonSerializer.SerializeToElement(new { projectOnly = true })),
            JsonOptions));
        Assert.Equal("Find rewrite priorities.", ai.RootElement.GetProperty("message").GetString());
        Assert.Equal("rank-results", ai.RootElement.GetProperty("allowedTools")[0].GetString());
        Assert.True(ai.RootElement.GetProperty("referenceScope").GetProperty("projectOnly").GetBoolean());

        using var candidate = JsonDocument.Parse(JsonSerializer.Serialize(
            new CannibalizationCandidateDetails(
                CandidateId: Guid.Parse("018f4dd8-0002-7000-8000-000000000002"),
                ProjectId: Guid.Parse("018f4dd8-0003-7000-8000-000000000003"),
                KeywordId: Guid.Parse("018f4dd8-0004-7000-8000-000000000004"),
                Keyword: "seo rewrite",
                PrimaryUrl: "https://example.com/seo-guide",
                CompetingUrls: JsonSerializer.SerializeToElement(new[] { new { url = "https://example.com/seo-tips" } }),
                SeverityScore: 91.25m,
                Evidence: JsonSerializer.SerializeToElement(new { rankSpread = 2 }),
                Recommendation: JsonSerializer.SerializeToElement(new { action = "consolidate_or_canonicalize" }),
                Status: "active",
                DetectedAt: DateTime.Parse("2026-06-05T00:00:00Z")),
            JsonOptions));
        Assert.Equal("seo rewrite", candidate.RootElement.GetProperty("keyword").GetString());
        Assert.Equal("consolidate_or_canonicalize", candidate.RootElement.GetProperty("recommendation").GetProperty("action").GetString());
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void Phase3DashboardSummariesDeserializeAsOptionalNestedContracts()
    {
        var dashboard = JsonSerializer.Deserialize<DashboardSnapshot>(
            """
            {
              "keywordCandidateCount": 0,
              "runningJobCount": 0,
              "failedJobCount": 0,
              "consumedCredit": 0,
              "rewriteSummary": {
                "taskCount": 4,
                "activeTaskCount": 3,
                "maxPriorityScore": 88.5
              },
              "cannibalizationSummary": {
                "candidateCount": 2,
                "activeCandidateCount": 1,
                "maxSeverityScore": 77.25
              },
              "reportSummary": {
                "reportCount": 5,
                "sharedReportCount": 2,
                "expiredShareCount": 1
              },
              "aiSummary": {
                "sessionCount": 3,
                "messageCount": 12,
                "pendingReviewCount": 4
              }
            }
            """,
            JsonOptions)!;

        Assert.Equal(4, dashboard.RewriteSummary!.TaskCount);
        Assert.Equal(77.25m, dashboard.CannibalizationSummary!.MaxSeverityScore);
        Assert.Equal(2, dashboard.ReportSummary!.SharedReportCount);
        Assert.Equal(12, dashboard.AiSummary!.MessageCount);
    }
}
