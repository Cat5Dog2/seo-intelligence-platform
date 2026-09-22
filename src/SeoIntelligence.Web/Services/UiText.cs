namespace SeoIntelligence.Web.Services;

public static class UiText
{
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
