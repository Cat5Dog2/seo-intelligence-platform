using Microsoft.EntityFrameworkCore;
using SeoIntelligence.Infrastructure.Persistence;
using SeoIntelligence.Infrastructure.Services;

namespace ContractTests;

/// <summary>
/// Pins that the location/language canonicalization queries can be translated to SQL.
/// <para>
/// These queries project onto <see cref="MasterNameEntry"/> and then filter on the projection. A
/// positional-constructor projection made that untranslatable, so every search volume job
/// registration failed with HTTP 500 - and no existing test caught it, because the API integration
/// tests run without a relational provider. Calling <c>ToQueryString</c> compiles the query without
/// opening a connection, so the translation is verified here without a database.
/// </para>
/// <para>
/// The "master data not synchronized yet" branch calls <c>AnyAsync</c> on the same projection.
/// <c>ToQueryString</c> cannot cover a terminal operator, so that branch is not pinned here; it
/// needs a test against a real relational provider.
/// </para>
/// </summary>
public sealed class MasterNameQueryContractTests
{
    [Fact]
    [Trait("Category", "Contract")]
    public void ActiveLocationNameLookupTranslatesToSql()
    {
        using var dbContext = CreateContext();

        var sql = MasterNameQuery
            .ActiveNamesMatching(MasterNameQuery.ForLocations(dbContext), "JAPAN")
            .ToQueryString();

        Assert.Contains("locations", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("UPPER", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void LegacyLocationCodeLookupTranslatesToSql()
    {
        using var dbContext = CreateContext();

        var sql = MasterNameQuery
            .NamesForLegacyCode(MasterNameQuery.ForLocations(dbContext), "JP")
            .ToQueryString();

        Assert.Contains("locations", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void ActiveLanguageNameLookupTranslatesToSql()
    {
        using var dbContext = CreateContext();

        var sql = MasterNameQuery
            .ActiveNamesMatching(MasterNameQuery.ForLanguages(dbContext), "JAPANESE")
            .ToQueryString();

        Assert.Contains("languages", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("UPPER", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void LegacyLanguageCodeLookupTranslatesToSql()
    {
        using var dbContext = CreateContext();

        var sql = MasterNameQuery
            .NamesForLegacyCode(MasterNameQuery.ForLanguages(dbContext), "JA")
            .ToQueryString();

        Assert.Contains("languages", sql, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The connection string is never used: <c>ToQueryString</c> only compiles the query.
    /// </summary>
    private static SeoIntelligenceDbContext CreateContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<SeoIntelligenceDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=seo;Username=seo;Password=unused");
        return new SeoIntelligenceDbContext(optionsBuilder.Options);
    }
}
