namespace SeoIntelligence.Infrastructure.Persistence;

public static class SeoIntelligenceSeedData
{
    public static readonly Guid DefaultWorkspaceId =
        Guid.Parse("018f3f12-0001-7000-8000-000000000001");

    // ラッコキーワードAPI v1.4.1契約(2026-05-30)のスコープ。v1.12.0対応でarchived。
    public static readonly Guid ArchivedRakkoContractScopeId =
        Guid.Parse("018f3f12-0002-7000-8000-000000000001");

    // ラッコキーワードAPI v1.12.0契約(2026-07-26)の現行スコープ。
    public static readonly Guid DefaultRakkoContractScopeId =
        Guid.Parse("018f3f12-0002-7000-8000-000000000002");

    public static readonly DateTime SeedCreatedAt =
        new(2026, 5, 30, 0, 0, 0, DateTimeKind.Utc);

    public static readonly DateTime ContractUpdatedAt =
        new(2026, 7, 26, 0, 0, 0, DateTimeKind.Utc);

    public static readonly DateOnly ArchivedContractEffectiveFrom =
        new(2026, 5, 30);

    public static readonly DateOnly ArchivedContractEffectiveTo =
        new(2026, 7, 25);

    public static readonly DateOnly ContractEffectiveFrom =
        new(2026, 7, 26);

    public const string DefaultWorkspaceName = "Default Workspace";

    // ラッコキーワードAPI v1.12.0以降、location/languageはmetadata一覧の名前を正準値とする。
    public const string DefaultLocation = "Japan";
    public const string DefaultLanguage = "Japanese";

    public const string RakkoKeywordProvider = "rakko_keyword";
    public const string RakkoKeywordPlanName = "standard";
    public const string RakkoKeywordDataUsageScope = "internal";
    public const string ArchivedRakkoKeywordScopeKey = "rakko_keyword:standard:internal:2026-05-30";
    public const string RakkoKeywordScopeKey = "rakko_keyword:standard:internal:2026-07-26";
}
