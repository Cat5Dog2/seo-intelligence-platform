using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace SeoIntelligence.Application.Diagnostics;

public static class SeoIntelligenceDiagnostics
{
    public const string ServiceName = "SeoIntelligence";
    public const string ActivitySourceName = "SeoIntelligence";
    public const string MeterName = "SeoIntelligence";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    public static readonly Meter Meter = new(MeterName);
}
