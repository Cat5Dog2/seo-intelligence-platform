namespace SeoIntelligence.Application.Services;

public static class OpportunityScoreCalculator
{
    private const decimal DefaultRelevanceScore = 0.6m;
    private const decimal DefaultTrendScore = 1.0m;

    public static IReadOnlyList<OpportunityScoreCalculationResult> Calculate(
        IReadOnlyList<OpportunityScoreCalculationInput> inputs)
    {
        if (inputs.Count == 0)
        {
            return [];
        }

        var maxVolume = inputs.Max(input => Math.Max(0, input.SearchVolume ?? 0));
        var cpcRange = ValueRange.Create(inputs.Select(input => input.Cpc));
        var competitionRange = ValueRange.Create(inputs.Select(input => input.Competition));

        return inputs.Select(input =>
        {
            var searchVolume = Math.Max(0, input.SearchVolume ?? 0);
            var volumeScore = maxVolume == 0
                ? 0m
                : ToDecimal(Math.Log10(searchVolume + 1d) / Math.Log10(maxVolume + 1d));
            var difficultyScore = 1m - (Clamp(input.SeoDifficulty ?? 0m, 0m, 100m) / 100m);
            var normalizedCpc = cpcRange.Normalize(input.Cpc);
            var normalizedCompetition = competitionRange.Normalize(input.Competition);
            var commercialScore = (normalizedCpc * 0.7m) + (normalizedCompetition * 0.3m);
            var relevanceScore = NormalizeRelevance(input.Relevance);
            var changeRate3m = CalculateChangeRate3m(input.MonthlySearchVolumes);
            var trendScore = changeRate3m.HasValue
                ? Clamp(1m + (changeRate3m.Value / 100m), 0.5m, 1.8m)
                : DefaultTrendScore;
            var opportunityScore = 100m *
                volumeScore *
                difficultyScore *
                trendScore *
                (0.7m + (0.3m * commercialScore)) *
                relevanceScore;

            return new OpportunityScoreCalculationResult(
                input.KeywordId,
                Round4(Clamp(opportunityScore, 0m, 100m)),
                new OpportunityScoreComponents(
                    Round4(volumeScore),
                    Round4(difficultyScore),
                    Round4(trendScore),
                    Round4(commercialScore),
                    Round4(relevanceScore),
                    Round4(normalizedCpc),
                    Round4(normalizedCompetition),
                    changeRate3m.HasValue ? Round4(changeRate3m.Value) : null,
                    searchVolume,
                    maxVolume,
                    input.SeoDifficulty ?? 0m,
                    input.Cpc,
                    input.Competition,
                    input.SourceCallId,
                    input.MetricId));
        }).ToArray();
    }

    private static decimal NormalizeRelevance(decimal? relevance)
    {
        if (!relevance.HasValue)
        {
            return DefaultRelevanceScore;
        }

        var value = relevance.Value;
        return value <= 1m
            ? Clamp(value, 0m, 1m)
            : Clamp(value / 100m, 0m, 1m);
    }

    private static decimal? CalculateChangeRate3m(IReadOnlyDictionary<string, int>? monthlySearchVolumes)
    {
        if (monthlySearchVolumes is null || monthlySearchVolumes.Count < 2)
        {
            return null;
        }

        var ordered = monthlySearchVolumes
            .Where(pair => IsYearMonth(pair.Key))
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ToArray();
        if (ordered.Length < 2)
        {
            return null;
        }

        var newest = ordered[^1].Value;
        var baseline = ordered[Math.Max(0, ordered.Length - 4)].Value;
        if (baseline <= 0)
        {
            return null;
        }

        return ((newest - baseline) / (decimal)baseline) * 100m;
    }

    private static bool IsYearMonth(string value)
        => value.Length == 7 &&
            value[4] == '-' &&
            int.TryParse(value.AsSpan(0, 4), out _) &&
            int.TryParse(value.AsSpan(5, 2), out var month) &&
            month is >= 1 and <= 12;

    private static decimal Clamp(decimal value, decimal min, decimal max)
        => Math.Min(max, Math.Max(min, value));

    private static decimal Round4(decimal value)
        => Math.Round(value, 4, MidpointRounding.AwayFromZero);

    private static decimal ToDecimal(double value)
        => double.IsFinite(value) ? (decimal)value : 0m;

    private readonly record struct ValueRange(decimal Min, decimal Max, bool CanNormalize)
    {
        public static ValueRange Create(IEnumerable<decimal?> values)
        {
            var present = values
                .Where(value => value.HasValue)
                .Select(value => value!.Value)
                .ToArray();
            if (present.Length == 0)
            {
                return new ValueRange(0m, 0m, CanNormalize: false);
            }

            var min = present.Min();
            var max = present.Max();
            return new ValueRange(min, max, max > min);
        }

        public decimal Normalize(decimal? value)
        {
            if (!CanNormalize || !value.HasValue)
            {
                return 0m;
            }

            return Clamp((value.Value - Min) / (Max - Min), 0m, 1m);
        }
    }
}

public sealed record OpportunityScoreCalculationInput(
    Guid KeywordId,
    int? SearchVolume,
    decimal? SeoDifficulty,
    decimal? Cpc,
    decimal? Competition,
    IReadOnlyDictionary<string, int>? MonthlySearchVolumes,
    decimal? Relevance = null,
    Guid? SourceCallId = null,
    Guid? MetricId = null);

public sealed record OpportunityScoreCalculationResult(
    Guid KeywordId,
    decimal OpportunityScore,
    OpportunityScoreComponents Components);

public sealed record OpportunityScoreComponents(
    decimal VolumeScore,
    decimal DifficultyScore,
    decimal TrendScore,
    decimal CommercialScore,
    decimal RelevanceScore,
    decimal NormalizedCpc,
    decimal NormalizedCompetition,
    decimal? ChangeRate3m,
    int SearchVolume,
    int MaxVolume,
    decimal SeoDifficulty,
    decimal? Cpc,
    decimal? Competition,
    Guid? SourceCallId,
    Guid? MetricId);
