using System.Net;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using SeoIntelligence.Application.Jobs;
using SeoIntelligence.Application.Services;
using SeoIntelligence.Contracts.Api;

namespace SeoIntelligence.Web.Services;

/// <summary>A bounded, session-owned demo. It has no database, secret store, worker or HTTP dependencies.</summary>
public sealed partial class GuestDemoSession
{
    private const int MaxProjects = 10;
    private const int MaxJobs = 50;
    private const int MaxKeywords = 100;
    private readonly Lock _gate = new();
    private readonly Guid _workspaceId = Guid.NewGuid();
    private readonly Dictionary<Guid, ProjectDetails> _projects = [];
    private readonly Dictionary<Guid, SiteDetails> _sites = [];
    private readonly Dictionary<Guid, JobDetails> _jobs = [];
    private readonly Dictionary<Guid, KeywordDiscoveryResult> _discoveries = [];
    private readonly Dictionary<Guid, IReadOnlyList<SearchVolumeResultRow>> _volumes = [];
    private readonly Dictionary<Guid, DataExportDetails> _exports = [];
    private readonly Dictionary<Guid, string> _files = [];
    private static readonly JsonElement EmptyJson = JsonSerializer.SerializeToElement(new { });

    public GuestDemoSession(DateTimeOffset expiresAt)
    {
        ExpiresAt = expiresAt;
        var project = NewProject("デモプロジェクト", "jp", "ja", EmptyJson, "ゲスト専用のサンプルデータです。");
        _projects.Add(project.ProjectId, project);
    }

    public DateTimeOffset ExpiresAt { get; }

    public ApiClientResult<T> Execute<T>(HttpMethod method, string path, object? body)
    {
        lock (_gate)
        {
            var uri = new Uri("https://demo.invalid" + path);
            var query = QueryHelpers.ParseQuery(uri.Query).ToDictionary(item => item.Key, item => item.Value.ToString());
            var parts = uri.AbsolutePath.Trim('/').Split('/');
            if (method == HttpMethod.Get)
            {
                switch (uri.AbsolutePath)
                {
                    case "/api/master-data/locations":
                        return Ok<T>(new LocationSummary[] { new("rakko_keyword", "jp", "日本", "JP", "active") });
                    case "/api/master-data/languages":
                        return Ok<T>(new LanguageSummary[] { new("rakko_keyword", "ja", "日本語", "active") });
                    case "/api/admin/external-api-calls":
                        return Ok<T>(Array.Empty<ExternalApiCallDetails>());
                    case "/api/admin/notification-channels":
                        return Ok<T>(Array.Empty<NotificationChannelDetails>());
                    case "/api/jobs":
                        // Match the real job API: page 1 must contain the most recent jobs.
                        query.TryAdd("orderBy", "desc");
                        return Page<T, JobDetails>(_jobs.Values.Where(job =>
                            (!query.TryGetValue("project_id", out var project) || job.ProjectId.ToString() == project)
                            && (!query.TryGetValue("job_type", out var type) || job.JobType == type)
                            && MatchesStatus(job.Status, query, "all")), query, job => job.JobType, job => job.CreatedAt);
                }
                if (parts is ["api", "jobs", var jobId] && Guid.TryParse(jobId, out var id))
                {
                    return _jobs.TryGetValue(id, out var job) ? Ok<T>(job) : Missing<T>();
                }
            }

            if (uri.AbsolutePath == "/api/projects")
            {
                if (method == HttpMethod.Get)
                    return Page<T, ProjectDetails>(_projects.Values.Where(project => MatchesStatus(project.Status, query)), query, project => project.Name, project => project.CreatedAt);
                if (method == HttpMethod.Post && body is ProjectCreateRequest create)
                {
                    if (!ValidProject(create.Name, create.DefaultLocation, create.DefaultLanguage, create.Memo, create.Kpi)) return Invalid<T>("プロジェクト名・地域・言語を入力してください。デモでは名前100文字、メモ1,000文字、KPI4,000文字までです。");
                    if (_projects.Count >= MaxProjects) return Limit<T>("プロジェクトは10件まで作成できます。");
                    if (_projects.Values.Any(project => project.Name == create.Name!.Trim())) return Conflict<T>("同名のプロジェクトが存在します。");
                    var project = NewProject(create.Name!.Trim(), create.DefaultLocation!, create.DefaultLanguage!, create.Kpi ?? EmptyJson, create.Memo);
                    _projects.Add(project.ProjectId, project);
                    return Ok<T>(project);
                }
            }

            if (parts.Length < 3 || parts[0] != "api" || parts[1] != "projects") return Unsupported<T>();
            if (!Guid.TryParse(parts[2], out var projectId) || !_projects.TryGetValue(projectId, out var current)) return Missing<T>();
            var route = string.Join('/', parts.Skip(3));
            if (route is "" or "restore")
            {
                if (method == HttpMethod.Get && route == "") return Ok<T>(current);
                if (method == HttpMethod.Put && body is ProjectUpdateRequest update)
                {
                    if (!ValidProject(update.Name, update.DefaultLocation, update.DefaultLanguage, update.Memo, update.Kpi)) return Invalid<T>("プロジェクトの入力値を確認してください。");
                    if (_projects.Values.Any(project => project.ProjectId != projectId && project.Name == update.Name!.Trim())) return Conflict<T>("同名のプロジェクトが存在します。");
                    current = current with { Name = update.Name!.Trim(), DefaultLocation = update.DefaultLocation!, DefaultLanguage = update.DefaultLanguage!, Kpi = update.Kpi ?? EmptyJson, Memo = update.Memo, UpdatedAt = DateTime.UtcNow };
                }
                else if (method == HttpMethod.Delete && route == "") current = current with { Status = "archived", ArchivedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
                else if (method == HttpMethod.Post && route == "restore") current = current with { Status = "active", ArchivedAt = null, UpdatedAt = DateTime.UtcNow };
                else return Unsupported<T>();
                _projects[projectId] = current;
                return Ok<T>(current);
            }
            if (current.Status != "active") return Missing<T>();
            if (route == "sites" || route.StartsWith("sites/", StringComparison.Ordinal)) return Sites<T>(method, route, body, projectId, query);

            if (method == HttpMethod.Post)
            {
                if (_jobs.Count >= MaxJobs) return Limit<T>("デモでの実行は50回までです。ゲストログインし直すと初期化されます。");
                if (route == "keyword-discovery/suggest" && body is KeywordDiscoveryRequest discovery)
                {
                    var seed = discovery.SeedKeyword ?? discovery.Seeds?.FirstOrDefault();
                    if (string.IsNullOrWhiteSpace(seed) || seed.Length > 100) return Invalid<T>("シードキーワードを1〜100文字で入力してください。");
                    var candidates = new[] { "とは", "方法", "比較", "おすすめ", "始め方" }.Select((suffix, index) =>
                        new KeywordCandidate($"{seed.Trim()} {suffix}", "suggest", "+", 80 - index * 5, Guid.NewGuid(),
                            Engine: "google", SearchVolume: 1200 - index * 150, SeoDifficulty: 25 + index * 3, Cpc: 0.5m, Competition: 0.3m)).ToArray();
                    var job = AddJob(projectId, "KeywordDiscoveryJob");
                    var result = new KeywordDiscoveryResult(candidates, SeedKeyword: seed.Trim(), Location: current.DefaultLocation, Language: current.DefaultLanguage,
                        Sources: ["suggest"], JobId: job.JobId, SourceStatuses: [new("suggest", "succeeded", candidates.Length)]);
                    _discoveries.Add(job.JobId, result);
                    return Ok<T>(result);
                }
                if (route == "search-volume/jobs" && body is SearchVolumeJobRequest volume)
                {
                    if (volume.Keywords is null || volume.Keywords.Count is < 1 or > MaxKeywords || volume.Keywords.Any(keyword => string.IsNullOrWhiteSpace(keyword) || keyword.Length > 100)
                        || string.IsNullOrWhiteSpace(volume.Location) || string.IsNullOrWhiteSpace(volume.Language))
                        return Invalid<T>("デモの一括調査は1〜100件、各キーワード100文字以内で指定してください。地域・言語は必須です。");
                    var job = AddJob(projectId, "RegisterSearchVolumeJob");
                    _volumes.Add(job.JobId, volume.Keywords.Select(keyword => keyword.Trim()).Distinct(StringComparer.Ordinal).Select((keyword, index) =>
                        new SearchVolumeResultRow(keyword, 1200 + index * 100, 30, 0.5m, 0.3m, DataSource: "Mock", KeywordId: Guid.NewGuid())).ToArray());
                    return Ok<T>(new JobReference(job.JobId, job.Status));
                }
                if (route == "exports/csv" && body is DataExportRequest export)
                {
                    if (export.ExportType is not ("keyword_candidates" or "search_volume_results") || export.Format is not (null or "csv"))
                        return Unsupported<T>();
                    if (export.Filter is { ValueKind: not JsonValueKind.Object }) return Invalid<T>("CSVの絞り込み条件が不正です。");
                    var filter = export.Filter;
                    var q = JsonText(filter, "q") ?? "";
                    string csv;
                    if (export.ExportType == "search_volume_results")
                    {
                        if (!Guid.TryParse(JsonText(filter, "jobId"), out var sourceJobId)) return Invalid<T>("出力する検索ボリュームジョブを選択してください。");
                        if (!_jobs.TryGetValue(sourceJobId, out var sourceJob) || sourceJob.ProjectId != projectId || !_volumes.TryGetValue(sourceJobId, out var rows)) return Missing<T>();
                        csv = "keyword,searchVolume,seoDifficulty,cpc,competition,dataSource\r\n" + string.Join("\r\n", rows
                            .Where(row => row.Keyword.Contains(q, StringComparison.OrdinalIgnoreCase))
                            .Select(row => string.Join(',', CsvCell(row.Keyword), Number(row.SearchVolume), Number(row.SeoDifficulty), Number(row.Cpc), Number(row.Competition), "Mock")));
                    }
                    else
                    {
                        var source = JsonText(filter, "source");
                        var candidates = _discoveries.Where(pair => _jobs[pair.Key].ProjectId == projectId).SelectMany(pair => pair.Value.Candidates)
                            .Where(candidate => candidate.Keyword.Contains(q, StringComparison.OrdinalIgnoreCase) && (source is null || source == candidate.Source));
                        csv = "keyword,source,searchVolume,seoDifficulty,dataSource\r\n" + string.Join("\r\n", candidates.Select(candidate =>
                            string.Join(',', CsvCell(candidate.Keyword), CsvCell(candidate.Source), Number(candidate.SearchVolume), Number(candidate.SeoDifficulty), "Mock")));
                    }
                    var id = Guid.NewGuid();
                    var job = AddJob(projectId, "DataExportJob", new JobResultResource("data_export", id));
                    _files[id] = csv;
                    _exports[id] = new DataExportDetails(id, projectId, export.ExportType, "csv", "succeeded", null, job.CreatedAt, job.CreatedAt);
                    return Ok<T>(new JobReference(job.JobId, job.Status));
                }
            }
            if (method == HttpMethod.Get)
            {
                if (route == "dashboard")
                {
                    var discoveries = _discoveries.Where(pair => _jobs[pair.Key].ProjectId == projectId).Select(pair => pair.Value).ToArray();
                    var volumes = _volumes.Where(pair => _jobs[pair.Key].ProjectId == projectId).Select(pair => pair.Value).ToArray();
                    return Ok<T>(new DashboardSnapshot(discoveries.Sum(item => item.Candidates.Count), 0, 0, 0,
                        discoveries.Length, volumes.Length, volumes.Sum(item => item.Count)));
                }
                if (parts.Length >= 6 && parts[4] == "jobs" && Guid.TryParse(parts[5], out var id))
                {
                    if (!_jobs.TryGetValue(id, out var job) || job.ProjectId != projectId) return Missing<T>();
                    if (parts[3] == "keyword-discovery" && route.EndsWith("/results", StringComparison.Ordinal))
                        return _discoveries.TryGetValue(id, out var discovery) ? Ok<T>(discovery) : Missing<T>();
                    if (parts[3] == "search-volume" && _volumes.TryGetValue(id, out var rows))
                        return route.EndsWith("/results", StringComparison.Ordinal)
                            ? Page<T, SearchVolumeResultRow>(rows, query, row => row.Keyword, row => row.Keyword, row => query.GetValueOrDefault("sortBy") switch
                            {
                                "searchVolume" => row.SearchVolume ?? 0,
                                "seoDifficulty" => row.SeoDifficulty ?? 0,
                                "cpc" => row.Cpc ?? 0,
                                "competition" => row.Competition ?? 0,
                                _ => row.Keyword
                            })
                            : Ok<T>(new JobReference(job.JobId, job.Status));
                }
                if (parts.Length >= 5 && parts[3] == "exports" && Guid.TryParse(parts[4], out var exportId))
                {
                    if (!_exports.TryGetValue(exportId, out var export) || export.ProjectId != projectId) return Missing<T>();
                    if (parts.Length == 5) return Ok<T>(export);
                    if (parts[5] == "download") return Ok<T>(new DataExportDownload(exportId, $"/api/projects/{projectId}/exports/{exportId}/content"));
                    if (parts[5] == "content")
                    {
                        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(_files[exportId])).ToArray()) };
                        return Ok<T>(new ApiFileResponse(response, response.Content.ReadAsStream(), "text/csv; charset=utf-8", "guest-demo.csv"));
                    }
                }
                return ReadSample<T>(route, projectId, query);
            }
            return Unsupported<T>();
        }
    }

    private JobDetails AddJob(Guid projectId, string type, JobResultResource? resource = null)
    {
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var job = new JobDetails(id, _workspaceId, projectId, type, "succeeded", 100, $"/api/jobs/{id}", null, resource, 0, null, null, "guest", now, now, now);
        _jobs.Add(id, job);
        return job;
    }

    private ProjectDetails NewProject(string name, string location, string language, JsonElement kpi, string? memo)
        => new(Guid.NewGuid(), _workspaceId, name, location, language, kpi.Clone(), memo, "active", DateTime.UtcNow, DateTime.UtcNow, null);

    private static bool ValidProject(string? name, string? location, string? language, string? memo, JsonElement? kpi)
        => !string.IsNullOrWhiteSpace(name) && name.Length <= 100
            && !string.IsNullOrWhiteSpace(location) && location.Length <= 20
            && !string.IsNullOrWhiteSpace(language) && language.Length <= 20
            && (memo?.Length ?? 0) <= 1000 && (kpi?.GetRawText().Length ?? 0) <= 4000;

    private static bool MatchesStatus(string status, Dictionary<string, string> query, string fallback = "active")
        => query.GetValueOrDefault("status", fallback) is "all" || query.GetValueOrDefault("status", fallback) == status;

    private static ApiClientResult<T> Page<T, TRow>(IEnumerable<TRow> rows, Dictionary<string, string> query,
        Func<TRow, string> text, Func<TRow, IComparable> defaultSort, Func<TRow, IComparable>? sort = null)
    {
        if (!int.TryParse(query.GetValueOrDefault("page", "1"), out var page) || page < 1
            || !int.TryParse(query.GetValueOrDefault("pageSize", "50"), out var size) || size is < 1 or > 200)
            return Invalid<T>("ページは1以上、表示件数は1〜200で指定してください。");
        var q = query.GetValueOrDefault("q", "");
        var filtered = rows.Where(row => text(row).Contains(q, StringComparison.OrdinalIgnoreCase)).ToArray();
        var sortKey = sort ?? (query.GetValueOrDefault("sortBy") == "name" ? row => text(row) : defaultSort);
        var comparer = Comparer<IComparable>.Create((left, right) => left is string a && right is string b
            ? StringComparer.Ordinal.Compare(a, b)
            : left.CompareTo(right));
        var ordered = query.GetValueOrDefault("orderBy", "asc") == "desc" ? filtered.OrderByDescending(sortKey, comparer) : filtered.OrderBy(sortKey, comparer);
        var skip = (long)(page - 1) * size;
        var items = skip > int.MaxValue ? Array.Empty<TRow>() : ordered.Skip((int)skip).Take(size).ToArray();
        return Ok<T>(items, new ApiResponseMeta(Page: new PageMeta(page, size, filtered.Length, (long)Math.Ceiling((double)filtered.Length / size))));
    }

    private static string CsvCell(string value)
    {
        if (value.Length > 0 && "=+-@\t\r\n".Contains(value[0])) value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private static string Number(IFormattable? value) => value?.ToString(null, CultureInfo.InvariantCulture) ?? "";
    private static string? JsonText(JsonElement? json, string property)
        => json is { ValueKind: JsonValueKind.Object } element && element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static ApiClientResult<T> Ok<T>(object data, ApiResponseMeta? meta = null)
        => data is T typed ? ApiClientResult<T>.Success(typed, meta ?? ApiResponseMeta.Empty, HttpStatusCode.OK, Guid.NewGuid().ToString("N")) : Unsupported<T>();
    private static ApiClientResult<T> Missing<T>() => Error<T>("Guest.NotFound", "デモ内に対象データがありません。", HttpStatusCode.NotFound);
    private static ApiClientResult<T> Invalid<T>(string message) => Error<T>("Guest.Validation", message, HttpStatusCode.BadRequest);
    private static ApiClientResult<T> Limit<T>(string message) => Error<T>("Guest.Limit", message, HttpStatusCode.BadRequest);
    private static ApiClientResult<T> Conflict<T>(string message) => Error<T>("Guest.Conflict", message, HttpStatusCode.Conflict);
    private static ApiClientResult<T> Unsupported<T>() => Error<T>("Guest.Unsupported", "この操作はゲストデモでは利用できません。プロジェクト・サイト編集、キーワード探索、検索ボリューム調査、CSV出力をお試しください。", HttpStatusCode.Forbidden);
    private static ApiClientResult<T> Error<T>(string code, string message, HttpStatusCode status) => ApiClientResult<T>.Failure([new ApiError(code, message)], statusCode: status);
}
