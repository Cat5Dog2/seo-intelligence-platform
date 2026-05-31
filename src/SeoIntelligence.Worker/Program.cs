using SeoIntelligence.Application.Diagnostics;
using SeoIntelligence.Infrastructure;
using SeoIntelligence.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.Configure(options =>
{
    options.ActivityTrackingOptions =
        ActivityTrackingOptions.TraceId |
        ActivityTrackingOptions.SpanId |
        ActivityTrackingOptions.ParentId |
        ActivityTrackingOptions.Baggage;
});

builder.Services.AddSingleton(SeoIntelligenceDiagnostics.ActivitySource);
builder.Services.AddSingleton(SeoIntelligenceDiagnostics.Meter);
builder.Services.AddSeoIntelligenceInfrastructure(builder.Configuration, addHangfireServer: true);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
