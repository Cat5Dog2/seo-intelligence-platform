namespace SeoIntelligence.Api.Common;

internal static class OpenApiDocumentEndpoint
{
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
        AddMvpManagementPaths(paths);

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

    private static void AddMvpManagementPaths(IDictionary<string, object?> paths)
    {
        paths["/api/admin/workspace"] = PathItem(get: "Get workspace settings", put: "Update workspace settings");
        paths["/api/admin/api-credentials"] = PathItem(get: "List API credentials", post: "Create API credential", postSuccessCode: "201");
        paths["/api/admin/api-credentials/{credentialId}"] = PathItem(get: "Get API credential", put: "Update API credential", delete: "Disable API credential");
        paths["/api/admin/api-credentials/{credentialId}/enable"] = PathItem(post: "Enable API credential");
        paths["/api/admin/api-credentials/{credentialId}/rotate"] = PathItem(post: "Rotate API credential key reference");
        paths["/api/admin/notification-channels"] = PathItem(get: "List notification channels", post: "Create notification channel", postSuccessCode: "201");
        paths["/api/admin/notification-channels/{channelId}"] = PathItem(get: "Get notification channel", put: "Update notification channel", delete: "Disable notification channel");
        paths["/api/admin/notification-channels/{channelId}/enable"] = PathItem(post: "Enable notification channel");
        paths["/api/admin/notification-channels/{channelId}/test"] = PathItem(post: "Create notification test delivery");
        paths["/api/admin/notification-deliveries"] = PathItem(get: "List notification deliveries");
        paths["/api/admin/notification-deliveries/{deliveryId}"] = PathItem(get: "Get notification delivery");
        paths["/api/admin/notification-deliveries/{deliveryId}/retry"] = PathItem(post: "Retry notification delivery");
        paths["/api/admin/external-api-calls"] = PathItem(get: "List external API calls");
        paths["/api/admin/audit-logs"] = PathItem(get: "List audit logs");
        paths["/api/admin/audit-logs/{auditLogId}"] = PathItem(get: "Get audit log");
        paths["/api/projects"] = PathItem(get: "List projects", post: "Create project", postSuccessCode: "201");
        paths["/api/projects/{projectId}"] = PathItem(get: "Get project", put: "Update project", delete: "Archive project");
        paths["/api/projects/{projectId}/restore"] = PathItem(post: "Restore project");
        paths["/api/projects/{projectId}/sites"] = PathItem(get: "List sites", post: "Create site", postSuccessCode: "201");
        paths["/api/projects/{projectId}/sites/{siteId}"] = PathItem(get: "Get site", put: "Update site", delete: "Archive site");
        paths["/api/projects/{projectId}/sites/{siteId}/restore"] = PathItem(post: "Restore site");
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
}
