using Microsoft.AspNetCore.Mvc;
using SeoIntelligence.Api.Common;
using SeoIntelligence.Application.Common;
using SeoIntelligence.Application.Jobs;
using SeoIntelligence.Application.ProjectContext;
using SeoIntelligence.Contracts.Api;
using SeoIntelligence.Infrastructure.Persistence;

namespace SeoIntelligence.Api.Endpoints;

internal static class JobEndpoints
{
    private static readonly string[] JobStatuses =
    [
        "all",
        "queued",
        "running",
        "waiting_external",
        "succeeded",
        "failed_retryable",
        "failed_fatal",
        "canceled"
    ];

    private static readonly string[] JobSortFields =
    [
        "jobType",
        "status",
        "progress",
        "retryCount",
        "nextRunAt",
        "createdAt",
        "updatedAt",
        "completedAt"
    ];

    public static IEndpointRouteBuilder MapJobEndpoints(this IEndpointRouteBuilder app)
    {
        var jobs = app.MapGroup("/api/jobs");

        jobs.MapGet("", SearchJobsAsync);
        jobs.MapGet("/{jobId:guid}", GetJobAsync);
        jobs.MapPost("/{jobId:guid}/cancel", CancelJobAsync);
        jobs.MapPost("/{jobId:guid}/retry", RetryJobAsync);

        return app;
    }

    private static async Task<IResult> SearchJobsAsync(
        [FromServices] IJobService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken,
        int page = ListQueryParameters.DefaultPage,
        int pageSize = ListQueryParameters.DefaultPageSize,
        string status = "all",
        string sortBy = "createdAt",
        string orderBy = "desc",
        string? q = null,
        string? jobType = null,
        [FromQuery(Name = "job_type")] string? jobTypeSnake = null,
        Guid? projectId = null,
        [FromQuery(Name = "project_id")] Guid? projectIdSnake = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null)
    {
        var validationErrors = ValidateJobQuery(
            page,
            pageSize,
            status,
            sortBy,
            orderBy,
            q,
            jobType ?? jobTypeSnake,
            projectId ?? projectIdSnake,
            from,
            to);
        if (validationErrors.Count > 0)
        {
            return ApiResponseResults.ValidationFailure(httpContext, validationErrors);
        }

        var query = new JobSearchQuery(
            new SearchQuery(
                q,
                status.Trim(),
                new SortRequest(
                    sortBy.Trim(),
                    string.Equals(orderBy, "asc", StringComparison.OrdinalIgnoreCase)
                        ? SortDirection.Asc
                        : SortDirection.Desc),
                new PageRequest(page, pageSize)),
            NormalizeText(jobType ?? jobTypeSnake),
            projectId ?? projectIdSnake,
            from,
            to);

        return ApiResponseResults.FromPagedResult(
            httpContext,
            await service.SearchAsync(CreateContext(contextService, httpContext), query, cancellationToken));
    }

    private static async Task<IResult> GetJobAsync(
        Guid jobId,
        [FromServices] IJobService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
        => FromJobResult(
            httpContext,
            await service.GetAsync(CreateContext(contextService, httpContext), jobId, cancellationToken));

    private static async Task<IResult> CancelJobAsync(
        Guid jobId,
        [FromServices] IJobService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
        => FromJobResult(
            httpContext,
            await service.CancelAsync(CreateContext(contextService, httpContext), jobId, cancellationToken));

    private static async Task<IResult> RetryJobAsync(
        Guid jobId,
        [FromServices] IJobService service,
        [FromServices] IProjectContextService contextService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
        => FromJobResult(
            httpContext,
            await service.RetryAsync(CreateContext(contextService, httpContext), jobId, cancellationToken));

    private static IResult FromJobResult(HttpContext httpContext, Result<JobDetails> result)
        => result.IsSuccess
            ? ApiResponseResults.Ok(
                httpContext,
                result.Value!,
                new ApiResponseMeta(
                    JobId: result.Value!.JobId,
                    ExternalRequestId: result.Value.ExternalRequestId))
            : ApiResponseResults.FromError(httpContext, result.Error!);

    private static ProjectContext CreateContext(
        IProjectContextService contextService,
        HttpContext httpContext)
        => contextService.Create(
            SeoIntelligenceSeedData.DefaultWorkspaceId,
            correlationId: httpContext.GetCorrelationId());

    private static IReadOnlyDictionary<string, string[]> ValidateJobQuery(
        int page,
        int pageSize,
        string status,
        string sortBy,
        string orderBy,
        string? q,
        string? jobType,
        Guid? projectId,
        DateTimeOffset? from,
        DateTimeOffset? to)
    {
        var parameters = new ListQueryParameters
        {
            Page = page,
            PageSize = pageSize,
            Status = status,
            SortBy = sortBy,
            OrderBy = orderBy,
            Q = q
        };

        var errors = parameters
            .Validate(JobStatuses, JobSortFields)
            .ToDictionary(
                pair => pair.Key,
                pair => pair.Value.ToList(),
                StringComparer.OrdinalIgnoreCase);

        if (jobType is { Length: > ListQueryParameters.MaxSearchTextLength })
        {
            AddValidationError(errors, nameof(jobType), $"jobType must be {ListQueryParameters.MaxSearchTextLength} characters or fewer.");
        }

        if (projectId == Guid.Empty)
        {
            AddValidationError(errors, nameof(projectId), "projectId must not be empty when provided.");
        }

        if (from.HasValue && to.HasValue && from.Value > to.Value)
        {
            AddValidationError(errors, nameof(from), "from must be earlier than or equal to to.");
        }

        return errors.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.ToArray(),
            StringComparer.OrdinalIgnoreCase);
    }

    private static void AddValidationError(
        IDictionary<string, List<string>> errors,
        string target,
        string message)
    {
        if (!errors.TryGetValue(target, out var messages))
        {
            messages = [];
            errors[target] = messages;
        }

        messages.Add(message);
    }

    private static string? NormalizeText(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
