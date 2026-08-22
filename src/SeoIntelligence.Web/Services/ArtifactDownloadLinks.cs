using SeoIntelligence.Application.Jobs;

namespace SeoIntelligence.Web.Services;

/// <summary>
/// Maps a finished job to the Web host route that streams whatever it produced.
/// <para>
/// Jobs are where exports and reports are generated, so this is the single place every generated
/// file becomes reachable regardless of which screen started it. The routes belong to the Web host
/// rather than the API because a browser carries the Identity cookie but no service key.
/// </para>
/// </summary>
public static class ArtifactDownloadLinks
{
    /// <summary>Result resource types whose artifact is stored as a data export.</summary>
    public const string DataExportResourceType = "data_export";

    public const string ArticleBriefExportResourceType = "article_brief_export";

    public const string ReportResourceType = "report";

    /// <summary>
    /// The download route for a job's artifact, or null when the job produced nothing downloadable
    /// - it has not succeeded, it is not project scoped, or its result is not a file.
    /// </summary>
    public static string? ForJob(JobDetails job)
    {
        ArgumentNullException.ThrowIfNull(job);

        if (!string.Equals(job.Status, "succeeded", StringComparison.OrdinalIgnoreCase) ||
            job.ProjectId is not { } projectId ||
            job.ResultResource is not { } resource)
        {
            return null;
        }

        return resource.ResourceType switch
        {
            // Article brief exports are written by a different service but stored as data exports,
            // so they are fetched through the same route.
            DataExportResourceType or ArticleBriefExportResourceType
                => $"/downloads/projects/{projectId:D}/exports/{resource.ResourceId:D}",
            ReportResourceType
                => $"/downloads/projects/{projectId:D}/reports/{resource.ResourceId:D}",
            _ => null
        };
    }
}
