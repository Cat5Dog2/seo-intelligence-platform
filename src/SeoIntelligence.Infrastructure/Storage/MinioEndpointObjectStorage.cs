using Microsoft.Extensions.Options;
using SeoIntelligence.Application.Configuration;
using SeoIntelligence.Application.Storage;

namespace SeoIntelligence.Infrastructure.Storage;

internal sealed class MinioEndpointObjectStorage(IOptions<StorageOptions> options) : IObjectStorage
{
    private static readonly HttpClient HttpClient = new();

    public Task<StoredObjectReference> PutAsync(StoragePutRequest request, CancellationToken cancellationToken = default)
        => throw CreateUnsupportedException();

    public Task<Stream> OpenReadAsync(StorageObjectKey key, CancellationToken cancellationToken = default)
        => throw CreateUnsupportedException();

    public Task<bool> ExistsAsync(StorageObjectKey key, CancellationToken cancellationToken = default)
        => throw CreateUnsupportedException();

    public Task DeleteAsync(StorageObjectKey key, CancellationToken cancellationToken = default)
        => throw CreateUnsupportedException();

    public async Task<StorageConnectivityResult> CheckConnectivityAsync(CancellationToken cancellationToken = default)
    {
        var errors = options.Value.Validate();
        if (errors.Count > 0)
        {
            return new StorageConnectivityResult(false, string.Join(" ", errors));
        }

        try
        {
            var endpoint = options.Value.Endpoint!.TrimEnd('/');
            using var response = await HttpClient.GetAsync($"{endpoint}/minio/health/ready", cancellationToken);

            return response.IsSuccessStatusCode
                ? new StorageConnectivityResult(true, "MinIO endpoint readiness succeeded.")
                : new StorageConnectivityResult(false, $"MinIO endpoint returned {(int)response.StatusCode}.");
        }
        catch (Exception exception)
        {
            return new StorageConnectivityResult(false, $"MinIO endpoint check failed: {exception.GetType().Name}.");
        }
    }

    private static NotSupportedException CreateUnsupportedException()
        => new("MinIO object operations require a signed S3 adapter. Configure Storage:Provider=Local for MVP file read/write operations.");
}
