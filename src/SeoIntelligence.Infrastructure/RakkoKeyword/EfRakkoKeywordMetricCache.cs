using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SeoIntelligence.Application.RakkoKeyword;
using SeoIntelligence.Infrastructure.Persistence;

namespace SeoIntelligence.Infrastructure.RakkoKeyword;

internal sealed class OptionalEfRakkoKeywordMetricCache(IServiceProvider serviceProvider) : IRakkoKeywordMetricCache
{
    public async Task<RakkoKeywordMetricCacheDecision> CanReuseAsync(
        RakkoKeywordMetricCacheLookup lookup,
        CancellationToken cancellationToken = default)
    {
        var dbContext = serviceProvider.GetService<SeoIntelligenceDbContext>();
        if (dbContext is null)
        {
            return RakkoKeywordMetricCacheDecision.NotReusable("database_not_configured");
        }

        var sameScopeMetric = await dbContext.KeywordMetrics
            .AsNoTracking()
            .Where(entity =>
                entity.KeywordId == lookup.KeywordId &&
                entity.Location == lookup.Location &&
                entity.Language == lookup.Language &&
                entity.ContractScopeKey == lookup.ContractScopeKey)
            .OrderByDescending(entity => entity.FetchedAt)
            .Select(entity => entity.SourceCallId)
            .FirstOrDefaultAsync(cancellationToken);

        if (sameScopeMetric.HasValue)
        {
            return RakkoKeywordMetricCacheDecision.Reusable(sameScopeMetric);
        }

        var hasDifferentScopeMetric = await dbContext.KeywordMetrics
            .AsNoTracking()
            .AnyAsync(
                entity =>
                    entity.KeywordId == lookup.KeywordId &&
                    entity.Location == lookup.Location &&
                    entity.Language == lookup.Language &&
                    entity.ContractScopeKey != lookup.ContractScopeKey,
                cancellationToken);

        return hasDifferentScopeMetric
            ? RakkoKeywordMetricCacheDecision.NotReusable("contract_scope_mismatch")
            : RakkoKeywordMetricCacheDecision.NotReusable("cache_miss");
    }
}
