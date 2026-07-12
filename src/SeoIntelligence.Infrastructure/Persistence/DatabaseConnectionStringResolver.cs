using Microsoft.Extensions.Configuration;
using Npgsql;
using SeoIntelligence.Application.Configuration;

namespace SeoIntelligence.Infrastructure.Persistence;

/// <summary>
/// Resolves the PostgreSQL connection string from configuration. The discrete
/// Database:* keys win when Database:Host is set (the application composes the
/// string via <see cref="NpgsqlConnectionStringBuilder"/>, so passwords never
/// need hand escaping in env files or Compose YAML); ConnectionStrings:Default
/// is the fallback used by host development via appsettings. Parts must win:
/// appsettings.Development.json ships a localhost ConnectionStrings:Default
/// that would otherwise shadow the container environment.
/// </summary>
internal static class DatabaseConnectionStringResolver
{
    internal static string? Resolve(IConfiguration configuration)
    {
        var parts = new DatabaseOptions();
        configuration.GetSection(DatabaseOptions.SectionName).Bind(parts);
        var composed = BuildFromParts(parts);
        if (!string.IsNullOrWhiteSpace(composed))
        {
            return composed;
        }

        return configuration.GetConnectionString(DatabaseOptions.DefaultConnectionName);
    }

    internal static string? ResolveFromEnvironment()
    {
        var composed = BuildFromParts(new DatabaseOptions
        {
            Host = ReadEnvironmentPart("Host"),
            Port = int.TryParse(ReadEnvironmentPart("Port"), out var port) ? port : null,
            Name = ReadEnvironmentPart("Name"),
            Username = ReadEnvironmentPart("Username"),
            Password = ReadEnvironmentPart("Password"),
            GssEncryptionMode = ReadEnvironmentPart("GssEncryptionMode")
        });
        if (!string.IsNullOrWhiteSpace(composed))
        {
            return composed;
        }

        return Environment.GetEnvironmentVariable(
            $"ConnectionStrings__{DatabaseOptions.DefaultConnectionName}");
    }

    internal static string? BuildFromParts(DatabaseOptions parts)
    {
        if (string.IsNullOrWhiteSpace(parts.Host))
        {
            return null;
        }

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = parts.Host
        };

        if (parts.Port.HasValue)
        {
            builder.Port = parts.Port.Value;
        }

        if (!string.IsNullOrWhiteSpace(parts.Name))
        {
            builder.Database = parts.Name;
        }

        if (!string.IsNullOrWhiteSpace(parts.Username))
        {
            builder.Username = parts.Username;
        }

        if (!string.IsNullOrEmpty(parts.Password))
        {
            builder.Password = parts.Password;
        }

        if (!string.IsNullOrWhiteSpace(parts.GssEncryptionMode))
        {
            builder["GSS Encryption Mode"] = parts.GssEncryptionMode;
        }

        return builder.ConnectionString;
    }

    private static string? ReadEnvironmentPart(string key)
        => Environment.GetEnvironmentVariable($"{DatabaseOptions.SectionName}__{key}");
}
