namespace SeoIntelligence.Web.Services;

public static class UiText
{
    public static string Location(string? value, string? name = null) => value?.Trim().ToLowerInvariant() switch
    {
        "jp" or "japan" or "日本" => "日本",
        "us" or "united states" or "米国" => "米国",
        _ => string.IsNullOrWhiteSpace(name) ? value ?? "未選択" : name
    };

    public static string Language(string? value, string? name = null) => value?.Trim().ToLowerInvariant() switch
    {
        "ja" or "japanese" or "日本語" => "日本語",
        "en" or "english" or "英語" => "英語",
        _ => string.IsNullOrWhiteSpace(name) ? value ?? "未選択" : name
    };

    public static string DiscoverySource(string? value) => value switch
    {
        "suggest" => "サジェスト",
        "related" => "関連キーワード",
        "other" => "LSI・関連質問",
        "question" => "よくある質問",
        "ranking" => "同時ランクイン",
        _ => value ?? "—"
    };

    public static string ReviewStatus(string? value) => value switch
    {
        "pending" => "確認待ち",
        "reviewed" => "確認済み",
        "rejected" => "差し戻し",
        _ => value ?? "—"
    };

    public static string SearchIntent(string? value) => value switch
    {
        "informational" => "情報を知りたい",
        "commercial" => "比較・検討したい",
        "transactional" => "購入・申込みをしたい",
        "navigational" => "特定のサイトを探したい",
        _ => value ?? "未設定"
    };

    public static string JobType(string? value) => value switch
    {
        "RegisterSearchVolumeJob" => "検索ボリューム調査",
        "KeywordDiscoveryJob" => "キーワード探索",
        "GenerateBriefJob" => "記事ブリーフ生成",
        "DataExportJob" => "CSV出力",
        "AiAssistantJob" => "AI生成",
        "RankCheckJob" => "順位チェック",
        _ => value ?? "—"
    };

    public static string Status(string? value) => value switch
    {
        "active" => "有効",
        "archived" => "アーカイブ済み",
        "disabled" => "無効",
        "all" => "すべて",
        "draft" => "下書き",
        "pending" => "待機中",
        "queued" => "実行待ち",
        "running" => "実行中",
        "waiting_external" => "外部処理待ち",
        "retrying" => "再試行中",
        "succeeded" => "完了",
        "failed" => "失敗",
        "failed_retryable" => "失敗（再試行可能）",
        "failed_fatal" => "失敗（要確認）",
        "canceled" => "キャンセル済み",
        _ => value ?? "—"
    };
}
