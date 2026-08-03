using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SeoIntelligence.Application.Configuration;
using SeoIntelligence.Application.Secrets;

namespace SeoIntelligence.Infrastructure.Secrets;

public static class SecretStoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Secret Store on its own, for hosts such as the Web app that need secrets but
    /// not the rest of the infrastructure stack.
    /// </summary>
    public static IServiceCollection AddSeoIntelligenceSecretStore(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = new SecretStoreOptions();
        configuration.GetSection(SecretStoreOptions.SectionName).Bind(options);

        var errors = options.Validate();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }

        services.AddOptions<SecretStoreOptions>()
            .Configure(configured =>
            {
                configured.Provider = options.Provider;
                configured.ConfigurationPrefix = options.ConfigurationPrefix;
            });

        services.TryAddSingleton(configuration);
        services.TryAddSingleton<ISecretStore, ConfigurationSecretStore>();

        return services;
    }
}
