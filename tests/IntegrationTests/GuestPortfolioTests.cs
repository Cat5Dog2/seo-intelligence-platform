using System.Net;
using System.Reflection;
using SeoIntelligence.Application.Services;
using SeoIntelligence.Infrastructure.Persistence;
using SeoIntelligence.Web.Components.Pages;
using SeoIntelligence.Web.Services;

namespace IntegrationTests;

public sealed class GuestPortfolioTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void NewProjectFormAndGuestMasterDataUseTheCanonicalLocaleNames()
    {
        // Rakko Keyword API v1.12.0 takes metadata names, and rewrite metrics match the project locale exactly.
        var form = typeof(Home).GetProperty("ProjectForm", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(new Home())!;
        Assert.Equal(SeoIntelligenceSeedData.DefaultLocation, form.GetType().GetProperty("DefaultLocation")!.GetValue(form));
        Assert.Equal(SeoIntelligenceSeedData.DefaultLanguage, form.GetType().GetProperty("DefaultLanguage")!.GetValue(form));

        // The guest lists must offer the same value, otherwise the locale selectors show a second "日本".
        var session = new GuestDemoSession(DateTimeOffset.UtcNow.AddHours(1));
        var location = Assert.Single(session.Execute<IReadOnlyList<LocationSummary>>(HttpMethod.Get, "/api/master-data/locations", null).Data!);
        var language = Assert.Single(session.Execute<IReadOnlyList<LanguageSummary>>(HttpMethod.Get, "/api/master-data/languages", null).Data!);
        Assert.Equal(SeoIntelligenceSeedData.DefaultLocation, location.Code);
        Assert.Equal(SeoIntelligenceSeedData.DefaultLanguage, language.Code);
        var project = Assert.Single(session.Execute<IReadOnlyList<ProjectDetails>>(HttpMethod.Get, "/api/projects", null).Data!);
        Assert.Equal(location.Code, project.DefaultLocation);
        Assert.Equal(language.Code, project.DefaultLanguage);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void PopulatedSamplesRespectVisibleAnalysisFilters()
    {
        var session = new GuestDemoSession(DateTimeOffset.UtcNow.AddHours(1));
        var project = Assert.Single(session.Execute<IReadOnlyList<ProjectDetails>>(HttpMethod.Get, "/api/projects", null).Data!);
        var prefix = $"/api/projects/{project.ProjectId}";
        var ranks = session.Execute<RankResultList>(HttpMethod.Get, prefix + "/rank-results?minPosition=15&maxPosition=22&sortBy=position&orderBy=desc", null).Data!;
        Assert.NotEmpty(ranks.Items);
        Assert.All(ranks.Items, row => Assert.InRange(row.Position, 15, 22));
        Assert.Equal(ranks.Items.OrderByDescending(row => row.Position).Select(row => row.Position), ranks.Items.Select(row => row.Position));
        var keywords = session.Execute<IReadOnlyList<InfluxKeywordResultRow>>(HttpMethod.Get, prefix + "/influx-keywords?target=guide-1.example&minRank=4&maxRank=5", null).Data!;
        Assert.Equal(2, keywords.Count);
        Assert.All(keywords, row => { Assert.Equal("guide-1.example", row.Target); Assert.InRange(row.Rank, 4, 5); });
        var clusters = session.Execute<IReadOnlyList<TopicClusterSummary>>(HttpMethod.Get, prefix + "/clusters?intentLabel=commercial", null).Data!;
        Assert.Empty(clusters);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CandidateCsvIncludesOnlyTheSelectedExploration()
    {
        var session = new GuestDemoSession(DateTimeOffset.UtcNow.AddHours(1));
        var project = Assert.Single(session.Execute<IReadOnlyList<ProjectDetails>>(HttpMethod.Get, "/api/projects", null).Data!);
        var prefix = $"/api/projects/{project.ProjectId}";
        var other = session.Execute<KeywordDiscoveryResult>(HttpMethod.Post, prefix + "/keyword-discovery/suggest", new KeywordDiscoveryRequest(SeedKeyword: "紅茶")).Data!;
        var exploration = session.Execute<KeywordDiscoveryResult>(HttpMethod.Post, prefix + "/keyword-discovery/suggest", new KeywordDiscoveryRequest(SeedKeyword: "珈琲")).Data!;
        var export = session.Execute<JobReference>(HttpMethod.Post, prefix + "/exports/csv", new DataExportRequest("keyword_candidates",
            System.Text.Json.JsonSerializer.SerializeToElement(new { jobId = exploration.JobId }))).Data!;
        var job = session.Execute<SeoIntelligence.Application.Jobs.JobDetails>(HttpMethod.Get, $"/api/jobs/{export.JobId}", null).Data!;
        using var file = session.Execute<ApiFileResponse>(HttpMethod.Get, $"{prefix}/exports/{job.ResultResource!.ResourceId}/content", null).Data!;
        using var reader = new StreamReader(file.Content);
        var csv = await reader.ReadToEndAsync();
        Assert.Equal(6, csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length);
        Assert.All(exploration.Candidates, row => Assert.Contains(row.Keyword, csv));
        Assert.All(other.Candidates, row => Assert.DoesNotContain(row.Keyword, csv));
        var invalid = session.Execute<JobReference>(HttpMethod.Post, prefix + "/exports/csv", new DataExportRequest("keyword_candidates",
            System.Text.Json.JsonSerializer.SerializeToElement(new { jobId = Guid.NewGuid() })));
        Assert.Equal(HttpStatusCode.NotFound, invalid.StatusCode);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void NewDemoContainsConnectedResearchAndContentWithMatchingDashboardCounts()
    {
        var session = new GuestDemoSession(DateTimeOffset.UtcNow.AddHours(1));
        var project = Assert.Single(session.Execute<IReadOnlyList<ProjectDetails>>(HttpMethod.Get, "/api/projects", null).Data!);
        var prefix = $"/api/projects/{project.ProjectId}";
        var dashboard = session.Execute<DashboardSnapshot>(HttpMethod.Get, prefix + "/dashboard", null).Data!;
        Assert.Equal(15, dashboard.KeywordCandidateCount);
        Assert.Equal(15, dashboard.SearchVolumeResultCount);
        Assert.Equal(0, dashboard.ConsumedCredit);
        Assert.NotEmpty(session.Execute<IReadOnlyList<SiteDetails>>(HttpMethod.Get, prefix + "/sites", null).Data!);

        var competitors = session.Execute<IReadOnlyList<CompetitorResultRow>>(HttpMethod.Get, prefix + "/competitors", null).Data!;
        var briefs = session.Execute<IReadOnlyList<ArticleBriefSummary>>(HttpMethod.Get, prefix + "/briefs", null).Data!;
        var clusters = session.Execute<IReadOnlyList<TopicClusterSummary>>(HttpMethod.Get, prefix + "/clusters", null).Data!;
        var analyses = session.Execute<IReadOnlyList<ContentAnalysisResultRow>>(HttpMethod.Get, prefix + "/content-analyses", null).Data!;
        Assert.Equal(3, competitors.Count);
        Assert.Equal(3, briefs.Count);
        Assert.Equal(3, clusters.Count);
        Assert.Equal(briefs.Count, dashboard.BriefSummary!.BriefCount);
        Assert.Equal(competitors.Count, dashboard.CompetitorSummary!.CompetitorCount);
        Assert.Equal(analyses.Count, dashboard.ContentAnalysisSummary!.KeywordCount);
        Assert.All(analyses, analysis => Assert.NotEmpty(analysis.CoOccurrences));
        Assert.All(clusters, cluster =>
        {
            var detail = session.Execute<TopicClusterDetails>(HttpMethod.Get, $"{prefix}/clusters/{cluster.ClusterId}", null).Data!;
            Assert.True(detail.Keywords.Count > 1);
            Assert.NotEmpty(detail.InternalLinkCandidates);
            Assert.Contains(detail.ArticleCandidates, candidate => briefs.Any(brief => brief.BriefId == candidate.BriefId));
        });
        Assert.NotEmpty(session.Execute<IReadOnlyList<CannibalizationCandidateDetails>>(HttpMethod.Get, prefix + "/cannibalization/candidates", null).Data!);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void AdvertisedSampleSizeMatchesTheSeededDemo()
    {
        var session = new GuestDemoSession(DateTimeOffset.UtcNow.AddHours(1));
        var project = Assert.Single(session.Execute<IReadOnlyList<ProjectDetails>>(HttpMethod.Get, "/api/projects", null).Data!);
        var prefix = $"/api/projects/{project.ProjectId}";
        var dashboard = session.Execute<DashboardSnapshot>(HttpMethod.Get, prefix + "/dashboard", null).Data!;
        var clusters = session.Execute<IReadOnlyList<TopicClusterSummary>>(HttpMethod.Get, prefix + "/clusters", null).Data!;
        Assert.Equal(new GuestDemoSession.DemoSampleSize(clusters.Count, dashboard.KeywordCandidateCount,
            dashboard.CompetitorSummary!.CompetitorCount, dashboard.BriefSummary!.BriefCount), GuestDemoSession.SampleSize);
    }

    [Theory]
    [InlineData(true, 3)]
    [InlineData(false, 1)]
    [InlineData(false, 24)]
    [Trait("Category", "Unit")]
    public void MockVolumeHonorsDifficultyAndRequestedMonthCount(bool difficulty, int months)
    {
        var session = new GuestDemoSession(DateTimeOffset.UtcNow.AddHours(1));
        var project = Assert.Single(session.Execute<IReadOnlyList<ProjectDetails>>(HttpMethod.Get, "/api/projects", null).Data!);
        var prefix = $"/api/projects/{project.ProjectId}";
        var job = session.Execute<JobReference>(HttpMethod.Post, prefix + "/search-volume/jobs",
            new SearchVolumeJobRequest(["条件検証"], "jp", "ja", months, difficulty)).Data!;
        var row = Assert.Single(session.Execute<IReadOnlyList<SearchVolumeResultRow>>(HttpMethod.Get, $"{prefix}/search-volume/jobs/{job.JobId}/results", null).Data!);
        Assert.Equal(difficulty ? 30m : null, row.SeoDifficulty);
        Assert.NotNull(row.MonthlySearchVolume);
        Assert.Equal(months, row.MonthlySearchVolume.Count);
        Assert.All(row.MonthlySearchVolume, month => Assert.True(month.Value > 0));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(25)]
    [Trait("Category", "Unit")]
    public void MockVolumeRejectsUnsupportedMonthCounts(int months)
    {
        var session = new GuestDemoSession(DateTimeOffset.UtcNow.AddHours(1));
        var project = Assert.Single(session.Execute<IReadOnlyList<ProjectDetails>>(HttpMethod.Get, "/api/projects", null).Data!);
        var result = session.Execute<JobReference>(HttpMethod.Post, $"/api/projects/{project.ProjectId}/search-volume/jobs",
            new SearchVolumeJobRequest(["条件検証"], "jp", "ja", months));
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
    }
}
