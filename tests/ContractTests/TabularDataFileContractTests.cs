using SeoIntelligence.Infrastructure.Services;

namespace ContractTests;

public sealed class TabularDataFileContractTests
{
    [Theory]
    [Trait("Category", "Contract")]
    [InlineData("=HYPERLINK(\"https://evil.example\")", "'=HYPERLINK(\"https://evil.example\")")]
    [InlineData("=1+2", "'=1+2")]
    [InlineData("+cmd|' /C calc'!A0", "'+cmd|' /C calc'!A0")]
    [InlineData("@SUM(A1)", "'@SUM(A1)")]
    [InlineData("-1+2+cmd", "'-1+2+cmd")]
    [InlineData("\tkeyword", "'\tkeyword")]
    [InlineData("\rkeyword", "'\rkeyword")]
    public void SanitizeFormulaTextPrefixesFormulaLeadingText(string value, string expected)
    {
        Assert.Equal(expected, TabularDataFile.SanitizeFormulaText(value));
    }

    [Theory]
    [Trait("Category", "Contract")]
    [InlineData("content marketing")]
    [InlineData("seo=tool")]
    [InlineData("-12.5")]
    [InlineData("+42")]
    [InlineData("-1,200.5")]
    [InlineData("1200")]
    [InlineData("")]
    public void SanitizeFormulaTextKeepsPlainTextAndNumbers(string value)
    {
        Assert.Equal(value, TabularDataFile.SanitizeFormulaText(value));
    }
}
