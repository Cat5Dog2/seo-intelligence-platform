using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SeoIntelligence.Application.Configuration;

namespace SeoIntelligence.Infrastructure.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddSeoIntelligencePersistence(
        this IServiceCollection services,
        DatabaseOptions databaseOptions)
    {
        var errors = databaseOptions.Validate();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }

        ConfigureDbContext(services, databaseOptions.ConnectionString!);
        return services;
    }

    internal static DbContextOptionsBuilder UseSeoIntelligencePostgres(
        this DbContextOptionsBuilder builder,
        string connectionString)
    {
        builder.UseNpgsql(
            connectionString,
            options => options.MigrationsAssembly(typeof(SeoIntelligenceDbContext).Assembly.FullName));

        return builder;
    }

    private static void ConfigureDbContext(IServiceCollection services, string connectionString)
    {
        services.AddDbContext<SeoIntelligenceDbContext>(options =>
            options.UseSeoIntelligencePostgres(connectionString));

        services.AddDbContextFactory<SeoIntelligenceDbContext>(options =>
            options.UseSeoIntelligencePostgres(connectionString));
    }
}
