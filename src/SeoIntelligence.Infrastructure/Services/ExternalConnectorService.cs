using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SeoIntelligence.Application.Auditing;
using SeoIntelligence.Application.Common;
using SeoIntelligence.Application.Services;
using SeoIntelligence.Domain.Common;
using SeoIntelligence.Infrastructure.Persistence;
using SeoIntelligence.Infrastructure.Persistence.Entities;
using ProjectExecutionContext = SeoIntelligence.Application.ProjectContext.ProjectContext;

namespace SeoIntelligence.Infrastructure.Services;

internal sealed class ExternalConnectorService(
    SeoIntelligenceDbContext dbContext,
    IAuditLogWriter auditLogWriter,
    TimeProvider timeProvider)
    : IExternalConnectorService
{
    private const int MaxConnectorTypeLength = 32;
    private const int MaxNameLength = 120;
    private const int MaxAuthRefLength = 300;
    private const int MaxSettingsJsonLength = 16_384;
    private const string ConnectionTestRunType = "connection_test";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> SupportedConnectorTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "gsc",
        "ga4",
        "cms",
        "bi"
    };
    private static readonly HashSet<string> LifecycleStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        StatusValues.Active,
        StatusValues.Disabled
    };
    private static readonly HashSet<string> SensitiveSettingNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "secret",
        "secretvalue",
        "apikey",
        "apitoken",
        "accesstoken",
        "refreshtoken",
        "oauthtoken",
        "password",
        "clientsecret",
        "webhookurl",
        "authorization",
        "bearertoken",
        "privatekey"
    };

    public async Task<Result<PagedResult<ConnectorSettingsDetails>>> SearchConnectorsAsync(
        ProjectExecutionContext context,
        SearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var project = await FindActiveProjectAsync(context, cancellationToken);
        if (project is null)
        {
            return Failure<PagedResult<ConnectorSettingsDetails>>(ErrorCode.NotFound, "Project was not found.");
        }

        var source = dbContext.ExternalConnectorSettings
            .AsNoTracking()
            .Where(entity =>
                entity.WorkspaceId == context.WorkspaceId &&
                entity.ProjectId == project.Id);

        source = ApplyStatusFilter(source, query.Status);

        var q = OptionalText(query.Q)?.ToLowerInvariant();
        if (q is not null)
        {
            source = source.Where(entity =>
                entity.ConnectorType.ToLower().Contains(q) ||
                entity.Name.ToLower().Contains(q) ||
                (entity.AuthRef != null && entity.AuthRef.ToLower().Contains(q)));
        }

        source = SortConnectors(source, query.Sort);
        return Result<PagedResult<ConnectorSettingsDetails>>.Success(
            await ToPagedResultAsync(source, query, MapConnector, cancellationToken));
    }

    public async Task<Result<ConnectorSettingsDetails>> CreateConnectorAsync(
        ProjectExecutionContext context,
        ConnectorSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateConnectorRequest(request, StatusValues.Active, "{}");
        if (validation.Errors.HasErrors)
        {
            return ValidationFailure<ConnectorSettingsDetails>(validation.Errors);
        }

        var project = await FindActiveProjectAsync(context, cancellationToken);
        if (project is null)
        {
            return Failure<ConnectorSettingsDetails>(ErrorCode.NotFound, "Project was not found.");
        }

        var now = NowUtc();
        var connector = new ExternalConnectorSettingEntity
        {
            Id = UuidV7.New(),
            WorkspaceId = context.WorkspaceId,
            ProjectId = project.Id,
            ConnectorType = validation.ConnectorType!,
            Name = validation.Name!,
            AuthRef = validation.AuthRef,
            SettingsJson = validation.SettingsJson!,
            Status = validation.Status!,
            CreatedAt = now,
            UpdatedAt = now,
            DisabledAt = string.Equals(validation.Status, StatusValues.Disabled, StringComparison.Ordinal)
                ? now
                : null
        };

        dbContext.ExternalConnectorSettings.Add(connector);
        AddConnectorAudit(
            context,
            AuditLogActionNames.ExternalConnectorCreated,
            connector,
            before: null);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<ConnectorSettingsDetails>.Success(MapConnector(connector));
    }

    public async Task<Result<ConnectorSettingsDetails>> UpdateConnectorAsync(
        ProjectExecutionContext context,
        Guid connectorId,
        ConnectorSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        var project = await FindActiveProjectAsync(context, cancellationToken);
        if (project is null)
        {
            return Failure<ConnectorSettingsDetails>(ErrorCode.NotFound, "Project was not found.");
        }

        var connector = await FindConnectorAsync(context, connectorId, asTracking: true, cancellationToken);
        if (connector is null)
        {
            return Failure<ConnectorSettingsDetails>(ErrorCode.NotFound, "Connector setting was not found.");
        }

        var validation = ValidateConnectorRequest(request, connector.Status, connector.SettingsJson);
        if (validation.Errors.HasErrors)
        {
            return ValidationFailure<ConnectorSettingsDetails>(validation.Errors);
        }

        var before = ToConnectorAuditSnapshot(connector);
        var now = NowUtc();
        connector.ConnectorType = validation.ConnectorType!;
        connector.Name = validation.Name!;
        connector.AuthRef = validation.AuthRef;
        connector.SettingsJson = validation.SettingsJson!;
        connector.Status = validation.Status!;
        connector.UpdatedAt = now;
        connector.DisabledAt = string.Equals(validation.Status, StatusValues.Disabled, StringComparison.Ordinal)
            ? connector.DisabledAt ?? now
            : null;

        AddConnectorAudit(
            context,
            AuditLogActionNames.ExternalConnectorUpdated,
            connector,
            before);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<ConnectorSettingsDetails>.Success(MapConnector(connector));
    }

    public async Task<Result<ConnectorSettingsDetails>> DisableConnectorAsync(
        ProjectExecutionContext context,
        Guid connectorId,
        CancellationToken cancellationToken = default)
    {
        var project = await FindActiveProjectAsync(context, cancellationToken);
        if (project is null)
        {
            return Failure<ConnectorSettingsDetails>(ErrorCode.NotFound, "Project was not found.");
        }

        var connector = await FindConnectorAsync(context, connectorId, asTracking: true, cancellationToken);
        if (connector is null)
        {
            return Failure<ConnectorSettingsDetails>(ErrorCode.NotFound, "Connector setting was not found.");
        }

        var before = ToConnectorAuditSnapshot(connector);
        var now = NowUtc();
        connector.Status = StatusValues.Disabled;
        connector.DisabledAt ??= now;
        connector.UpdatedAt = now;

        AddConnectorAudit(
            context,
            AuditLogActionNames.ExternalConnectorDisabled,
            connector,
            before);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<ConnectorSettingsDetails>.Success(MapConnector(connector));
    }

    public async Task<Result<ConnectorRunDetails>> TestConnectorAsync(
        ProjectExecutionContext context,
        Guid connectorId,
        CancellationToken cancellationToken = default)
    {
        var project = await FindActiveProjectAsync(context, cancellationToken);
        if (project is null)
        {
            return Failure<ConnectorRunDetails>(ErrorCode.NotFound, "Project was not found.");
        }

        var connector = await FindConnectorAsync(context, connectorId, asTracking: false, cancellationToken);
        if (connector is null)
        {
            return Failure<ConnectorRunDetails>(ErrorCode.NotFound, "Connector setting was not found.");
        }

        if (string.Equals(connector.Status, StatusValues.Disabled, StringComparison.Ordinal))
        {
            return Failure<ConnectorRunDetails>(ErrorCode.Conflict, "Connector setting is disabled.");
        }

        var now = NowUtc();
        var run = new ExternalConnectorRunEntity
        {
            Id = UuidV7.New(),
            ConnectorSettingId = connector.Id,
            WorkspaceId = context.WorkspaceId,
            ProjectId = project.Id,
            RunType = ConnectionTestRunType,
            Status = StatusValues.Succeeded,
            RequestJson = JsonSerializer.Serialize(
                new
                {
                    version = 1,
                    mode = "stub",
                    connectorId = connector.Id,
                    connectorType = connector.ConnectorType,
                    settingsKeys = ReadSettingsKeys(connector.SettingsJson),
                    hasAuthRef = !string.IsNullOrWhiteSpace(connector.AuthRef)
                },
                JsonOptions),
            ResultSummaryJson = JsonSerializer.Serialize(
                new
                {
                    version = 1,
                    mode = "stub",
                    connectorType = connector.ConnectorType,
                    status = StatusValues.Succeeded,
                    dataFetched = false,
                    message = "Connection test stub completed without external data fetch.",
                    checkedAt = now
                },
                JsonOptions),
            ErrorJson = null,
            StartedAt = now,
            CompletedAt = now,
            CreatedAt = now
        };

        dbContext.ExternalConnectorRuns.Add(run);
        auditLogWriter.Add(
            context,
            new AuditLogWriteRequest(
                AuditLogActionNames.ExternalConnectorTested,
                AuditLogResourceTypes.ExternalConnectorRun,
                run.Id.ToString("D"),
                new
                {
                    before = (object?)null,
                    after = ToRunAuditSnapshot(run),
                    connector = ToConnectorAuditSnapshot(connector)
                }));
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<ConnectorRunDetails>.Success(MapRun(run));
    }

    public async Task<Result<PagedResult<ConnectorRunDetails>>> GetConnectorRunsAsync(
        ProjectExecutionContext context,
        Guid connectorId,
        SearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var project = await FindActiveProjectAsync(context, cancellationToken);
        if (project is null)
        {
            return Failure<PagedResult<ConnectorRunDetails>>(ErrorCode.NotFound, "Project was not found.");
        }

        var connector = await FindConnectorAsync(context, connectorId, asTracking: false, cancellationToken);
        if (connector is null)
        {
            return Failure<PagedResult<ConnectorRunDetails>>(ErrorCode.NotFound, "Connector setting was not found.");
        }

        var source = dbContext.ExternalConnectorRuns
            .AsNoTracking()
            .Where(entity =>
                entity.WorkspaceId == context.WorkspaceId &&
                entity.ProjectId == project.Id &&
                entity.ConnectorSettingId == connector.Id);

        source = ApplyRunStatusFilter(source, query.Status);

        var q = OptionalText(query.Q)?.ToLowerInvariant();
        if (q is not null)
        {
            source = source.Where(entity =>
                entity.RunType.ToLower().Contains(q) ||
                entity.Status.ToLower().Contains(q) ||
                entity.ResultSummaryJson.ToLower().Contains(q) ||
                (entity.ErrorJson != null && entity.ErrorJson.ToLower().Contains(q)));
        }

        source = SortRuns(source, query.Sort);
        return Result<PagedResult<ConnectorRunDetails>>.Success(
            await ToPagedResultAsync(source, query, MapRun, cancellationToken));
    }

    private async Task<ProjectEntity?> FindActiveProjectAsync(
        ProjectExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (!context.ProjectId.HasValue)
        {
            return null;
        }

        return await dbContext.Projects
            .AsNoTracking()
            .SingleOrDefaultAsync(
                entity =>
                    entity.WorkspaceId == context.WorkspaceId &&
                    entity.Id == context.ProjectId.Value &&
                    entity.Status == StatusValues.Active,
                cancellationToken);
    }

    private async Task<ExternalConnectorSettingEntity?> FindConnectorAsync(
        ProjectExecutionContext context,
        Guid connectorId,
        bool asTracking,
        CancellationToken cancellationToken)
    {
        if (!context.ProjectId.HasValue)
        {
            return null;
        }

        var source = dbContext.ExternalConnectorSettings
            .Where(entity =>
                entity.WorkspaceId == context.WorkspaceId &&
                entity.ProjectId == context.ProjectId.Value &&
                entity.Id == connectorId);
        if (!asTracking)
        {
            source = source.AsNoTracking();
        }

        return await source.SingleOrDefaultAsync(cancellationToken);
    }

    private static ConnectorValidationResult ValidateConnectorRequest(
        ConnectorSettingsRequest request,
        string defaultStatus,
        string defaultSettingsJson)
    {
        var errors = new ValidationErrors();
        var connectorType = NormalizeConnectorType(request.ConnectorType, errors);
        var name = RequireText(request.Name, "name", MaxNameLength, errors);
        var authRef = OptionalText(request.AuthRef);
        if (authRef is { Length: > MaxAuthRefLength })
        {
            errors.Add("authRef", $"authRef must be {MaxAuthRefLength} characters or fewer.");
        }

        var settingsJson = NormalizeSettingsJson(request.Settings, defaultSettingsJson, errors);
        var status = NormalizeLifecycleStatus(request.Status, defaultStatus, errors);
        return new ConnectorValidationResult(errors, connectorType, name, authRef, settingsJson, status);
    }

    private static string? NormalizeConnectorType(string? value, ValidationErrors errors)
    {
        var connectorType = OptionalText(value)?.ToLowerInvariant();
        if (connectorType is null)
        {
            errors.Add("connectorType", "connectorType is required.");
            return null;
        }

        if (connectorType.Length > MaxConnectorTypeLength)
        {
            errors.Add("connectorType", $"connectorType must be {MaxConnectorTypeLength} characters or fewer.");
            return null;
        }

        if (!SupportedConnectorTypes.Contains(connectorType))
        {
            errors.Add("connectorType", "connectorType must be gsc, ga4, cms, or bi.");
            return null;
        }

        return connectorType;
    }

    private static string? NormalizeLifecycleStatus(
        string? value,
        string defaultStatus,
        ValidationErrors errors)
    {
        var status = OptionalText(value)?.ToLowerInvariant() ?? defaultStatus;
        if (!LifecycleStatuses.Contains(status))
        {
            errors.Add("status", "status must be active or disabled.");
            return null;
        }

        return status;
    }

    private static string NormalizeSettingsJson(
        JsonElement? value,
        string defaultJson,
        ValidationErrors errors)
    {
        if (!value.HasValue || value.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return defaultJson;
        }

        if (value.Value.ValueKind != JsonValueKind.Object)
        {
            errors.Add("settings", "settings must be a JSON object.");
            return defaultJson;
        }

        var raw = value.Value.GetRawText();
        if (raw.Length > MaxSettingsJsonLength)
        {
            errors.Add("settings", $"settings must be {MaxSettingsJsonLength} characters or fewer.");
        }

        if (ContainsSensitiveSettingName(value.Value))
        {
            errors.Add("settings", "settings must not contain secret values. Store secrets in Secret Store and pass authRef only.");
        }

        return raw;
    }

    private static bool ContainsSensitiveSettingName(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (IsSensitiveSettingName(property.Name) || ContainsSensitiveSettingName(property.Value))
                {
                    return true;
                }
            }
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (ContainsSensitiveSettingName(item))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsSensitiveSettingName(string propertyName)
    {
        var normalized = new string(propertyName.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        return SensitiveSettingNames.Contains(normalized);
    }

    private static IQueryable<ExternalConnectorSettingEntity> ApplyStatusFilter(
        IQueryable<ExternalConnectorSettingEntity> source,
        string? status)
    {
        var normalized = OptionalText(status) ?? StatusValues.Active;
        return string.Equals(normalized, "all", StringComparison.OrdinalIgnoreCase)
            ? source
            : source.Where(entity => entity.Status == normalized);
    }

    private static IQueryable<ExternalConnectorRunEntity> ApplyRunStatusFilter(
        IQueryable<ExternalConnectorRunEntity> source,
        string? status)
    {
        var normalized = OptionalText(status) ?? "all";
        return string.Equals(normalized, "all", StringComparison.OrdinalIgnoreCase)
            ? source
            : source.Where(entity => entity.Status == normalized);
    }

    private static IQueryable<ExternalConnectorSettingEntity> SortConnectors(
        IQueryable<ExternalConnectorSettingEntity> source,
        SortRequest? sort)
        => SortBy(source, sort, "updatedAt",
            ("connectorType", query => query.OrderBy(entity => entity.ConnectorType), query => query.OrderByDescending(entity => entity.ConnectorType)),
            ("name", query => query.OrderBy(entity => entity.Name), query => query.OrderByDescending(entity => entity.Name)),
            ("status", query => query.OrderBy(entity => entity.Status), query => query.OrderByDescending(entity => entity.Status)),
            ("createdAt", query => query.OrderBy(entity => entity.CreatedAt), query => query.OrderByDescending(entity => entity.CreatedAt)),
            ("updatedAt", query => query.OrderBy(entity => entity.UpdatedAt), query => query.OrderByDescending(entity => entity.UpdatedAt)));

    private static IQueryable<ExternalConnectorRunEntity> SortRuns(
        IQueryable<ExternalConnectorRunEntity> source,
        SortRequest? sort)
        => SortBy(source, sort, "createdAt",
            ("runType", query => query.OrderBy(entity => entity.RunType), query => query.OrderByDescending(entity => entity.RunType)),
            ("status", query => query.OrderBy(entity => entity.Status), query => query.OrderByDescending(entity => entity.Status)),
            ("startedAt", query => query.OrderBy(entity => entity.StartedAt), query => query.OrderByDescending(entity => entity.StartedAt)),
            ("completedAt", query => query.OrderBy(entity => entity.CompletedAt), query => query.OrderByDescending(entity => entity.CompletedAt)),
            ("createdAt", query => query.OrderBy(entity => entity.CreatedAt), query => query.OrderByDescending(entity => entity.CreatedAt)));

    private static IQueryable<T> SortBy<T>(
        IQueryable<T> source,
        SortRequest? sort,
        string defaultSortBy,
        params (string SortBy, Func<IQueryable<T>, IOrderedQueryable<T>> Asc, Func<IQueryable<T>, IOrderedQueryable<T>> Desc)[] choices)
    {
        var sortBy = OptionalText(sort?.SortBy) ?? defaultSortBy;
        var choice = choices.FirstOrDefault(item => string.Equals(item.SortBy, sortBy, StringComparison.OrdinalIgnoreCase));
        if (choice.SortBy is null)
        {
            choice = choices.First(item => string.Equals(item.SortBy, defaultSortBy, StringComparison.OrdinalIgnoreCase));
        }

        return sort?.Direction == SortDirection.Asc
            ? choice.Asc(source)
            : choice.Desc(source);
    }

    private static async Task<PagedResult<TResponse>> ToPagedResultAsync<TEntity, TResponse>(
        IQueryable<TEntity> source,
        SearchQuery query,
        Func<TEntity, TResponse> map,
        CancellationToken cancellationToken)
    {
        var page = query.EffectivePage;
        var totalCount = await source.LongCountAsync(cancellationToken);
        var entities = await source
            .Skip(page.Offset)
            .Take(page.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<TResponse>(
            entities.Select(map).ToArray(),
            page.Page,
            page.PageSize,
            totalCount);
    }

    private void AddConnectorAudit(
        ProjectExecutionContext context,
        string action,
        ExternalConnectorSettingEntity connector,
        object? before)
        => auditLogWriter.Add(
            context,
            new AuditLogWriteRequest(
                action,
                AuditLogResourceTypes.ExternalConnector,
                connector.Id.ToString("D"),
                new
                {
                    before,
                    after = ToConnectorAuditSnapshot(connector)
                }));

    private static object ToConnectorAuditSnapshot(ExternalConnectorSettingEntity entity)
        => new
        {
            connectorId = entity.Id,
            workspaceId = entity.WorkspaceId,
            projectId = entity.ProjectId,
            connectorType = entity.ConnectorType,
            name = entity.Name,
            authRef = entity.AuthRef,
            settings = ParseJsonElement(entity.SettingsJson, "{}"),
            status = entity.Status,
            disabledAt = entity.DisabledAt
        };

    private static object ToRunAuditSnapshot(ExternalConnectorRunEntity entity)
        => new
        {
            runId = entity.Id,
            connectorId = entity.ConnectorSettingId,
            workspaceId = entity.WorkspaceId,
            projectId = entity.ProjectId,
            runType = entity.RunType,
            status = entity.Status,
            resultSummary = ParseJsonElement(entity.ResultSummaryJson, "{}"),
            startedAt = entity.StartedAt,
            completedAt = entity.CompletedAt
        };

    private static ConnectorSettingsDetails MapConnector(ExternalConnectorSettingEntity entity)
        => new(
            entity.Id,
            entity.WorkspaceId,
            entity.ProjectId,
            entity.ConnectorType,
            entity.Name,
            entity.AuthRef,
            ParseJsonElement(entity.SettingsJson, "{}"),
            entity.Status,
            entity.CreatedAt,
            entity.UpdatedAt,
            entity.DisabledAt);

    private static ConnectorRunDetails MapRun(ExternalConnectorRunEntity entity)
        => new(
            entity.Id,
            entity.ConnectorSettingId,
            entity.WorkspaceId,
            entity.ProjectId,
            entity.RunType,
            entity.Status,
            ParseJsonElement(entity.RequestJson, "{}"),
            ParseJsonElement(entity.ResultSummaryJson, "{}"),
            ParseNullableJsonElement(entity.ErrorJson),
            entity.StartedAt,
            entity.CompletedAt,
            entity.CreatedAt);

    private static IReadOnlyList<string> ReadSettingsKeys(string settingsJson)
    {
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(settingsJson) ? "{}" : settingsJson);
            return document.RootElement.ValueKind == JsonValueKind.Object
                ? document.RootElement.EnumerateObject().Select(property => property.Name).ToArray()
                : [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static JsonElement ParseJsonElement(string? json, string fallback)
    {
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? fallback : json);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            using var document = JsonDocument.Parse(fallback);
            return document.RootElement.Clone();
        }
    }

    private static JsonElement? ParseNullableJsonElement(string? json)
        => string.IsNullOrWhiteSpace(json) ? null : ParseJsonElement(json, "{}");

    private DateTime NowUtc()
        => timeProvider.GetUtcNow().UtcDateTime;

    private static Result<T> ValidationFailure<T>(ValidationErrors errors)
        => Result<T>.Failure(Error.Validation("Validation failed.", errors.ToDictionary()));

    private static Result<T> Failure<T>(ErrorCode code, string message)
        => Result<T>.Failure(new Error(code, message));

    private static string? RequireText(
        string? value,
        string target,
        int maxLength,
        ValidationErrors errors)
    {
        var text = OptionalText(value);
        if (text is null)
        {
            errors.Add(target, $"{target} is required.");
            return null;
        }

        if (text.Length > maxLength)
        {
            errors.Add(target, $"{target} must be {maxLength} characters or fewer.");
            return null;
        }

        return text;
    }

    private static string? OptionalText(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed class ValidationErrors
    {
        private readonly Dictionary<string, List<string>> errors = new(StringComparer.OrdinalIgnoreCase);

        public bool HasErrors => errors.Count > 0;

        public void Add(string target, string message)
        {
            if (!errors.TryGetValue(target, out var messages))
            {
                messages = [];
                errors[target] = messages;
            }

            messages.Add(message);
        }

        public IReadOnlyDictionary<string, string[]> ToDictionary()
            => errors.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.ToArray(),
                StringComparer.OrdinalIgnoreCase);
    }

    private sealed record ConnectorValidationResult(
        ValidationErrors Errors,
        string? ConnectorType,
        string? Name,
        string? AuthRef,
        string? SettingsJson,
        string? Status);
}
