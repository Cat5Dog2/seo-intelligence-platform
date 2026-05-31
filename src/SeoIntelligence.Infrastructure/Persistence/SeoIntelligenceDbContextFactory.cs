using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SeoIntelligence.Application.Configuration;

namespace SeoIntelligence.Infrastructure.Persistence;

public sealed class SeoIntelligenceDbContextFactory
    : IDesignTimeDbContextFactory<SeoIntelligenceDbContext>
{
    public SeoIntelligenceDbContext CreateDbContext(string[] args)
    {
        var connectionString = ResolveConnectionString(args);
        var optionsBuilder = new DbContextOptionsBuilder<SeoIntelligenceDbContext>();
        optionsBuilder.UseSeoIntelligencePostgres(connectionString);

        return new SeoIntelligenceDbContext(optionsBuilder.Options);
    }

    private static string ResolveConnectionString(IReadOnlyList<string> args)
    {
        var explicitConnectionString = ReadOption(args, "--connection");
        if (!string.IsNullOrWhiteSpace(explicitConnectionString))
        {
            return explicitConnectionString;
        }

        var environmentConnectionString = Environment.GetEnvironmentVariable(
            $"ConnectionStrings__{DatabaseOptions.DefaultConnectionName}");
        if (!string.IsNullOrWhiteSpace(environmentConnectionString))
        {
            return environmentConnectionString;
        }

        var repositoryRoot = FindRepositoryRoot();
        var developmentSettingsPath = Path.Combine(
            repositoryRoot,
            "src",
            "SeoIntelligence.Api",
            "appsettings.Development.json");
        var defaultSettingsPath = Path.Combine(
            repositoryRoot,
            "src",
            "SeoIntelligence.Api",
            "appsettings.json");

        return ReadConnectionString(developmentSettingsPath)
            ?? ReadConnectionString(defaultSettingsPath)
            ?? throw new InvalidOperationException("ConnectionStrings:Default is required for EF Core design-time operations.");
    }

    private static string? ReadOption(IReadOnlyList<string> args, string name)
    {
        for (var index = 0; index < args.Count - 1; index++)
        {
            if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[index + 1];
            }
        }

        return null;
    }

    private static string? ReadConnectionString(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        using var stream = File.OpenRead(path);
        using var document = JsonDocument.Parse(stream);
        if (!document.RootElement.TryGetProperty("ConnectionStrings", out var connectionStrings) ||
            !connectionStrings.TryGetProperty(DatabaseOptions.DefaultConnectionName, out var defaultConnectionString))
        {
            return null;
        }

        var value = defaultConnectionString.GetString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SeoIntelligence.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate SeoIntelligence.sln.");
    }
}
