using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using SeoIntelligence.Application.Services;
using SeoIntelligence.Contracts.Api;
using SeoIntelligence.Web.Services;

namespace E2ETests;

public sealed class BlazorAdministrationUiTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    [Trait("Category", "UI")]
    public async Task ApiCredentialClientSendsSecretOnlyInCreateRequestAndNeverExposesItFromResponse()
    {
        const string inputSecret = "ui-test-secret-must-not-render";
        const string keyRef = "api-credential-rakko-keyword-generated";
        using var handler = new RecordingHandler((request, _) =>
            JsonResponse(
                HttpStatusCode.Created,
                ApiResponseEnvelope<ApiCredentialDetails>.Success(
                    "req-ui-secret",
                    new ApiCredentialDetails(
                        Guid.NewGuid(),
                        Guid.NewGuid(),
                        "rakko_keyword",
                        keyRef,
                        "active",
                        DateTime.UtcNow,
                        DateTime.UtcNow,
                        DisabledAt: null))));
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://localhost")
        };
        var client = new SeoIntelligenceApiClient(
            httpClient,
            NullLogger<SeoIntelligenceApiClient>.Instance);

        var result = await client.CreateApiCredentialAsync(
            new ApiCredentialCreateRequest("rakko_keyword", KeyRef: null, inputSecret));

        Assert.True(result.IsSuccess, result.ErrorSummary);
        Assert.Equal("/api/admin/api-credentials", handler.RequestUris.Single());
        Assert.Contains(inputSecret, handler.RequestBodies.Single(), StringComparison.Ordinal);
        Assert.Equal(keyRef, result.Data!.KeyRef);

        var renderedModelJson = JsonSerializer.Serialize(result.Data, JsonOptions);
        Assert.DoesNotContain(inputSecret, renderedModelJson, StringComparison.Ordinal);
        Assert.DoesNotContain("secretValue", renderedModelJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task AuditSearchSendsResourceCorrelationAndPeriodFiltersToManagementApi()
    {
        const string resourceType = "api_credential";
        var resourceId = Guid.NewGuid().ToString("D");
        const string correlationId = "corr-ui-audit";
        using var handler = new RecordingHandler((_, _) =>
            JsonResponse(
                HttpStatusCode.OK,
                ApiResponseEnvelope<IReadOnlyList<AuditLogDetails>>.Success(
                    "req-ui-audit",
                    [
                        new AuditLogDetails(
                            Guid.NewGuid(),
                            Guid.NewGuid(),
                            "developer",
                            "api_credential.created",
                            resourceType,
                            resourceId,
                            EmptyJson(),
                            correlationId,
                            IpAddress: null,
                            UserAgent: null,
                            DateTime.UtcNow)
                    ],
                    new ApiResponseMeta(Page: new PageMeta(1, 100, 1, 1)))));
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://localhost")
        };
        var client = new SeoIntelligenceApiClient(
            httpClient,
            NullLogger<SeoIntelligenceApiClient>.Instance);

        var result = await client.SearchAuditLogsAsync(new AuditLogSearchParameters(
            Actor: "developer",
            ResourceType: resourceType,
            ResourceId: resourceId,
            CorrelationId: correlationId,
            From: DateTimeOffset.Parse("2026-06-01T00:00:00Z"),
            To: DateTimeOffset.Parse("2026-06-02T00:00:00Z"),
            PageSize: 100));

        Assert.True(result.IsSuccess, result.ErrorSummary);
        var requestPath = Uri.UnescapeDataString(handler.RequestUris.Single());
        Assert.Contains("resourceType=api_credential", requestPath, StringComparison.Ordinal);
        Assert.Contains($"resourceId={resourceId}", requestPath, StringComparison.Ordinal);
        Assert.Contains("correlation_id=corr-ui-audit", requestPath, StringComparison.Ordinal);
        Assert.Contains("actor=developer", requestPath, StringComparison.Ordinal);
        Assert.Contains("from=2026-06-01T00:00:00.0000000+00:00", requestPath, StringComparison.Ordinal);
        Assert.Contains("to=2026-06-02T00:00:00.0000000+00:00", requestPath, StringComparison.Ordinal);
        Assert.Equal(resourceType, result.Data!.Single().ResourceType);
        Assert.Equal(resourceId, result.Data!.Single().ResourceId);
        Assert.Equal(correlationId, result.Data!.Single().CorrelationId);
    }

    private static JsonElement EmptyJson()
    {
        using var document = JsonDocument.Parse("{}");
        return document.RootElement.Clone();
    }

    private static HttpResponseMessage JsonResponse<T>(HttpStatusCode statusCode, ApiResponseEnvelope<T> envelope)
        => new(statusCode)
        {
            Content = JsonContent.Create(envelope, options: JsonOptions)
        };

    private sealed class RecordingHandler : HttpMessageHandler, IDisposable
    {
        private readonly Func<HttpRequestMessage, string, HttpResponseMessage> _responseFactory;

        public RecordingHandler(Func<HttpRequestMessage, string, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        public List<string> RequestBodies { get; } = [];

        public List<string> RequestUris { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            RequestBodies.Add(body);
            RequestUris.Add(request.RequestUri?.PathAndQuery ?? string.Empty);
            return _responseFactory(request, body);
        }
    }
}
