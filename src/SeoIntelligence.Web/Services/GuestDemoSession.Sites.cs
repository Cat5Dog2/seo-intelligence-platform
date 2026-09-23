using SeoIntelligence.Application.Services;

namespace SeoIntelligence.Web.Services;

public sealed partial class GuestDemoSession
{
    private ApiClientResult<T> Sites<T>(HttpMethod method, string route, object? body, Guid projectId, Dictionary<string, string> query)
    {
        var parts = route.Split('/');
        if (parts.Length == 1)
        {
            if (method == HttpMethod.Get)
                return Page<T, SiteDetails>(_sites.Values.Where(site => site.ProjectId == projectId && MatchesStatus(site.Status, query)), query, site => site.Domain, site => site.CreatedAt);
            if (method == HttpMethod.Post && body is SiteCreateRequest create)
            {
                if (!ValidSite(create.Domain, create.CanonicalUrl, create.Type, create.Memo)) return Invalid<T>("ドメイン、http/httpsの正規URL、サイト種別を確認してください。");
                if (_sites.Count >= 30) return Limit<T>("デモのサイトは30件まで作成できます。");
                if (_sites.Values.Any(site => site.ProjectId == projectId && site.Domain == create.Domain!.Trim())) return Conflict<T>("同じドメインのサイトが存在します。");
                var site = new SiteDetails(Guid.NewGuid(), projectId, create.Domain!.Trim(), create.CanonicalUrl!, create.Type!, create.Memo, "active", DateTime.UtcNow, DateTime.UtcNow, null);
                _sites.Add(site.SiteId, site);
                return Ok<T>(site);
            }
        }
        if (parts.Length < 2 || !Guid.TryParse(parts[1], out var siteId) || !_sites.TryGetValue(siteId, out var current) || current.ProjectId != projectId) return Missing<T>();
        if (method == HttpMethod.Get && parts.Length == 2) return Ok<T>(current);
        if (method == HttpMethod.Put && body is SiteUpdateRequest update)
        {
            if (!ValidSite(update.Domain, update.CanonicalUrl, update.Type, update.Memo)) return Invalid<T>("サイトの入力値を確認してください。");
            if (_sites.Values.Any(site => site.SiteId != siteId && site.ProjectId == projectId && site.Domain == update.Domain!.Trim())) return Conflict<T>("同じドメインのサイトが存在します。");
            current = current with { Domain = update.Domain!.Trim(), CanonicalUrl = update.CanonicalUrl!, Type = update.Type!, Memo = update.Memo, UpdatedAt = DateTime.UtcNow };
        }
        else if (method == HttpMethod.Delete && parts.Length == 2) current = current with { Status = "archived", ArchivedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        else if (method == HttpMethod.Post && parts is [_, _, "restore"]) current = current with { Status = "active", ArchivedAt = null, UpdatedAt = DateTime.UtcNow };
        else return Unsupported<T>();
        _sites[siteId] = current;
        return Ok<T>(current);
    }

    private static bool ValidSite(string? domain, string? url, string? type, string? memo)
        => !string.IsNullOrWhiteSpace(domain) && domain.Length <= 253
            && Uri.CheckHostName(domain.Trim()) == UriHostNameType.Dns
            && url?.Length <= 2000 && Uri.TryCreate(url, UriKind.Absolute, out var parsed) && parsed.Scheme is "http" or "https"
            && type is "own" or "competitor" or "reference" && (memo?.Length ?? 0) <= 1000;
}
