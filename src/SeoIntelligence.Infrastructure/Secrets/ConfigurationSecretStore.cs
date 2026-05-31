using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using SeoIntelligence.Application.Configuration;
using SeoIntelligence.Application.Secrets;

namespace SeoIntelligence.Infrastructure.Secrets;

internal sealed class ConfigurationSecretStore(
    IConfiguration configuration,
    IOptions<SecretStoreOptions> options)
    : ISecretStore
{
    public Task<SecretValue?> GetAsync(SecretReference reference, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var value = configuration[BuildConfigurationKey(reference)];
        return Task.FromResult(string.IsNullOrEmpty(value) ? null : new SecretValue(value));
    }

    public Task<bool> ExistsAsync(SecretReference reference, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(!string.IsNullOrEmpty(configuration[BuildConfigurationKey(reference)]));
    }

    public Task<SecretStoreConnectivityResult> CheckConnectivityAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var errors = options.Value.Validate();
        return Task.FromResult(errors.Count == 0
            ? new SecretStoreConnectivityResult(true, "Configuration Secret Store is available.")
            : new SecretStoreConnectivityResult(false, string.Join(" ", errors)));
    }

    private string BuildConfigurationKey(SecretReference reference)
        => $"{options.Value.ConfigurationPrefix}:{reference.Name}";
}
