using SeoIntelligence.Infrastructure.Services;

namespace ContractTests;

/// <summary>
/// ラッコキーワードAPI v1.14.0の <c>entryNo</c> は <c>type: number</c> で宣言されているため、
/// 小数や0以下の値が届き得る。<c>rank_results.entry_no</c> は
/// <c>GET /v1/search-rank/{requestId}/results/{entryNo}/serp</c> のパスに使うので、
/// 丸めて別のSERPを指してしまうより破棄してnullにする。その境界をここで固定する。
/// </summary>
public sealed class RankResultEntryNoContractTests
{
    [Theory]
    [Trait("Category", "Contract")]
    [InlineData(1, 1)]
    [InlineData(3, 3)]
    [InlineData(2147483647, int.MaxValue)]
    public void IntegralEntryNoWithinRangeIsStored(int value, int expected)
        => Assert.Equal(expected, RankMonitoringService.ToEntryNo(value));

    [Theory]
    [Trait("Category", "Contract")]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveOrMissingEntryNoIsDiscarded(int? value)
        => Assert.Null(RankMonitoringService.ToEntryNo(value));

    [Theory]
    [Trait("Category", "Contract")]
    [InlineData("3.5")]
    [InlineData("0.9")]
    [InlineData("-2.5")]
    public void FractionalEntryNoIsDiscardedRatherThanRounded(string value)
        => Assert.Null(RankMonitoringService.ToEntryNo(decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture)));

    [Fact]
    [Trait("Category", "Contract")]
    public void EntryNoAboveIntMaxIsDiscarded()
        => Assert.Null(RankMonitoringService.ToEntryNo(2147483648m));
}
