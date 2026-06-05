namespace SeoIntelligence.Api.Endpoints;

internal static class Phase3FoundationEndpoints
{
    public static IEndpointRouteBuilder MapPhase3FoundationEndpoints(this IEndpointRouteBuilder app)
    {
        var project = app.MapGroup(Phase3EndpointRoutes.ProjectBase);

        _ = project.MapGroup(Phase3EndpointRoutes.Rewrite);
        _ = project.MapGroup(Phase3EndpointRoutes.Cannibalization);
        _ = project.MapGroup(Phase3EndpointRoutes.Reports);
        _ = project.MapGroup(Phase3EndpointRoutes.Exports);
        _ = project.MapGroup(Phase3EndpointRoutes.Imports);
        _ = project.MapGroup(Phase3EndpointRoutes.Connectors);
        _ = project.MapGroup(Phase3EndpointRoutes.Ai);
        _ = app.MapGroup(Phase3EndpointRoutes.ReportShares);

        return app;
    }
}

internal static class Phase3EndpointRoutes
{
    public const string ProjectBase = "/api/projects/{projectId:guid}";
    public const string Rewrite = "/rewrite";
    public const string Cannibalization = "/cannibalization";
    public const string Reports = "/reports";
    public const string Exports = "/exports";
    public const string Imports = "/imports";
    public const string Connectors = "/connectors";
    public const string Ai = "/ai";
    public const string ReportShares = "/api/report-shares";
}
