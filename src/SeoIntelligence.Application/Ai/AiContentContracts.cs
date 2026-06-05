using System.Text.Json;
using SeoIntelligence.Application.Common;

namespace SeoIntelligence.Application.Ai;

public interface IAiContentService
{
    Task<Result<AiContentResponse>> GenerateAsync(
        AiContentRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record AiContentRequest(
    string Prompt,
    JsonElement? ReferenceData = null,
    IReadOnlyList<string>? AllowedTools = null);

public sealed record AiContentResponse(
    string Response,
    IReadOnlyList<AiToolCall> ToolCalls,
    JsonElement TokenUsage);

public sealed record AiToolCall(
    string Name,
    JsonElement Arguments);
