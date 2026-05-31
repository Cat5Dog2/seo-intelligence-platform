using Microsoft.Extensions.Options;
using SeoIntelligence.Application.Configuration;
using SeoIntelligence.Application.Storage;

namespace SeoIntelligence.Infrastructure.Storage;

internal sealed class LocalObjectStorage(IOptions<StorageOptions> options) : IObjectStorage
{
    private readonly string basePath = ResolveBasePath(options.Value);

    public async Task<StoredObjectReference> PutAsync(
        StoragePutRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request.Content);

        var fullPath = ResolveObjectPath(request.Key);
        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("Storage object directory could not be resolved.");

        Directory.CreateDirectory(directory);

        await using (var output = File.Create(fullPath))
        {
            await request.Content.CopyToAsync(output, cancellationToken);
        }

        var fileInfo = new FileInfo(fullPath);
        var contentType = string.IsNullOrWhiteSpace(request.ContentType)
            ? "application/octet-stream"
            : request.ContentType;

        return new StoredObjectReference(
            $"storage://local/{request.Key.Value}",
            request.Key,
            StorageOptions.LocalProvider,
            contentType,
            fileInfo.Length);
    }

    public Task<Stream> OpenReadAsync(StorageObjectKey key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var fullPath = ResolveObjectPath(key);
        Stream stream = File.OpenRead(fullPath);
        return Task.FromResult(stream);
    }

    public Task<bool> ExistsAsync(StorageObjectKey key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(File.Exists(ResolveObjectPath(key)));
    }

    public Task DeleteAsync(StorageObjectKey key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var fullPath = ResolveObjectPath(key);

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    public async Task<StorageConnectivityResult> CheckConnectivityAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            Directory.CreateDirectory(basePath);

            var healthDirectory = Path.Combine(basePath, ".health");
            Directory.CreateDirectory(healthDirectory);

            var healthFile = Path.Combine(healthDirectory, $"{Guid.NewGuid():N}.tmp");
            await File.WriteAllTextAsync(healthFile, "ok", cancellationToken);
            var content = await File.ReadAllTextAsync(healthFile, cancellationToken);
            File.Delete(healthFile);

            return string.Equals(content, "ok", StringComparison.Ordinal)
                ? new StorageConnectivityResult(true, "Local storage read/write succeeded.")
                : new StorageConnectivityResult(false, "Local storage health content mismatch.");
        }
        catch (Exception exception)
        {
            return new StorageConnectivityResult(false, $"Local storage check failed: {exception.GetType().Name}.");
        }
    }

    private static string ResolveBasePath(StorageOptions options)
    {
        var errors = options.Validate();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }

        return Path.GetFullPath(options.BasePath!);
    }

    private string ResolveObjectPath(StorageObjectKey key)
    {
        var relativePath = key.Value.Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(basePath, relativePath));

        if (!IsSubPathOfBasePath(fullPath))
        {
            throw new InvalidOperationException("Storage object path resolved outside the configured base path.");
        }

        return fullPath;
    }

    private bool IsSubPathOfBasePath(string fullPath)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var normalizedBasePath = basePath.EndsWith(Path.DirectorySeparatorChar)
            ? basePath
            : basePath + Path.DirectorySeparatorChar;

        return fullPath.StartsWith(normalizedBasePath, comparison);
    }
}
