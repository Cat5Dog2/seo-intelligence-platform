namespace SeoIntelligence.Application.Storage;

public interface IObjectStorage
{
    Task<StoredObjectReference> PutAsync(
        StoragePutRequest request,
        CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(
        StorageObjectKey key,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(
        StorageObjectKey key,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        StorageObjectKey key,
        CancellationToken cancellationToken = default);

    Task<StorageConnectivityResult> CheckConnectivityAsync(CancellationToken cancellationToken = default);
}

public sealed record StoragePutRequest(
    StorageObjectKey Key,
    Stream Content,
    string ContentType);

public sealed record StoredObjectReference(
    string Uri,
    StorageObjectKey Key,
    string Provider,
    string ContentType,
    long Length);

public sealed record StorageConnectivityResult(bool IsHealthy, string Message);

public readonly record struct StorageObjectKey
{
    public StorageObjectKey(string value)
    {
        Value = Normalize(value);
    }

    public string Value { get; }

    public override string ToString() => Value;

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Storage object key is required.", nameof(value));
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out _))
        {
            throw new ArgumentException("Storage object key must be relative.", nameof(value));
        }

        var normalized = value.Replace('\\', '/').Trim('/');
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length == 0)
        {
            throw new ArgumentException("Storage object key is required.", nameof(value));
        }

        if (segments.Any(segment => segment is "." or ".."))
        {
            throw new ArgumentException("Storage object key cannot contain relative path segments.", nameof(value));
        }

        return string.Join('/', segments);
    }
}
