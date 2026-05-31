namespace SeoIntelligence.Api.Common;

internal sealed record ApiStatusResponse(
    string Service,
    string Diagnostics,
    string Status);
