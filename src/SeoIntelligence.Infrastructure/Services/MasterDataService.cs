using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SeoIntelligence.Application.Common;
using SeoIntelligence.Application.Configuration;
using SeoIntelligence.Application.Jobs;
using SeoIntelligence.Application.ProjectContext;
using SeoIntelligence.Application.RakkoKeyword;
using SeoIntelligence.Application.Services;
using SeoIntelligence.Domain.Common;
using SeoIntelligence.Infrastructure.Persistence;
using SeoIntelligence.Infrastructure.Persistence.Entities;
using ProjectExecutionContext = SeoIntelligence.Application.ProjectContext.ProjectContext;

namespace SeoIntelligence.Infrastructure.Services;

internal sealed class MasterDataService(
    SeoIntelligenceDbContext dbContext,
    IJobService jobService)
    : IMasterDataService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<Result<JobReference>> SyncAsync(
        ProjectExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.SerializeToElement(
            new
            {
                provider = RakkoKeywordOptions.ProviderName,
                targets = new[] { "locations", "languages" }
            },
            JsonOptions);

        var result = await jobService.RegisterAsync(
            context,
            new JobRegistrationRequest(
                MasterDataSyncJob.JobType,
                payload,
                TargetKey: MasterDataSyncJob.TargetKey,
                Queue: "default"),
            cancellationToken);

        return result.IsSuccess
            ? Result<JobReference>.Success(new JobReference(result.Value!.JobId, result.Value.Status))
            : Result<JobReference>.Failure(result.Error!);
    }

    public async Task<Result<IReadOnlyList<LocationSummary>>> ListLocationsAsync(
        CancellationToken cancellationToken = default)
    {
        var locations = await dbContext.Locations
            .AsNoTracking()
            .Where(entity => entity.Status == StatusValues.Active)
            .OrderBy(entity => entity.LocationName)
            .ThenBy(entity => entity.LocationCode)
            .Select(entity => new LocationSummary(
                entity.Provider,
                entity.LocationCode,
                entity.LocationName,
                entity.CountryCode,
                entity.Status))
            .ToArrayAsync(cancellationToken);

        return Result<IReadOnlyList<LocationSummary>>.Success(locations);
    }

    public async Task<Result<IReadOnlyList<LanguageSummary>>> ListLanguagesAsync(
        CancellationToken cancellationToken = default)
    {
        var languages = await dbContext.Languages
            .AsNoTracking()
            .Where(entity => entity.Status == StatusValues.Active)
            .OrderBy(entity => entity.LanguageName)
            .ThenBy(entity => entity.LanguageCode)
            .Select(entity => new LanguageSummary(
                entity.Provider,
                entity.LanguageCode,
                entity.LanguageName,
                entity.Status))
            .ToArrayAsync(cancellationToken);

        return Result<IReadOnlyList<LanguageSummary>>.Success(languages);
    }
}

internal sealed class MasterDataSyncJob(
    SeoIntelligenceDbContext dbContext,
    IRakkoKeywordClient rakkoKeywordClient,
    IJobService jobService,
    IProjectContextService contextService,
    TimeProvider timeProvider,
    ILogger<MasterDataSyncJob> logger)
{
    public const string JobType = "MasterDataSyncJob";
    public const string TargetKey = $"{RakkoKeywordOptions.ProviderName}:master-data";

    public async Task ExecuteAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await dbContext.Jobs
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Id == jobId, cancellationToken);
        if (job is null)
        {
            logger.LogWarning("Master data sync job {job_id} was not found.", jobId);
            return;
        }

        var context = contextService.Create(
            job.WorkspaceId,
            job.ProjectId,
            correlationId: $"job:{job.Id:D}");

        var start = await jobService.TryStartAsync(
            context,
            jobId,
            new JobExecutionStartRequest(TargetKey, TimeSpan.FromMinutes(10)),
            cancellationToken);
        if (!start.IsSuccess)
        {
            logger.LogInformation(
                "Master data sync job {job_id} was not started: {message}",
                jobId,
                start.Error?.Message);
            return;
        }

        await using var lease = start.Value!;
        try
        {
            var contractScope = await ResolveContractScopeAsync(job.WorkspaceId, cancellationToken);
            var clientContext = new RakkoKeywordClientContext(
                job.WorkspaceId,
                ProjectId: null,
                JobId: job.Id,
                ApiContractScopeId: contractScope?.Id,
                ContractScopeKey: contractScope?.ScopeKey ?? SeoIntelligenceSeedData.RakkoKeywordScopeKey,
                CorrelationId: context.CorrelationId,
                Actor: job.RequestedBy);

            var locations = await rakkoKeywordClient.ListLocationsAsync(clientContext, cancellationToken);
            if (!locations.IsSuccess)
            {
                await RecordExternalFailureAsync(context, jobId, locations, cancellationToken);
                return;
            }

            var languages = await rakkoKeywordClient.ListLanguagesAsync(clientContext, cancellationToken);
            if (!languages.IsSuccess)
            {
                await RecordExternalFailureAsync(context, jobId, languages, cancellationToken);
                return;
            }

            var now = timeProvider.GetUtcNow().UtcDateTime;
            await UpsertLocationsAsync(locations.Data!, now, cancellationToken);
            await UpsertLanguagesAsync(languages.Data!, now, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            var completed = await jobService.CompleteAsync(
                context,
                jobId,
                new JobCompletion(Progress: 100),
                cancellationToken);
            if (!completed.IsSuccess)
            {
                logger.LogWarning(
                    "Master data sync job {job_id} finished work but could not be marked succeeded: {message}",
                    jobId,
                    completed.Error?.Message);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (DbUpdateException exception)
        {
            logger.LogWarning(exception, "Master data sync job {job_id} hit a transient database failure.", jobId);
            await jobService.RecordFailureAsync(
                context,
                jobId,
                JobFailure.DatabaseTransient("Master data sync could not persist changes."),
                CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Master data sync job {job_id} failed unexpectedly.", jobId);
            await jobService.RecordFailureAsync(
                context,
                jobId,
                new JobFailure(
                    JobFailureKind.Unexpected,
                    HttpStatusCode: null,
                    ErrorCode: "master_data_sync_failed",
                    Message: "Master data sync failed."),
                CancellationToken.None);
        }
    }

    private async Task<ApiContractScopeEntity?> ResolveContractScopeAsync(
        Guid workspaceId,
        CancellationToken cancellationToken)
        => await dbContext.ApiContractScopes
            .AsNoTracking()
            .Where(entity =>
                entity.WorkspaceId == workspaceId &&
                entity.Provider == RakkoKeywordOptions.ProviderName &&
                entity.Status == StatusValues.Active)
            .OrderByDescending(entity => entity.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task UpsertLocationsAsync(
        RakkoLocationCatalog catalog,
        DateTime syncedAt,
        CancellationToken cancellationToken)
    {
        var incoming = catalog.Locations
            .Select(NormalizeLocation)
            .Where(item => item is not null)
            .Select(item => item!)
            .GroupBy(item => item.Code, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
        var incomingCodes = incoming
            .Select(item => item.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existing = await dbContext.Locations
            .Where(entity => entity.Provider == RakkoKeywordOptions.ProviderName)
            .ToListAsync(cancellationToken);
        var existingByCode = existing.ToDictionary(
            entity => entity.LocationCode,
            StringComparer.OrdinalIgnoreCase);

        foreach (var item in incoming)
        {
            if (existingByCode.TryGetValue(item.Code, out var entity))
            {
                entity.LocationName = item.Name;
                entity.CountryCode = item.CountryCode;
                entity.Status = StatusValues.Active;
                entity.SyncedAt = syncedAt;
                continue;
            }

            dbContext.Locations.Add(new LocationEntity
            {
                Id = UuidV7.New(),
                Provider = RakkoKeywordOptions.ProviderName,
                LocationCode = item.Code,
                LocationName = item.Name,
                CountryCode = item.CountryCode,
                Status = StatusValues.Active,
                SyncedAt = syncedAt
            });
        }

        foreach (var entity in existing.Where(entity => !incomingCodes.Contains(entity.LocationCode)))
        {
            entity.Status = StatusValues.Archived;
            entity.SyncedAt = syncedAt;
        }
    }

    private async Task UpsertLanguagesAsync(
        RakkoLanguageCatalog catalog,
        DateTime syncedAt,
        CancellationToken cancellationToken)
    {
        var incoming = catalog.Languages
            .Select(NormalizeLanguage)
            .Where(item => item is not null)
            .Select(item => item!)
            .GroupBy(item => item.Code, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
        var incomingCodes = incoming
            .Select(item => item.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existing = await dbContext.Languages
            .Where(entity => entity.Provider == RakkoKeywordOptions.ProviderName)
            .ToListAsync(cancellationToken);
        var existingByCode = existing.ToDictionary(
            entity => entity.LanguageCode,
            StringComparer.OrdinalIgnoreCase);

        foreach (var item in incoming)
        {
            if (existingByCode.TryGetValue(item.Code, out var entity))
            {
                entity.LanguageName = item.Name;
                entity.Status = StatusValues.Active;
                entity.SyncedAt = syncedAt;
                continue;
            }

            dbContext.Languages.Add(new LanguageEntity
            {
                Id = UuidV7.New(),
                Provider = RakkoKeywordOptions.ProviderName,
                LanguageCode = item.Code,
                LanguageName = item.Name,
                Status = StatusValues.Active,
                SyncedAt = syncedAt
            });
        }

        foreach (var entity in existing.Where(entity => !incomingCodes.Contains(entity.LanguageCode)))
        {
            entity.Status = StatusValues.Archived;
            entity.SyncedAt = syncedAt;
        }
    }

    // ラッコキーワードAPI v1.12.0以降、地域・言語のcode値は提供されない。
    // location_code/language_code列にはAPIリクエスト(location/language)でそのまま使える名前を正準値として格納する。
    private static NormalizedLocation? NormalizeLocation(RakkoLocation location)
    {
        var name = NormalizeText(location.Name);
        if (name is null)
        {
            return null;
        }

        var countryCode = NormalizeText(location.CountryIsoCode)?.ToUpperInvariant() ?? string.Empty;
        return new NormalizedLocation(name, name, countryCode);
    }

    private static NormalizedLanguage? NormalizeLanguage(RakkoLanguage language)
    {
        var name = NormalizeText(language.Name);
        return name is null
            ? null
            : new NormalizedLanguage(name, name);
    }

    private async Task RecordExternalFailureAsync<T>(
        ProjectExecutionContext context,
        Guid jobId,
        RakkoKeywordCallResult<T> result,
        CancellationToken cancellationToken)
    {
        var message = result.Errors.Count == 0
            ? "Rakko Keyword API returned an error while syncing master data."
            : string.Join(" ", result.Errors);
        await jobService.RecordFailureAsync(
            context,
            jobId,
            JobFailure.FromHttpStatusCode(result.StatusCode, result.ExternalCall.ErrorCode, message),
            cancellationToken);
    }

    private static string? NormalizeText(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record NormalizedLocation(string Code, string Name, string CountryCode);

    private sealed record NormalizedLanguage(string Code, string Name);
}
