using SeoIntelligence.Application.Jobs;
using SeoIntelligence.Web.Services;

namespace E2ETests;

/// <summary>
/// The mapping behind the download link on the job panel. It decides which generated files are
/// reachable at all, so the cases that must produce no link matter as much as the ones that must.
/// </summary>
public sealed class ArtifactDownloadLinkTests
{
    private static readonly Guid ProjectId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ResourceId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Theory]
    [Trait("Category", "E2E")]
    [InlineData("data_export", "/downloads/projects/11111111-1111-1111-1111-111111111111/exports/22222222-2222-2222-2222-222222222222")]
    [InlineData("article_brief_export", "/downloads/projects/11111111-1111-1111-1111-111111111111/exports/22222222-2222-2222-2222-222222222222")]
    [InlineData("report", "/downloads/projects/11111111-1111-1111-1111-111111111111/reports/22222222-2222-2222-2222-222222222222")]
    public void EachDownloadableResourceTypeGetsItsWebHostRoute(string resourceType, string expected)
        => Assert.Equal(expected, ArtifactDownloadLinks.ForJob(CreateJob("succeeded", resourceType)));

    [Theory]
    [Trait("Category", "E2E")]
    [InlineData("queued")]
    [InlineData("running")]
    [InlineData("waiting_external")]
    [InlineData("failed_retryable")]
    [InlineData("failed_fatal")]
    [InlineData("canceled")]
    public void UnfinishedJobsOfferNoLink(string status)
        => Assert.Null(ArtifactDownloadLinks.ForJob(CreateJob(status, "data_export")));

    [Theory]
    [Trait("Category", "E2E")]
    [InlineData("keyword_discovery")]
    [InlineData("search_volume")]
    [InlineData("content_analysis")]
    public void JobsWhoseResultIsNotAFileOfferNoLink(string resourceType)
        => Assert.Null(ArtifactDownloadLinks.ForJob(CreateJob("succeeded", resourceType)));

    [Fact]
    [Trait("Category", "E2E")]
    public void JobsWithNoResultResourceOfferNoLink()
        => Assert.Null(ArtifactDownloadLinks.ForJob(CreateJob("succeeded", resourceType: null)));

    [Fact]
    [Trait("Category", "E2E")]
    public void WorkspaceScopedJobsOfferNoLinkBecauseTheRouteNeedsAProject()
        => Assert.Null(ArtifactDownloadLinks.ForJob(
            CreateJob("succeeded", "data_export") with { ProjectId = null }));

    private static JobDetails CreateJob(string status, string? resourceType)
        => new(
            JobId: Guid.Parse("33333333-3333-3333-3333-333333333333"),
            WorkspaceId: Guid.Parse("44444444-4444-4444-4444-444444444444"),
            ProjectId: ProjectId,
            JobType: "DataExportJob",
            Status: status,
            Progress: 100,
            StatusUrl: "/api/jobs/33333333-3333-3333-3333-333333333333",
            ExternalRequestId: null,
            ResultResource: resourceType is null ? null : new JobResultResource(resourceType, ResourceId),
            RetryCount: 0,
            NextRunAt: null,
            Error: null,
            RequestedBy: "developer",
            CreatedAt: DateTime.UtcNow,
            UpdatedAt: DateTime.UtcNow,
            CompletedAt: DateTime.UtcNow);
}
