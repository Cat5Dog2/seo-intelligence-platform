using SeoIntelligence.Domain.Normalization;

namespace UnitTests;

public sealed class InputNormalizationTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void KeywordNormalizerTrimsNormalizesDropsEmptyLinesAndDeduplicates()
    {
        string?[] keywords =
        [
            " SEO　対策 ",
            "",
            "ＳＥＯ 対策",
            "検索ボリューム",
            "   ",
            null,
            "検索ボリューム"
        ];

        var normalized = KeywordNormalizer.NormalizeMany(keywords);

        Assert.Equal(["SEO 対策", "検索ボリューム"], normalized);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void UrlNormalizerLowercasesHostRemovesPathFromDomainAndStripsUrlFragment()
    {
        var domain = UrlNormalizer.NormalizeDomain(" HTTPS://WWW.Example.COM:443/topics?x=1 ");
        var url = UrlNormalizer.NormalizeUrl("WWW.Example.COM:443/topics?x=1#section");

        Assert.Equal("www.example.com", domain);
        Assert.Equal("https://www.example.com/topics?x=1", url);
    }
}
