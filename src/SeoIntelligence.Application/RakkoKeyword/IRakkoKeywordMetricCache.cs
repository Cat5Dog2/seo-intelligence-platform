namespace SeoIntelligence.Application.RakkoKeyword;

public interface IRakkoKeywordMetricCache
{
    Task<RakkoKeywordMetricCacheDecision> CanReuseAsync(
        RakkoKeywordMetricCacheLookup lookup,
        CancellationToken cancellationToken = default);
}

public sealed record RakkoKeywordMetricCacheLookup(
    Guid KeywordId,
    string Location,
    string Language,
    string ContractScopeKey);

public sealed record RakkoKeywordMetricCacheDecision(
    bool CanReuse,
    string Reason,
    Guid? SourceCallId)
{
    public static RakkoKeywordMetricCacheDecision Reusable(Guid? sourceCallId)
        => new(true, "contract_scope_matched", sourceCallId);

    public static RakkoKeywordMetricCacheDecision NotReusable(string reason)
        => new(false, reason, null);
}
