using Npgsql;
using SeoIntelligence.Application.Configuration;
using SeoIntelligence.Infrastructure.Persistence;

namespace ContractTests;

public sealed class DatabaseConnectionStringResolverTests
{
    [Fact]
    [Trait("Category", "Contract")]
    public void BuildFromPartsReturnsNullWithoutHost()
    {
        Assert.Null(DatabaseConnectionStringResolver.BuildFromParts(new DatabaseOptions()));
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void BuildFromPartsComposesAllParts()
    {
        var connectionString = DatabaseConnectionStringResolver.BuildFromParts(new DatabaseOptions
        {
            Host = "postgres",
            Port = 5432,
            Name = "seo",
            Username = "seo",
            Password = "seo_dev_password",
            GssEncryptionMode = "Disable"
        });

        Assert.NotNull(connectionString);
        var parsed = new NpgsqlConnectionStringBuilder(connectionString);
        Assert.Equal("postgres", parsed.Host);
        Assert.Equal(5432, parsed.Port);
        Assert.Equal("seo", parsed.Database);
        Assert.Equal("seo", parsed.Username);
        Assert.Equal("seo_dev_password", parsed.Password);
        Assert.Equal("Disable", parsed["GSS Encryption Mode"]?.ToString());
    }

    [Theory]
    [Trait("Category", "Contract")]
    [InlineData("pass;word")]
    [InlineData("pass\"word")]
    [InlineData("pass'word")]
    [InlineData("pass word")]
    [InlineData(";=\"'\\$p@ss")]
    [InlineData(" leading-and-trailing ")]
    public void BuildFromPartsRoundTripsSpecialCharacterPasswords(string password)
    {
        var connectionString = DatabaseConnectionStringResolver.BuildFromParts(new DatabaseOptions
        {
            Host = "postgres",
            Password = password
        });

        Assert.NotNull(connectionString);
        var parsed = new NpgsqlConnectionStringBuilder(connectionString);
        Assert.Equal(password, parsed.Password);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void BuildFromPartsOmitsUnsetOptionalParts()
    {
        var connectionString = DatabaseConnectionStringResolver.BuildFromParts(new DatabaseOptions
        {
            Host = "postgres"
        });

        Assert.NotNull(connectionString);
        Assert.DoesNotContain("GSS Encryption Mode", connectionString, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password", connectionString, StringComparison.OrdinalIgnoreCase);
    }
}
