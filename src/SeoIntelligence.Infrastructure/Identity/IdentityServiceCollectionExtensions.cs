using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SeoIntelligence.Application.Accounts;
using SeoIntelligence.Application.Configuration;
using SeoIntelligence.Infrastructure.Accounts;
using SeoIntelligence.Infrastructure.Persistence;

namespace SeoIntelligence.Infrastructure.Identity;

public static class IdentityServiceCollectionExtensions
{
    /// <summary>
    /// Registers the persistence and seeding services that back the single administrator login.
    /// Cookie authentication and <c>AddIdentity</c> stay in the Web host because they are a
    /// presentation concern. This deliberately does not pull in Redis, Hangfire, Storage or the
    /// external API clients: the Web host reaches those capabilities through the API.
    /// </summary>
    public static IServiceCollection AddSeoIntelligenceIdentityStores(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = DatabaseConnectionStringResolver.Resolve(configuration);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "A database connection is required for the administrator login. "
                + "Configure ConnectionStrings:Default or the Database__* keys.");
        }

        services.AddSeoIntelligencePersistence(new DatabaseOptions { ConnectionString = connectionString });
        services.TryAddSingleton(TimeProvider.System);
        services.Configure<AdminSeedOptions>(configuration.GetSection(AdminSeedOptions.SectionName));
        services.AddScoped<IIdentityDataSeeder, IdentityDataSeeder>();
        services.AddScoped<IAccountPasswordService, AccountPasswordService>();

        return services;
    }
}
