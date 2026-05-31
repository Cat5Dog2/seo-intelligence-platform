namespace SeoIntelligence.Application.Secrets;

public interface ISecretStore
{
    Task<SecretValue?> GetAsync(SecretReference reference, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(SecretReference reference, CancellationToken cancellationToken = default);

    Task<SecretStoreConnectivityResult> CheckConnectivityAsync(CancellationToken cancellationToken = default);
}

public readonly record struct SecretReference
{
    public SecretReference(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Secret name is required.", nameof(name));
        }

        Name = name.Trim();
    }

    public string Name { get; }

    public override string ToString() => Name;
}

public sealed class SecretValue
{
    public SecretValue(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            throw new ArgumentException("Secret value is required.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => "****";
}

public sealed record SecretStoreConnectivityResult(bool IsHealthy, string Message);
