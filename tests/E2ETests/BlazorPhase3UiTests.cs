using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using SeoIntelligence.Application.Services;
using SeoIntelligence.Contracts.Api;
using SeoIntelligence.Web.Services;

namespace E2ETests;

public sealed class BlazorPhase3UiTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    [Trait("Category", "E2E")]
    public async Task Phase3ClientUsesProjectScopedEndpointsForMainUiFlows()
    {
        var projectId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var connectorId = Guid.NewGuid();
        using var handler = new RecordingHandler((request, _) =>
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (request.Method == HttpMethod.Get && path.EndsWith("/rewrite/tasks", StringComparison.Ordinal))
            {
                return JsonResponse(
                    HttpStatusCode.OK,
                    ApiResponseEnvelope<IReadOnlyList<RewriteTaskDetails>>.Success(
                        "req-rewrite-list",
                        [
                            new RewriteTaskDetails(
                                taskId,
                                projectId,
                                "https://example.com/rewrite-target",
                                91.5m,
                                EmptyJson(),
                                "active",
                                "developer",
                                "Update headings.",
                                DateTime.UtcNow,
                                DateTime.UtcNow)
                        ]));
            }

            if (request.Method == HttpMethod.Put && path.EndsWith($"/rewrite/tasks/{taskId:D}", StringComparison.Ordinal))
            {
                return JsonResponse(
                    HttpStatusCode.OK,
                    ApiResponseEnvelope<RewriteTaskDetails>.Success(
                        "req-rewrite-update",
                        new RewriteTaskDetails(
                            taskId,
                            projectId,
                            "https://example.com/rewrite-target",
                            95m,
                            EmptyJson(),
                            "completed",
                            "developer",
                            "Done.",
                            DateTime.UtcNow,
                            DateTime.UtcNow)));
            }

            if (request.Method == HttpMethod.Post && path.EndsWith("/cannibalization/refresh", StringComparison.Ordinal))
            {
                return JsonResponse(
                    HttpStatusCode.Accepted,
                    ApiResponseEnvelope<JobReference>.Success(
                        "req-cannibalization-refresh",
                        new JobReference(Guid.NewGuid(), "queued")));
            }

            if (request.Method == HttpMethod.Post && path.EndsWith("/reports", StringComparison.Ordinal))
            {
                return JsonResponse(
                    HttpStatusCode.Accepted,
                    ApiResponseEnvelope<JobReference>.Success(
                        "req-report-create",
                        new JobReference(Guid.NewGuid(), "queued")));
            }

            if (request.Method == HttpMethod.Get && path.EndsWith($"/reports/{reportId:D}/download", StringComparison.Ordinal))
            {
                return JsonResponse(
                    HttpStatusCode.OK,
                    ApiResponseEnvelope<ReportDownload>.Success(
                        "req-report-download",
                        new ReportDownload(reportId, "https://download.example/report.pdf", DateTimeOffset.UtcNow.AddMinutes(10))));
            }

            if (request.Method == HttpMethod.Post && path.EndsWith($"/reports/{reportId:D}/share", StringComparison.Ordinal))
            {
                return JsonResponse(
                    HttpStatusCode.OK,
                    ApiResponseEnvelope<ReportShareDetails>.Success(
                        "req-report-share",
                        new ReportShareDetails(reportId, "https://share.example/token", DateTimeOffset.UtcNow.AddDays(7), null, "active")));
            }

            if (request.Method == HttpMethod.Delete && path.EndsWith($"/reports/{reportId:D}/share", StringComparison.Ordinal))
            {
                return JsonResponse(
                    HttpStatusCode.OK,
                    ApiResponseEnvelope<ReportShareDetails>.Success(
                        "req-report-revoke",
                        new ReportShareDetails(reportId, null, DateTimeOffset.UtcNow.AddDays(7), DateTimeOffset.UtcNow, "revoked")));
            }

            if (request.Method == HttpMethod.Post && path.EndsWith("/ai/chat", StringComparison.Ordinal))
            {
                return JsonResponse(
                    HttpStatusCode.Accepted,
                    ApiResponseEnvelope<AiChatResponse>.Success(
                        "req-ai-chat",
                        new AiChatResponse(
                            Guid.NewGuid(),
                            Guid.NewGuid(),
                            Guid.NewGuid(),
                            "Draft response.",
                            Array.Empty<JsonElement>(),
                            EmptyJson(),
                            EmptyJson(),
                            "redacted",
                            "pending")));
            }

            if (request.Method == HttpMethod.Get && path.EndsWith("/connectors", StringComparison.Ordinal))
            {
                return JsonResponse(
                    HttpStatusCode.OK,
                    ApiResponseEnvelope<IReadOnlyList<ConnectorSettingsDetails>>.Success(
                        "req-connectors-list",
                        [
                            new ConnectorSettingsDetails(
                                connectorId,
                                Guid.NewGuid(),
                                projectId,
                                "gsc",
                                "Search Console stub",
                                "gsc-auth-ref",
                                EmptyJson(),
                                "active",
                                DateTime.UtcNow,
                                DateTime.UtcNow,
                                null)
                        ]));
            }

            if (request.Method == HttpMethod.Post && path.EndsWith("/connectors", StringComparison.Ordinal))
            {
                return JsonResponse(
                    HttpStatusCode.Created,
                    ApiResponseEnvelope<ConnectorSettingsDetails>.Success(
                        "req-connectors-create",
                        new ConnectorSettingsDetails(
                            connectorId,
                            Guid.NewGuid(),
                            projectId,
                            "gsc",
                            "Search Console stub",
                            "gsc-auth-ref",
                            EmptyJson(),
                            "active",
                            DateTime.UtcNow,
                            DateTime.UtcNow,
                            null)));
            }

            if (request.Method == HttpMethod.Post && path.EndsWith($"/connectors/{connectorId:D}/test", StringComparison.Ordinal))
            {
                return JsonResponse(
                    HttpStatusCode.OK,
                    ApiResponseEnvelope<ConnectorRunDetails>.Success(
                        "req-connectors-test",
                        new ConnectorRunDetails(
                            Guid.NewGuid(),
                            connectorId,
                            Guid.NewGuid(),
                            projectId,
                            "test",
                            "succeeded",
                            EmptyJson(),
                            EmptyJson(),
                            null,
                            DateTime.UtcNow,
                            DateTime.UtcNow,
                            DateTime.UtcNow)));
            }

            if (request.Method == HttpMethod.Get && path.EndsWith($"/connectors/{connectorId:D}/runs", StringComparison.Ordinal))
            {
                return JsonResponse(
                    HttpStatusCode.OK,
                    ApiResponseEnvelope<IReadOnlyList<ConnectorRunDetails>>.Success(
                        "req-connectors-runs",
                        Array.Empty<ConnectorRunDetails>()));
            }

            return JsonResponse(
                HttpStatusCode.NotFound,
                ApiResponseEnvelope<object>.Failure(
                    "req-not-found",
                    [new ApiError("Test.NotFound", $"{request.Method} {path}")]));
        });
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://localhost")
        };
        var client = new SeoIntelligenceApiClient(
            httpClient,
            NullLogger<SeoIntelligenceApiClient>.Instance);

        var rewriteTasks = await client.SearchRewriteTasksAsync(projectId, q: "rewrite");
        var update = await client.UpdateRewriteTaskAsync(
            projectId,
            taskId,
            new RewriteTaskUpdateRequest("completed", 95m, "developer", "Done."));
        var cannibalizationRefresh = await client.RefreshCannibalizationAsync(projectId);
        var reportCreate = await client.CreateReportAsync(
            projectId,
            new ReportRequest("monthly_seo", "2026-06", "pdf", ["summary", "rewrite"], DateTimeOffset.UtcNow.AddDays(7)));
        var reportDownload = await client.CreateReportDownloadAsync(projectId, reportId);
        var reportShare = await client.ShareReportAsync(projectId, reportId, new ReportShareRequest(DateTimeOffset.UtcNow.AddDays(7)));
        var reportRevoke = await client.RevokeReportShareAsync(projectId, reportId);
        var aiChat = await client.ChatWithAiAsync(
            projectId,
            new AiChatRequest("リライト指示を作成", AllowedTools: ["rewrite.plan"], ReferenceScope: EmptyJson()));
        var connectors = await client.SearchConnectorsAsync(projectId, status: "active");
        var connectorCreate = await client.CreateConnectorAsync(
            projectId,
            new ConnectorSettingsRequest("gsc", "Search Console stub", "gsc-auth-ref", EmptyJson(), "active"));
        var connectorTest = await client.TestConnectorAsync(projectId, connectorId);
        var connectorRuns = await client.GetConnectorRunsAsync(projectId, connectorId);

        Assert.True(rewriteTasks.IsSuccess, rewriteTasks.ErrorSummary);
        Assert.True(update.IsSuccess, update.ErrorSummary);
        Assert.True(cannibalizationRefresh.IsSuccess, cannibalizationRefresh.ErrorSummary);
        Assert.True(reportCreate.IsSuccess, reportCreate.ErrorSummary);
        Assert.True(reportDownload.IsSuccess, reportDownload.ErrorSummary);
        Assert.True(reportShare.IsSuccess, reportShare.ErrorSummary);
        Assert.True(reportRevoke.IsSuccess, reportRevoke.ErrorSummary);
        Assert.True(aiChat.IsSuccess, aiChat.ErrorSummary);
        Assert.True(connectors.IsSuccess, connectors.ErrorSummary);
        Assert.True(connectorCreate.IsSuccess, connectorCreate.ErrorSummary);
        Assert.True(connectorTest.IsSuccess, connectorTest.ErrorSummary);
        Assert.True(connectorRuns.IsSuccess, connectorRuns.ErrorSummary);
        Assert.Contains($"GET /api/projects/{projectId:D}/rewrite/tasks?status=active&q=rewrite&sortBy=priorityScore&orderBy=desc&page=1&pageSize=100", handler.RequestLines);
        Assert.Contains($"PUT /api/projects/{projectId:D}/rewrite/tasks/{taskId:D}", handler.RequestLines);
        Assert.Contains($"POST /api/projects/{projectId:D}/cannibalization/refresh", handler.RequestLines);
        Assert.Contains($"POST /api/projects/{projectId:D}/reports", handler.RequestLines);
        Assert.Contains($"GET /api/projects/{projectId:D}/reports/{reportId:D}/download", handler.RequestLines);
        Assert.Contains($"POST /api/projects/{projectId:D}/reports/{reportId:D}/share", handler.RequestLines);
        Assert.Contains($"DELETE /api/projects/{projectId:D}/reports/{reportId:D}/share", handler.RequestLines);
        Assert.Contains($"POST /api/projects/{projectId:D}/ai/chat", handler.RequestLines);
        Assert.Contains($"GET /api/projects/{projectId:D}/connectors?status=active&sortBy=updatedAt&orderBy=desc&page=1&pageSize=100", handler.RequestLines);
        Assert.Contains($"POST /api/projects/{projectId:D}/connectors", handler.RequestLines);
        Assert.Contains($"POST /api/projects/{projectId:D}/connectors/{connectorId:D}/test", handler.RequestLines);
        Assert.Contains($"GET /api/projects/{projectId:D}/connectors/{connectorId:D}/runs?status=all&sortBy=createdAt&orderBy=desc&page=1&pageSize=50", handler.RequestLines);
        var connectorCreateBody = handler.RequestBodies[handler.RequestLines.IndexOf($"POST /api/projects/{projectId:D}/connectors")];
        Assert.Contains("gsc-auth-ref", connectorCreateBody, StringComparison.Ordinal);
        Assert.DoesNotContain("secretValue", connectorCreateBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("oauthToken", connectorCreateBody, StringComparison.OrdinalIgnoreCase);
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
        private readonly Func<HttpRequestMessage, string, HttpResponseMessage> responseFactory;

        public RecordingHandler(Func<HttpRequestMessage, string, HttpResponseMessage> responseFactory)
        {
            this.responseFactory = responseFactory;
        }

        public List<string> RequestBodies { get; } = [];

        public List<string> RequestLines { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            RequestBodies.Add(body);
            RequestLines.Add($"{request.Method} {request.RequestUri?.PathAndQuery}");
            return responseFactory(request, body);
        }
    }
}
