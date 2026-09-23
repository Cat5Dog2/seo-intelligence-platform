using System.Text.Json;
using SeoIntelligence.Application.RakkoKeyword;
using SeoIntelligence.Infrastructure.RakkoKeyword;

namespace ContractTests;

public sealed class RakkoRequestDefaultsContractTests
{
    public static IEnumerable<object[]> Requests()
    {
        yield return ["ContentSearchDto", RakkoKeywordDtoMapper.ToDto(new RakkoContentSearchRequest("SEO"))];
        yield return ["InfluxKeywordsKeywordDto", RakkoKeywordDtoMapper.ToDto(new RakkoInfluxKeywordsRequest([new("example.com")]))];
        yield return ["InfluxPagesDto", RakkoKeywordDtoMapper.ToDto(new RakkoInfluxPagesRequest([new("example.com")]))];
        yield return ["HeadlineDto", RakkoKeywordDtoMapper.ToDto(new RakkoHeadlineRequest("SEO"))];
        yield return ["CoOccurrenceDto", RakkoKeywordDtoMapper.ToDto(new RakkoCoOccurrenceRequest("SEO"))];
        yield return ["OtherKeywordsDto", RakkoKeywordDtoMapper.ToDto(new RakkoOtherKeywordsRequest("SEO"))];
        yield return ["SearchRankResultsDto", RakkoKeywordDtoMapper.ToDto(new RakkoSearchRankResultsRequest())];
    }

    [Theory]
    [Trait("Category", "Contract")]
    [MemberData(nameof(Requests))]
    public void DefaultWireRequestsRespectVendorEnumsAndBounds(string schemaName, object dto)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "SeoIntelligence.sln"))) root = root.Parent;
        Assert.NotNull(root);
        using var spec = JsonDocument.Parse(File.ReadAllText(Path.Combine(root.FullName, "docs", "rakko-keyword-api-docs.json")));
        var properties = spec.RootElement.GetProperty("components").GetProperty("schemas").GetProperty(schemaName).GetProperty("properties");
        var wire = JsonSerializer.SerializeToElement(dto, dto.GetType(), RakkoKeywordJson.SerializerOptions);
        foreach (var property in wire.EnumerateObject().Where(property => property.Value.ValueKind != JsonValueKind.Null))
        {
            var rule = properties.GetProperty(property.Name);
            if (rule.TryGetProperty("enum", out var values))
                Assert.Contains(property.Value.GetString(), values.EnumerateArray().Select(value => value.GetString()));
            if (rule.TryGetProperty("minimum", out var minimum))
                Assert.True(property.Value.GetDecimal() >= minimum.GetDecimal(), $"{schemaName}.{property.Name} is below minimum.");
            if (rule.TryGetProperty("maximum", out var maximum))
                Assert.True(property.Value.GetDecimal() <= maximum.GetDecimal(), $"{schemaName}.{property.Name} exceeds maximum.");
        }
    }
}
