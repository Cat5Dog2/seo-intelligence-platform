namespace SeoIntelligence.Api.Common;

internal static class OpenApiDocumentEndpoint
{
    private static readonly OpenApiPathDefinition[] MvpPathDefinitions =
    [
        new("/api/admin/workspace", Get: "Get workspace settings", Put: "Update workspace settings"),
        new("/api/admin/api-credentials", Get: "List API credentials", Post: "Create API credential", PostSuccessCode: "201"),
        new("/api/admin/api-credentials/{credentialId}", Get: "Get API credential", Put: "Update API credential", Delete: "Disable API credential"),
        new("/api/admin/api-credentials/{credentialId}/enable", Post: "Enable API credential"),
        new("/api/admin/api-credentials/{credentialId}/rotate", Post: "Rotate API credential key reference"),
        new("/api/admin/notification-channels", Get: "List notification channels", Post: "Create notification channel", PostSuccessCode: "201"),
        new("/api/admin/notification-channels/{channelId}", Get: "Get notification channel", Put: "Update notification channel", Delete: "Disable notification channel"),
        new("/api/admin/notification-channels/{channelId}/enable", Post: "Enable notification channel"),
        new("/api/admin/notification-channels/{channelId}/test", Post: "Create notification test delivery"),
        new("/api/admin/notification-deliveries", Get: "List notification deliveries"),
        new("/api/admin/notification-deliveries/{deliveryId}", Get: "Get notification delivery"),
        new("/api/admin/notification-deliveries/{deliveryId}/retry", Post: "Retry notification delivery"),
        new("/api/admin/master-data/sync", Post: "Register master data sync job", PostSuccessCode: "202"),
        new("/api/master-data/locations", Get: "List active master locations"),
        new("/api/master-data/languages", Get: "List active master languages"),
        new("/api/admin/external-api-calls", Get: "List external API calls"),
        new("/api/admin/audit-logs", Get: "List audit logs"),
        new("/api/admin/audit-logs/{auditLogId}", Get: "Get audit log"),
        new("/api/jobs", Get: "List jobs"),
        new("/api/jobs/{jobId}", Get: "Get job"),
        new("/api/jobs/{jobId}/cancel", Post: "Cancel queued or waiting external job"),
        new("/api/jobs/{jobId}/retry", Post: "Retry failed retryable job"),
        new("/api/projects", Get: "List projects", Post: "Create project", PostSuccessCode: "201"),
        new("/api/projects/{projectId}", Get: "Get project", Put: "Update project", Delete: "Archive project"),
        new("/api/projects/{projectId}/restore", Post: "Restore project"),
        new("/api/projects/{projectId}/dashboard", Get: "Get Phase 1 dashboard metrics"),
        new("/api/projects/{projectId}/sites", Get: "List sites", Post: "Create site", PostSuccessCode: "201"),
        new("/api/projects/{projectId}/sites/{siteId}", Get: "Get site", Put: "Update site", Delete: "Archive site"),
        new("/api/projects/{projectId}/sites/{siteId}/restore", Post: "Restore site"),
        new("/api/projects/{projectId}/keyword-discovery/suggest", UseKeywordDiscoveryResponses: true),
        new("/api/projects/{projectId}/search-volume/jobs", Post: "Register search volume job", PostSuccessCode: "202"),
        new("/api/projects/{projectId}/search-volume/jobs/{jobId}", Get: "Get search volume job"),
        new("/api/projects/{projectId}/search-volume/jobs/{jobId}/results", Get: "List search volume results"),
        new("/api/projects/{projectId}/competitors", Get: "List competitor analysis results"),
        new("/api/projects/{projectId}/competitors/analyze", Post: "Register competitor refresh job", PostSuccessCode: "202"),
        new("/api/projects/{projectId}/influx-keywords", Get: "List influx keyword results"),
        new("/api/projects/{projectId}/influx-pages", Get: "List influx page results"),
        new("/api/projects/{projectId}/clusters", Get: "List topic clusters"),
        new("/api/projects/{projectId}/clusters/{clusterId}", Get: "Get topic cluster details"),
        new("/api/projects/{projectId}/clusters/generate", Post: "Register topic cluster generation job", PostSuccessCode: "202"),
        new("/api/projects/{projectId}/content-analyses", Get: "List content analysis results"),
        new("/api/projects/{projectId}/content/analyze", Post: "Register content analysis job", PostSuccessCode: "202"),
        new("/api/projects/{projectId}/briefs", Get: "List article briefs"),
        new("/api/projects/{projectId}/briefs/generate", Post: "Register article brief generation job", PostSuccessCode: "202"),
        new("/api/projects/{projectId}/briefs/{briefId}", Get: "Get article brief", Put: "Update article brief"),
        new("/api/projects/{projectId}/briefs/{briefId}/versions", Get: "List article brief versions"),
        new("/api/projects/{projectId}/briefs/{briefId}/export", Post: "Register article brief export job", PostSuccessCode: "202"),
        new("/api/projects/{projectId}/exports/csv", Post: "Register Phase 1 CSV export job", PostSuccessCode: "202"),
        new("/api/projects/{projectId}/exports/{exportId}", Get: "Get export state and file metadata"),
        new("/api/projects/{projectId}/exports/{exportId}/download", Get: "Issue a short-lived export download URL")
    ];

    public static IResult GetV1(HttpContext context)
    {
        var serverUrl = $"{context.Request.Scheme}://{context.Request.Host}";
        var paths = new Dictionary<string, object?>
        {
            ["/api"] = new
            {
                get = new
                {
                    summary = "API service status",
                    responses = new Dictionary<string, object?>
                    {
                        ["200"] = JsonResponse("API status envelope."),
                        ["400"] = JsonResponse("Validation error envelope.")
                    }
                }
            },
            ["/healthz"] = new
            {
                get = new
                {
                    summary = "Liveness health check",
                    responses = new Dictionary<string, object?>
                    {
                        ["200"] = JsonResponse("Liveness health check envelope.")
                    }
                }
            },
            ["/readyz"] = new
            {
                get = new
                {
                    summary = "Readiness health check",
                    responses = new Dictionary<string, object?>
                    {
                        ["200"] = JsonResponse("Readiness health check envelope."),
                        ["503"] = JsonResponse("Readiness failure envelope.")
                    }
                }
            }
        };
        AddMvpPaths(paths);

        var document = new
        {
            openapi = "3.0.3",
            info = new
            {
                title = "SeoIntelligence API",
                version = "v1"
            },
            servers = new[]
            {
                new { url = serverUrl }
            },
            paths,
            components = new
            {
                schemas = new Dictionary<string, object?>
                {
                    ["ApiResponseEnvelope"] = new
                    {
                        type = "object",
                        required = new[] { "requestId", "result", "errors", "meta" },
                        properties = new Dictionary<string, object?>
                        {
                            ["requestId"] = new { type = "string" },
                            ["result"] = new { type = "boolean" },
                            ["data"] = new { nullable = true },
                            ["errors"] = new
                            {
                                type = "array",
                                items = new { @ref = "#/components/schemas/ApiError" }
                            },
                            ["meta"] = new { @ref = "#/components/schemas/ApiResponseMeta" }
                        }
                    },
                    ["ApiError"] = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object?>
                        {
                            ["code"] = new { type = "string" },
                            ["message"] = new { type = "string" },
                            ["target"] = new { type = "string", nullable = true }
                        }
                    },
                    ["ApiResponseMeta"] = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object?>
                        {
                            ["jobId"] = new { type = "string", format = "uuid", nullable = true },
                            ["externalRequestId"] = new { type = "string", nullable = true },
                            ["consumedCredit"] = new { type = "number", format = "decimal" },
                            ["page"] = new { nullable = true }
                        }
                    }
                }
            }
        };

        return Results.Json(document);
    }

    private static object JsonResponse(string description)
        => new
        {
            description,
            content = new Dictionary<string, object?>
            {
                ["application/json"] = new
                {
                    schema = new { @ref = "#/components/schemas/ApiResponseEnvelope" }
                }
            }
        };

    private static void AddMvpPaths(IDictionary<string, object?> paths)
    {
        foreach (var definition in MvpPathDefinitions)
        {
            paths[definition.Template] = definition.ToPathItem();
        }
    }

    private static object PathItem(
        string? get = null,
        string? post = null,
        string? put = null,
        string? delete = null,
        string postSuccessCode = "200")
    {
        var operations = new Dictionary<string, object?>();

        if (get is not null)
        {
            operations["get"] = Operation(get, "200");
        }

        if (post is not null)
        {
            operations["post"] = Operation(post, postSuccessCode);
        }

        if (put is not null)
        {
            operations["put"] = Operation(put, "200");
        }

        if (delete is not null)
        {
            operations["delete"] = Operation(delete, "200");
        }

        return operations;
    }

    private static object KeywordDiscoveryPathItem()
        => new
        {
            post = new
            {
                summary = "Run keyword discovery",
                responses = new Dictionary<string, object?>
                {
                    ["200"] = JsonResponse("Synchronous keyword discovery result envelope."),
                    ["202"] = JsonResponse("Queued keyword discovery job envelope."),
                    ["400"] = JsonResponse("Validation error envelope."),
                    ["404"] = JsonResponse("Not found error envelope."),
                    ["409"] = JsonResponse("Conflict error envelope."),
                    ["503"] = JsonResponse("Retryable external API error envelope.")
                }
            }
        };

    private static object Operation(string summary, string successCode)
        => new
        {
            summary,
            responses = new Dictionary<string, object?>
            {
                [successCode] = JsonResponse("Success envelope."),
                ["400"] = JsonResponse("Validation error envelope."),
                ["404"] = JsonResponse("Not found error envelope."),
                ["409"] = JsonResponse("Conflict error envelope.")
            }
        };

    private sealed record OpenApiPathDefinition(
        string Template,
        string? Get = null,
        string? Post = null,
        string? Put = null,
        string? Delete = null,
        string PostSuccessCode = "200",
        bool UseKeywordDiscoveryResponses = false)
    {
        public object ToPathItem()
            => UseKeywordDiscoveryResponses
                ? KeywordDiscoveryPathItem()
                : PathItem(
                    get: Get,
                    post: Post,
                    put: Put,
                    delete: Delete,
                    postSuccessCode: PostSuccessCode);
    }
}
