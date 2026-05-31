namespace SeoIntelligence.Infrastructure.Persistence;

public static class SeoIntelligenceSeedData
{
    public static readonly Guid DefaultWorkspaceId =
        Guid.Parse("018f3f12-0001-7000-8000-000000000001");

    public static readonly Guid DefaultRakkoContractScopeId =
        Guid.Parse("018f3f12-0002-7000-8000-000000000001");

    public static readonly DateTime SeedCreatedAt =
        new(2026, 5, 30, 0, 0, 0, DateTimeKind.Utc);

    public static readonly DateOnly ContractEffectiveFrom =
        new(2026, 5, 30);

    public const string DefaultWorkspaceName = "Default Workspace";
    public const string DefaultLocation = "JP";
    public const string DefaultLanguage = "ja";
    public const string RakkoKeywordProvider = "rakko_keyword";
    public const string RakkoKeywordPlanName = "standard";
    public const string RakkoKeywordDataUsageScope = "internal";
    public const string RakkoKeywordScopeKey = "rakko_keyword:standard:internal:2026-05-30";
}
