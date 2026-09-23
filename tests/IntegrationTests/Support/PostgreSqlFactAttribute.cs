namespace IntegrationTests.Support;

/// <summary>Runs against a caller-provided, disposable PostgreSQL test database.</summary>
public sealed class PostgreSqlFactAttribute : FactAttribute
{
    public PostgreSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TEST_POSTGRES_CONNECTION")))
        {
            Skip = "TEST_POSTGRES_CONNECTION must point to a disposable PostgreSQL test database.";
        }
    }
}
