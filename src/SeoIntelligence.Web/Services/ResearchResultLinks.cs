using SeoIntelligence.Application.Jobs;

namespace SeoIntelligence.Web.Services;

public static class ResearchResultLinks
{
    public static string? ForJob(JobDetails job) => job.ProjectId is { } projectId ? job.JobType switch
    {
        "KeywordDiscoveryJob" => $"/keywords?projectId={projectId:D}&jobId={job.JobId:D}",
        "RegisterSearchVolumeJob" => $"/search-volume?projectId={projectId:D}&jobId={job.JobId:D}",
        _ => null
    } : null;
}
