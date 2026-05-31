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
}
