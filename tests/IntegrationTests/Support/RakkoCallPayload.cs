using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SeoIntelligence.Application.Storage;
using SeoIntelligence.Infrastructure.Persistence.Entities;

namespace IntegrationTests.Support;

internal static class RakkoCallPayload
{
    public static async Task<JsonDocument> ReadRequestAsync(IServiceProvider services, ExternalApiCallEntity call)
    {
        var storage = services.GetRequiredService<IObjectStorage>();
        await using var stream = await storage.OpenReadAsync(new StorageObjectKey(new Uri(call.RequestUri).AbsolutePath.TrimStart('/')));
        await using var decompressed = new GZipStream(stream, CompressionMode.Decompress);
        return await JsonDocument.ParseAsync(decompressed);
    }
}
