namespace SeoIntelligence.Api.Common;

internal static class CorrelationIdHttpContextExtensions
{
    public const string HeaderName = "X-Correlation-Id";

    private const string ItemKey = "SeoIntelligence.CorrelationId";

    public static string GetCorrelationId(this HttpContext context)
        => context.Items.TryGetValue(ItemKey, out var value) && value is string correlationId
            ? correlationId
            : context.TraceIdentifier;

    public static void SetCorrelationId(this HttpContext context, string correlationId)
    {
        context.Items[ItemKey] = correlationId;
        context.TraceIdentifier = correlationId;
    }
}
