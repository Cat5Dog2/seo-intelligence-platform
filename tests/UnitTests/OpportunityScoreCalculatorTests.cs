using SeoIntelligence.Application.Services;

namespace UnitTests;

public sealed class OpportunityScoreCalculatorTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void CalculateAppliesDesignFormulaNormalizationAndDefaults()
    {
        var topKeywordId = Guid.Parse("018f3f12-0012-7000-8000-000000000001");
        var zeroVolumeKeywordId = Guid.Parse("018f3f12-0012-7000-8000-000000000002");

        var results = OpportunityScoreCalculator.Calculate(
        [
            new OpportunityScoreCalculationInput(
                topKeywordId,
                SearchVolume: 999,
                SeoDifficulty: 25m,
                Cpc: 10m,
                Competition: 90m,
                new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["2026-01"] = 100,
                    ["2026-04"] = 150
                },
                Relevance: 80m),
            new OpportunityScoreCalculationInput(
                zeroVolumeKeywordId,
                SearchVolume: 0,
                SeoDifficulty: 90m,
                Cpc: 0m,
                Competition: 30m,
                MonthlySearchVolumes: null,
                Relevance: null)
        ]);

        var top = Assert.Single(results, result => result.KeywordId == topKeywordId);
        Assert.Equal(90m, top.OpportunityScore);
        Assert.Equal(1m, top.Components.VolumeScore);
        Assert.Equal(0.75m, top.Components.DifficultyScore);
        Assert.Equal(1.5m, top.Components.TrendScore);
        Assert.Equal(1m, top.Components.CommercialScore);
        Assert.Equal(0.8m, top.Components.RelevanceScore);
        Assert.Equal(50m, top.Components.ChangeRate3m);

        var zeroVolume = Assert.Single(results, result => result.KeywordId == zeroVolumeKeywordId);
        Assert.Equal(0m, zeroVolume.OpportunityScore);
        Assert.Equal(0m, zeroVolume.Components.VolumeScore);
        Assert.Equal(1m, zeroVolume.Components.TrendScore);
        Assert.Equal(0.6m, zeroVolume.Components.RelevanceScore);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void CalculateUsesZeroVolumeAndCommercialScoresWhenExecutionUnitCannotNormalize()
    {
        var keywordId = Guid.Parse("018f3f12-0012-7000-8000-000000000003");

        var result = Assert.Single(OpportunityScoreCalculator.Calculate(
        [
            new OpportunityScoreCalculationInput(
                keywordId,
                SearchVolume: 0,
                SeoDifficulty: 10m,
                Cpc: 5m,
                Competition: 5m,
                new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["2026-04"] = 0,
                    ["2026-05"] = 10
                },
                Relevance: 0.4m)
        ]));

        Assert.Equal(0m, result.OpportunityScore);
        Assert.Equal(0m, result.Components.VolumeScore);
        Assert.Equal(0m, result.Components.CommercialScore);
        Assert.Null(result.Components.ChangeRate3m);
        Assert.Equal(1m, result.Components.TrendScore);
    }
}
