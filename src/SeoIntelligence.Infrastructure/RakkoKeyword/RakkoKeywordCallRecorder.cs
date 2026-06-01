using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SeoIntelligence.Application.Configuration;
using SeoIntelligence.Application.RakkoKeyword;
using SeoIntelligence.Application.Storage;
using SeoIntelligence.Infrastructure.Persistence;
using SeoIntelligence.Infrastructure.Persistence.Entities;

namespace SeoIntelligence.Infrastructure.RakkoKeyword;

internal interface IRakkoKeywordCallRecorder
{
    Task<RakkoKeywordExternalCallRecord> RecordAsync(
        RakkoKeywordCallRecordRequest request,
        CancellationToken cancellationToken = default);
}

internal sealed record RakkoKeywordCallRecordRequest(
    RakkoKeywordClientContext Context,
    string Endpoint,
    string Method,
    object? RequestBody,
    byte[]? ResponseBody,
    int StatusCode,
    decimal ConsumedCredit,
    int DurationMs,
    bool CacheHit,
    string? ErrorCode);

internal sealed class RakkoKeywordCallRecorder(
    IObjectStorage storage,
    IRakkoKeywordExternalApiCallStore externalApiCallStore,
    IOptions<RakkoKeywordOptions> options)
    : IRakkoKeywordCallRecorder
{
    public async Task<RakkoKeywordExternalCallRecord> RecordAsync(
        RakkoKeywordCallRecordRequest request,
        CancellationToken cancellationToken = default)
    {
        var requestEnvelope = new RawRequestEnvelope(request.Method, request.Endpoint, request.RequestBody);
        var requestBytes = JsonSerializer.SerializeToUtf8Bytes(requestEnvelope, RakkoKeywordJson.SerializerOptions);
        var compressedRequest = Compress(requestBytes);
        var requestHash = ComputeSha256Hex(compressedRequest);
        var requestReference = await storage.PutAsync(
            new StoragePutRequest(
                BuildStorageKey(request.Context, request.Endpoint, "request"),
                new MemoryStream(compressedRequest),
                "application/json+gzip"),
            cancellationToken);

        string? responseHash = null;
        string? responseUri = null;
        if (request.ResponseBody is not null)
        {
            var compressedResponse = Compress(request.ResponseBody);
            responseHash = ComputeSha256Hex(compressedResponse);
            var responseReference = await storage.PutAsync(
                new StoragePutRequest(
                    BuildStorageKey(request.Context, request.Endpoint, "response"),
                    new MemoryStream(compressedResponse),
                    "application/json+gzip"),
                cancellationToken);
            responseUri = responseReference.Uri;
        }

        var callId = await externalApiCallStore.StoreAsync(
            new ExternalApiCallWriteRequest(
                request.Context,
                request.Endpoint,
                requestHash,
                requestReference.Uri,
                responseHash,
                responseUri,
                request.CacheHit,
                request.StatusCode,
                request.ConsumedCredit,
                request.DurationMs,
                request.ErrorCode,
                DateTime.UtcNow.AddMonths(options.Value.RawDataRetentionMonths)),
            cancellationToken);

        return new RakkoKeywordExternalCallRecord(
            callId,
            requestHash,
            requestReference.Uri,
            responseHash,
            responseUri,
            request.CacheHit,
            request.ErrorCode);
    }

    private static StorageObjectKey BuildStorageKey(
        RakkoKeywordClientContext context,
        string endpoint,
        string kind)
    {
        var safeEndpoint = endpoint.Trim('/').Replace('/', '-').Replace("{", string.Empty).Replace("}", string.Empty);
        var timestamp = DateTime.UtcNow;
        var callId = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
        var workspaceId = context.WorkspaceId == Guid.Empty ? "workspace-unknown" : context.WorkspaceId.ToString("N");

        return new StorageObjectKey(
            $"raw/rakko-keyword/{timestamp:yyyy/MM/dd}/{workspaceId}/{safeEndpoint}-{kind}-{callId}-{Guid.NewGuid():N}.json.gz");
    }

    private static byte[] Compress(byte[] content)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            gzip.Write(content, 0, content.Length);
        }

        return output.ToArray();
    }

    private static string ComputeSha256Hex(byte[] content)
        => Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

    private sealed record RawRequestEnvelope(string Method, string Endpoint, object? Body);
}

internal interface IRakkoKeywordExternalApiCallStore
{
    Task<Guid?> StoreAsync(
        ExternalApiCallWriteRequest request,
        CancellationToken cancellationToken = default);
}

internal sealed record ExternalApiCallWriteRequest(
    RakkoKeywordClientContext Context,
    string Endpoint,
    string RequestHash,
    string RequestUri,
    string? ResponseHash,
    string? ResponseUri,
    bool CacheHit,
    int StatusCode,
    decimal ConsumedCredit,
    int DurationMs,
    string? ErrorCode,
    DateTime RetainedUntil);

internal sealed class OptionalEfExternalApiCallStore(IServiceProvider serviceProvider) : IRakkoKeywordExternalApiCallStore
{
    public async Task<Guid?> StoreAsync(
        ExternalApiCallWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        var dbContext = serviceProvider.GetService<SeoIntelligenceDbContext>();
        if (dbContext is null)
        {
            return null;
        }

        var now = DateTime.UtcNow;
        var entity = new ExternalApiCallEntity
        {
            Id = Guid.NewGuid(),
            WorkspaceId = request.Context.WorkspaceId,
            ProjectId = request.Context.ProjectId,
            JobId = request.Context.JobId,
            ApiCredentialId = request.Context.ApiCredentialId,
            ApiContractScopeId = request.Context.ApiContractScopeId,
            Provider = RakkoKeywordOptions.ProviderName,
            Endpoint = request.Endpoint,
            RequestHash = request.RequestHash,
            RequestUri = request.RequestUri,
            ResponseHash = request.ResponseHash,
            ResponseUri = request.ResponseUri,
            ContractScopeKey = ResolveContractScopeKey(request.Context),
            CacheHit = request.CacheHit,
            StatusCode = request.StatusCode,
            ConsumedCredit = request.ConsumedCredit,
            DurationMs = request.DurationMs,
            ErrorCode = request.ErrorCode,
            CorrelationId = request.Context.CorrelationId,
            Actor = string.IsNullOrWhiteSpace(request.Context.Actor) ? "developer" : request.Context.Actor,
            RetainedUntil = request.RetainedUntil,
            CreatedAt = now
        };

        dbContext.ExternalApiCalls.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    private static string ResolveContractScopeKey(RakkoKeywordClientContext context)
        => string.IsNullOrWhiteSpace(context.ContractScopeKey)
            ? SeoIntelligenceSeedData.RakkoKeywordScopeKey
            : context.ContractScopeKey;
}
