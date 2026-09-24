using System.Net;
using System.Security.Claims;
using System.Text.Json;
using SeoIntelligence.Application.Jobs;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging.Abstractions;
using SeoIntelligence.Application.Security;
using SeoIntelligence.Application.Services;
using SeoIntelligence.Web.Security;
using SeoIntelligence.Web.Services;

namespace IntegrationTests;

public sealed class GuestDemoSessionTests
{
    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("", "", 1)]
    [InlineData("pending", "SEO", 1)]
    [InlineData("reviewed", "", 0)]
    [InlineData("rejected", "", 0)]
    [InlineData("pending", "no-match", 0)]
    public void DemoBriefFiltersRespectReviewStateAndSearchText(string reviewStatus, string query, int expectedCount)
    {
        var session = NewSession();
        var project = Assert.Single(session.Execute<IReadOnlyList<ProjectDetails>>(HttpMethod.Get, "/api/projects", null).Data!);
        var result = session.Execute<IReadOnlyList<ArticleBriefSummary>>(HttpMethod.Get,
            $"/api/projects/{project.ProjectId}/briefs?reviewStatus={reviewStatus}&q={query}", null);
        Assert.True(result.IsSuccess);
        Assert.Equal(expectedCount, result.Data!.Count);
        Assert.Equal(expectedCount, result.Meta.Page!.TotalCount);
        Assert.All(result.Data, row => Assert.Equal("pending", row.ReviewStatus));
    }

    [Fact]
    [Trait("Category", "UI")]
    public void RecentDemoJobsUseNewestFirstBeforePagination()
    {
        var session = NewSession();
        var project = Assert.Single(session.Execute<IReadOnlyList<ProjectDetails>>(HttpMethod.Get, "/api/projects", null).Data!);
        var jobs = new List<JobDetails>();
        for (var index = 0; index < 6; index++)
        {
            var reference = session.Execute<JobReference>(HttpMethod.Post, $"/api/projects/{project.ProjectId}/search-volume/jobs",
                new SearchVolumeJobRequest([$"keyword {index}"], "jp", "ja")).Data!;
            jobs.Add(session.Execute<JobDetails>(HttpMethod.Get, $"/api/jobs/{reference.JobId}", null).Data!);
        }
        var path = $"/api/jobs?project_id={project.ProjectId}&pageSize=5";
        var recent = session.Execute<IReadOnlyList<JobDetails>>(HttpMethod.Get, path, null).Data!;
        Assert.Equal(jobs.OrderByDescending(job => job.CreatedAt).Take(5).Select(job => job.JobId), recent.Select(job => job.JobId));
        var oldest = session.Execute<IReadOnlyList<JobDetails>>(HttpMethod.Get, path + "&orderBy=asc", null).Data!;
        Assert.Equal(jobs.OrderBy(job => job.CreatedAt).Take(5).Select(job => job.JobId), oldest.Select(job => job.JobId));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void DemoProjectsAreIsolatedBetweenSessionsAndSupportArchiveAndRestore()
    {
        var first = NewSession();
        var second = NewSession();
        var created = first.Execute<ProjectDetails>(HttpMethod.Post, "/api/projects", new ProjectCreateRequest("個別デモ", "jp", "ja", null, null));
        Assert.True(created.IsSuccess);
        var id = created.Data!.ProjectId;
        Assert.Equal("個別デモ", created.Data.Name);
        Assert.Equal(HttpStatusCode.NotFound, second.Execute<ProjectDetails>(HttpMethod.Get, $"/api/projects/{id}", null).StatusCode);

        var archived = first.Execute<ProjectDetails>(HttpMethod.Delete, $"/api/projects/{id}", null);
        Assert.Equal("archived", archived.Data?.Status);
        Assert.DoesNotContain(first.Execute<IReadOnlyList<ProjectDetails>>(HttpMethod.Get, "/api/projects", null).Data!, item => item.ProjectId == id);
        var restored = first.Execute<ProjectDetails>(HttpMethod.Post, $"/api/projects/{id}/restore", null);
        Assert.Equal("active", restored.Data?.Status);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void MockDiscoveryAndVolumeUseTheInputAndNeverConsumeCredits()
    {
        var session = NewSession();
        var project = Assert.Single(session.Execute<IReadOnlyList<ProjectDetails>>(HttpMethod.Get, "/api/projects", null).Data!);
        var prefix = $"/api/projects/{project.ProjectId}";
        var discovery = session.Execute<KeywordDiscoveryResult>(HttpMethod.Post, $"{prefix}/keyword-discovery/suggest", new KeywordDiscoveryRequest(SeedKeyword: "珈琲"));
        Assert.True(discovery.IsSuccess);
        Assert.All(discovery.Data!.Candidates, item => Assert.Contains("珈琲", item.Keyword));
        Assert.NotEmpty(discovery.Data.Candidates);
        Assert.Equal(0, discovery.Meta.ConsumedCredit);

        var job = session.Execute<JobReference>(HttpMethod.Post, $"{prefix}/search-volume/jobs", new SearchVolumeJobRequest(["珈琲 豆", "珈琲 入れ方"], "jp", "ja"));
        Assert.True(job.IsSuccess);
        Assert.Equal("succeeded", job.Data!.Status);
        var details = session.Execute<JobDetails>(HttpMethod.Get, $"/api/jobs/{job.Data.JobId}", null);
        Assert.Equal("RegisterSearchVolumeJob", details.Data?.JobType);
        var rows = session.Execute<IReadOnlyList<SearchVolumeResultRow>>(HttpMethod.Get, $"{prefix}/search-volume/jobs/{job.Data.JobId}/results", null);
        Assert.Equal(new[] { "珈琲 入れ方", "珈琲 豆" }, rows.Data!.Select(row => row.Keyword));
        Assert.All(rows.Data!, row => Assert.Equal("Mock", row.DataSource));
        Assert.Equal(0, rows.Meta.ConsumedCredit);
        Assert.Equal(HttpStatusCode.NotFound, session.Execute<IReadOnlyList<SearchVolumeResultRow>>(HttpMethod.Get, $"{prefix}/search-volume/jobs/{Guid.NewGuid()}/results", null).StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [Trait("Category", "Unit")]
    public void InvalidDemoInputDoesNotCreateResources(string name)
    {
        var session = NewSession();
        var result = session.Execute<ProjectDetails>(HttpMethod.Post, "/api/projects", new ProjectCreateRequest(name, "jp", "ja", null, null));
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Single(session.Execute<IReadOnlyList<ProjectDetails>>(HttpMethod.Get, "/api/projects", null).Data!);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task GuestCallsAndDownloadsNeverReachTheRealTransportEvenAfterLogout()
    {
        using var sessions = new GuestDemoSessionStore(TimeProvider.System);
        var principal = Guest(sessions.Create());
        var router = new GuestApiRouter(new FixedAuthenticationState(principal), sessions);
        using var handler = new RejectNetworkHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://real-api.invalid") };
        var client = new SeoIntelligenceApiClient(http, NullLogger<SeoIntelligenceApiClient>.Instance, router);

        var projects = await client.SearchProjectsAsync();
        Assert.True(projects.IsSuccess);
        var project = Assert.Single(projects.Data!);
        Assert.False((await client.DownloadReportAsync(project.ProjectId, Guid.NewGuid())).IsSuccess);
        Assert.False((await client.UpdateWorkspaceAsync(new WorkspaceUpdateRequest("unauthorized", null, null, null, null))).IsSuccess);
        sessions.Remove(principal);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SearchProjectsAsync()).StatusCode);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task OnlyAnAdministratorCanFallThroughToTheRealApi()
    {
        using var sessions = new GuestDemoSessionStore(TimeProvider.System);
        foreach (var role in new[] { ApplicationRoles.User, ApplicationRoles.Guest })
        {
            var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "test"));
            var denied = await new GuestApiRouter(new FixedAuthenticationState(principal), sessions)
                .RouteAsync<object>(HttpMethod.Get, "/api/projects", null, default);
            Assert.Equal(HttpStatusCode.Forbidden, denied?.StatusCode);
        }
        var admin = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, ApplicationRoles.Admin)], "test"));
        Assert.Null(await new GuestApiRouter(new FixedAuthenticationState(admin), sessions)
            .RouteAsync<object>(HttpMethod.Get, "/api/projects", null, default));
    }

    private static GuestDemoSession NewSession() => new(DateTimeOffset.UtcNow.AddHours(1));

    [Fact]
    [Trait("Category", "Unit")]
    public void RewriteSampleDetailsMatchTheListAndStayWithinTheProject()
    {
        var session = NewSession();
        var project = Assert.Single(session.Execute<IReadOnlyList<ProjectDetails>>(HttpMethod.Get, "/api/projects", null).Data!);
        var prefix = $"/api/projects/{project.ProjectId}/rewrite/tasks";
        var sample = Assert.Single(session.Execute<IReadOnlyList<RewriteTaskDetails>>(HttpMethod.Get, prefix, null).Data!);

        var result = session.Execute<RewriteTaskDetails>(HttpMethod.Get, $"{prefix}/{sample.TaskId}", null);

        Assert.True(result.IsSuccess);
        Assert.Equal(sample.TaskId, result.Data!.TaskId);
        Assert.Equal(project.ProjectId, result.Data.ProjectId);
        Assert.Equal(sample.TargetUrl, result.Data.TargetUrl);
        Assert.Equal(sample.PriorityScore, result.Data.PriorityScore);
        Assert.Equal(sample.Reason.GetRawText(), result.Data.Reason.GetRawText());
        Assert.Equal("active", result.Data.Status);
        Assert.Equal(sample.Status, result.Data.Status);
        Assert.Equal("デモの提案です。", result.Data.Memo);

        var other = session.Execute<ProjectDetails>(HttpMethod.Post, "/api/projects", new ProjectCreateRequest("other", "jp", "ja", null, null)).Data!;
        Assert.Equal(HttpStatusCode.NotFound, session.Execute<RewriteTaskDetails>(HttpMethod.Get,
            $"/api/projects/{other.ProjectId}/rewrite/tasks/{sample.TaskId}", null).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, session.Execute<RewriteTaskDetails>(HttpMethod.Get,
            $"{prefix}/{Guid.NewGuid()}", null).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, NewSession().Execute<RewriteTaskDetails>(HttpMethod.Get,
            $"{prefix}/{sample.TaskId}", null).StatusCode);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BriefSampleUsesPendingReviewInTheListDetailsAndVersionHistory()
    {
        var session = NewSession();
        var project = Assert.Single(session.Execute<IReadOnlyList<ProjectDetails>>(HttpMethod.Get, "/api/projects", null).Data!);
        var prefix = $"/api/projects/{project.ProjectId}/briefs";
        var sample = Assert.Single(session.Execute<IReadOnlyList<ArticleBriefSummary>>(HttpMethod.Get, prefix, null).Data!);
        var details = session.Execute<ArticleBriefDetails>(HttpMethod.Get, $"{prefix}/{sample.BriefId}", null).Data!;
        var version = Assert.Single(session.Execute<IReadOnlyList<ArticleBriefVersionDetails>>(HttpMethod.Get,
            $"{prefix}/{sample.BriefId}/versions", null).Data!);

        Assert.Equal("pending", sample.ReviewStatus);
        Assert.Equal(sample.ReviewStatus, details.ReviewStatus);
        Assert.Equal(sample.ReviewStatus, version.ReviewStatus);
        Assert.Equal("active", details.Status);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CsvExportHonorsTheSelectedJobAndEscapesSpreadsheetFormulas()
    {
        var session = NewSession();
        var project = Assert.Single(session.Execute<IReadOnlyList<ProjectDetails>>(HttpMethod.Get, "/api/projects", null).Data!);
        var path = $"/api/projects/{project.ProjectId}";
        var first = session.Execute<JobReference>(HttpMethod.Post, $"{path}/search-volume/jobs", new SearchVolumeJobRequest(["=1+1", "wanted"], "jp", "ja"));
        session.Execute<JobReference>(HttpMethod.Post, $"{path}/search-volume/jobs", new SearchVolumeJobRequest(["unrelated"], "jp", "ja"));
        var export = session.Execute<JobReference>(HttpMethod.Post, $"{path}/exports/csv", new DataExportRequest("search_volume_results", JsonSerializer.SerializeToElement(new { jobId = first.Data!.JobId })));
        Assert.True(export.IsSuccess);
        var job = session.Execute<JobDetails>(HttpMethod.Get, $"/api/jobs/{export.Data!.JobId}", null).Data!;
        var file = session.Execute<ApiFileResponse>(HttpMethod.Get, $"{path}/exports/{job.ResultResource!.ResourceId}/content", null);
        Assert.True(file.IsSuccess);
        using var content = file.Data!;
        using var reader = new StreamReader(content.Content);
        var csv = await reader.ReadToEndAsync();
        Assert.Contains("searchVolume", csv);
        Assert.Contains("\"'=1+1\"", csv);
        Assert.Contains("wanted", csv);
        Assert.DoesNotContain("unrelated", csv);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(100, true)]
    [InlineData(101, false)]
    [Trait("Category", "Unit")]
    public void VolumeChecksTheDemoKeywordLimit(int count, bool accepted)
    {
        var session = NewSession();
        var project = Assert.Single(session.Execute<IReadOnlyList<ProjectDetails>>(HttpMethod.Get, "/api/projects", null).Data!);
        var result = session.Execute<JobReference>(HttpMethod.Post, $"/api/projects/{project.ProjectId}/search-volume/jobs", new SearchVolumeJobRequest(Enumerable.Range(0, count).Select(i => $"keyword {i}").ToArray(), "jp", "ja"));
        Assert.Equal(accepted, result.IsSuccess);
        if (!accepted) Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
    }

    [Fact]
    [Trait("Category", "Security")]
    public void ExpiredOrRestartedSessionsCannotBeResolved()
    {
        var clock = new DemoClock();
        using var sessions = new GuestDemoSessionStore(clock);
        var principal = Guest(sessions.Create());
        Assert.NotNull(sessions.Find(principal));
        using var restarted = new GuestDemoSessionStore(clock);
        Assert.Null(restarted.Find(principal));
        clock.Now += GuestDemoSessionStore.Lifetime;
        Assert.Null(sessions.Find(principal));
    }

    [Fact]
    [Trait("Category", "Security")]
    public void ResourcesAndExportsCannotCrossDemoProjectBoundaries()
    {
        var session = NewSession();
        var first = Assert.Single(session.Execute<IReadOnlyList<ProjectDetails>>(HttpMethod.Get, "/api/projects", null).Data!);
        var second = session.Execute<ProjectDetails>(HttpMethod.Post, "/api/projects", new ProjectCreateRequest("other", "jp", "ja", null, null)).Data!;
        var site = session.Execute<SiteDetails>(HttpMethod.Post, $"/api/projects/{first.ProjectId}/sites", new SiteCreateRequest("example.com", "https://example.com", "own", null)).Data!;
        Assert.Equal(HttpStatusCode.NotFound, session.Execute<SiteDetails>(HttpMethod.Put, $"/api/projects/{second.ProjectId}/sites/{site.SiteId}", new SiteUpdateRequest("example.org", "https://example.org", "own", null)).StatusCode);
        var volume = session.Execute<JobReference>(HttpMethod.Post, $"/api/projects/{first.ProjectId}/search-volume/jobs", new SearchVolumeJobRequest(["private demo"], "jp", "ja")).Data!;
        Assert.Equal(HttpStatusCode.NotFound, session.Execute<JobReference>(HttpMethod.Get, $"/api/projects/{second.ProjectId}/search-volume/jobs/{volume.JobId}", null).StatusCode);
        var export = session.Execute<JobReference>(HttpMethod.Post, $"/api/projects/{second.ProjectId}/exports/csv", new DataExportRequest("search_volume_results", JsonSerializer.SerializeToElement(new { jobId = volume.JobId })));
        Assert.Equal(HttpStatusCode.NotFound, export.StatusCode);
        var sample = Assert.Single(session.Execute<IReadOnlyList<TopicClusterSummary>>(HttpMethod.Get, $"/api/projects/{first.ProjectId}/clusters", null).Data!);
        Assert.Equal(HttpStatusCode.NotFound, session.Execute<TopicClusterDetails>(HttpMethod.Get, $"/api/projects/{second.ProjectId}/clusters/{sample.ClusterId}", null).StatusCode);
    }

    private sealed class DemoClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    internal static ClaimsPrincipal Guest(string id) => new(new ClaimsIdentity(
    [
        new Claim(ClaimTypes.NameIdentifier, id),
        new Claim(ClaimTypes.Role, ApplicationRoles.Guest),
        new Claim(GuestAuthentication.ModeClaim, GuestAuthentication.MockMode)
    ], "test"));

    private sealed class FixedAuthenticationState(ClaimsPrincipal principal) : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(principal));
    }

    private sealed class RejectNetworkHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            throw new InvalidOperationException("A guest attempted a real network request.");
        }
    }
}
