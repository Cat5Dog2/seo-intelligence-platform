# 運用Runbook

**ラッコキーワードAPIを中核にしたSEOインテリジェンス基盤**

_SEO Intelligence Platform / SEOインテリジェンス基盤_

| 項目 | 内容 |
| --- | --- |
| 文書ID | OPS-RKSEO-001 |
| 作成日 | 2026-05-30 |
| 対象 | API/Worker/DB/Redis/Storage/Secret/外部API/通知の運用 |
| 関連文書 | requirements.md / basic_design.md / job_design.md / external_api_design.md / environment_setup.md |

## 改訂履歴

| 版 | 日付 | 内容 | 作成/更新 |
| --- | --- | --- | --- |
| 1.0 | 2026-05-30 | 初版作成。日次確認、障害対応、クレジット、外部API、バックアップ復元を定義。 | ChatGPT |
| 1.1 | 2026-06-02 | MVP運用メトリクス、管理画面/API確認導線、Runbookスモークコマンドを追記。 | Codex |

## 1. 目的

本書は、開発者本人が運用するための監視、確認、障害対応、復旧、メンテナンス手順を定義する。秘密情報の実値は本書に記載しない。

## 2. 定常確認

| 頻度 | 確認項目 | 確認先 |
| --- | --- | --- |
| 毎日 | ジョブ失敗、キュー滞留、402/403、429急増、クレジット消費量 | 管理画面、ログ、メトリクス |
| 毎週 | ラッコAPI仕様更新、マスタ同期、DBサイズ、外部API失敗率 | 管理画面、OpenAPI差分 |
| 毎月 | 月次レポート生成、クレジット使用量 | レポート画面、DB運用画面 |
| 四半期 | リストア手順、Secretローテーション、不要ローデータ削除 | Runbook、Storage、Key Vault |

### 2.1 MVP確認導線

| 確認項目 | 画面/API | コマンド例 |
| --- | --- | --- |
| ジョブ失敗/滞留 | 管理画面 S-900 ジョブ、`GET /api/jobs` | `Invoke-WebRequest -UseBasicParsing "http://localhost:5251/api/jobs?status=all&page=1&pageSize=25"` |
| 402/403/429/クレジット | 管理画面 S-900 クレジット、`GET /api/admin/external-api-calls` | `Invoke-WebRequest -UseBasicParsing "http://localhost:5251/api/admin/external-api-calls?page=1&pageSize=50"` |
| 監査ログ | 管理画面 S-900 監査ログ、`GET /api/admin/audit-logs` | `Invoke-WebRequest -UseBasicParsing "http://localhost:5251/api/admin/audit-logs?page=1&pageSize=50"` |
| 通知失敗 | 管理画面 S-900 Discord通知、`GET /api/admin/notification-deliveries` | `Invoke-WebRequest -UseBasicParsing "http://localhost:5251/api/admin/notification-deliveries?status=all&page=1&pageSize=50"` |
| CSV出力 | S-010/S-020/S-030 のCSV出力、`POST /api/projects/{projectId}/exports/csv` | `Invoke-WebRequest -UseBasicParsing -Method Post -ContentType "application/json" -Body '{"exportType":"external_api_calls","filter":{},"columns":["provider","endpoint","statusCode","consumedCredit","cacheHit","errorCode","createdAt"]}' "http://localhost:5251/api/projects/{projectId}/exports/csv"` |

## 3. 主要アラート

| アラート | 閾値 | 初動 |
| --- | --- | --- |
| ジョブ失敗率高 | 直近1時間で5%超 | 失敗ジョブ一覧、エラー分類、外部API状態確認。 |
| API 402 | 1件以上 | 契約側のクレジット残量、API契約、対象ジョブの消費量を確認。 |
| API 403 | 連続3件 | APIキー状態、Secret参照、契約スコープを確認。 |
| API 429急増 | 直近10分で通常比3倍 | Worker同時実行数、バックオフ、キュー滞留を確認。 |
| キュー滞留 | 30分以上増加 | Worker稼働、DB接続、外部API遅延を確認。 |
| DB接続エラー | 連続発生 | DB稼働、接続文字列、コネクション枯渇を確認。 |
| 通知失敗 | retrying/failed増加 | Discord Webhook、レート制限、Secret参照を確認。 |

## 4. MVPメトリクス

OpenTelemetry Meter名は `SeoIntelligence`。MVPで記録する運用メトリクスは以下を正本にする。

| メトリクス | 種別 | 確認内容 |
| --- | --- | --- |
| `job_success_rate` | ObservableGauge | 直近1時間の成功/失敗/キャンセル状態ジョブに対する成功率。 |
| `job_queue_depth` | ObservableGauge | `queued` と `waiting_external` の滞留ジョブ数。 |
| `job_duration_p95` | Histogram | ジョブ完了/失敗/キャンセルまでの処理時間。p95はOTelバックエンド側で算出する。 |
| `external_api_429_count` | Counter | ラッコキーワードAPI等の429発生回数。 |
| `external_api_402_count` | Counter | クレジット不足402発生回数。 |
| `external_api_credit_consumed` | Counter | 外部APIレスポンスの `consumedCredit` 合計。 |
| `notification_failure_count` | Counter | Discord通知のretrying/failed発生回数。 |
| `retry_count_by_job_type` | Counter | 自動/手動再試行回数。`job_type` と `source` で確認する。 |

## 5. 障害対応手順

### 5.1 ジョブ失敗

1. 管理画面または`GET /api/jobs`で失敗ジョブを確認する。
2. `job_type`、`status`、`error_json`、`correlation_id`を確認する。
3. `external_api_calls`に紐付くHTTP status、error_code、consumed_creditを確認する。
4. `failed_retryable`なら原因が一時的であることを確認して手動再実行する。
5. `failed_fatal`なら入力、APIキー、契約側クレジット、契約スコープを修正して新規ジョブを登録する。

### 5.2 クレジット不足 402

1. `external_api_calls`で402発生ジョブとAPIキーを確認する。
2. `external_api_calls`で実消費、対象エンドポイント、対象ジョブ、APIキーを確認する。
3. 契約側のクレジット残量を確認する。
4. 大量ジョブは分割数、対象キーワード、重複除外を見直す。
5. 再実行する場合はfailed_fatalの同一ジョブを直接再実行せず、新しい条件で登録する。

### 5.3 APIキー無効 403

1. `api_credentials.status`がactiveであることを確認する。
2. `key_ref`がSecret Store上の正しいSecret名を指していることを確認する。
3. Secretの有効期限、ローテーション履歴、参照権限を確認する。
4. 必要なら`POST /api/admin/api-credentials/{credentialId}/rotate`でローテーションする。
5. テスト通知または軽量APIで疎通確認する。

### 5.4 レート制限 429

1. 直近の`external_api_calls`で429のendpointと頻度を確認する。
2. `RakkoKeyword__MaxConcurrentRequests`を一時的に下げる。
3. キュー滞留が増える場合は優先度の低いジョブを停止または延期する。
4. 429が収束したら同時実行数を段階的に戻す。

### 5.5 Discord通知失敗

1. `notification_deliveries`のstatus、error_message、retry_countを確認する。
2. Webhook Secret参照が正しいことを確認する。
3. Discord側のWebhook削除、レート制限、権限変更を確認する。
4. 修正後、手動再送APIを実行する。

## 6. バックアップ・復元

| 対象 | 方針 |
| --- | --- |
| PostgreSQL | 日次フルバックアップ、WAL/PITR有効化。 |
| Storage | ローデータ、レポート、CSV/Excelを冗長化。 |
| Secret | Key Vault等でバージョン管理。実値はRunbookに書かない。 |
| 復元検証 | 四半期ごとにステージング相当へ復元し、主要APIをスモークテストする。 |

復元時は、DB、Storage、Secret参照、アプリ設定の整合性を確認する。ローデータ本体を保持期間で削除済みの場合でも、DB上のハッシュ、ステータス、クレジット、契約スコープは監査用に残す。

## 7. デプロイ・メンテナンス

| 作業 | 手順 |
| --- | --- |
| API仕様更新 | `rakko-keyword-api-docs.json`差分確認、DTO再生成、契約テスト、影響確認。 |
| API契約変更 | 管理画面/APIでは契約スコープを変更しない。SeedDataまたはマイグレーション相当の保守手順で旧`api_contract_scopes`をarchivedにし、新しい`scope_key`を追加する。 |
| DBマイグレーション | dry-run、バックアップ確認、適用、スモークテスト。 |
| Secretローテーション | 新Secret登録、credential rotate、疎通確認、旧Secret無効化。 |
| Worker設定変更 | 同時実行数、キュー、ポーリング間隔を変更し、ジョブ成功率を監視。 |
| 保持期間変更 | `workspaces.retention_settings_json`更新、削除対象確認、監査情報保持確認。 |

## 8. スモークテスト

Runbookスモークは依存サービスReady、DB migration適用、API/Worker/Web起動、プロジェクト一覧、監査ログ検索、マスタ同期ジョブ完了、CSV出力ジョブ完了を確認する。Discordテスト通知はSecret参照が設定済みの通知チャンネルIDを `SMOKE_DISCORD_CHANNEL_ID` に渡した場合だけ実行する。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/smoke-local.ps1
```

既存プロジェクトでCSV出力を確認する場合:

```powershell
$env:SMOKE_PROJECT_ID = "<project-guid>"
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/smoke-local.ps1
```

Discordテスト通知まで含める場合:

```powershell
$env:SMOKE_DISCORD_CHANNEL_ID = "<notification-channel-guid>"
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/smoke-local.ps1
```

Playwrightで画面操作まで含める場合:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/smoke-local.ps1 -RunBrowserTests -InstallPlaywrightBrowsers
```

| 対象 | 確認 |
| --- | --- |
| API | `/healthz`、`/readyz`が成功する。 |
| DB | プロジェクト一覧、監査ログ検索が成功する。 |
| Worker | マスタ同期ジョブとCSV出力ジョブが`succeeded`になる。 |
| ラッコAPI | 地域/言語マスタまたは軽量APIが成功する。 |
| Discord | テスト通知が成功し履歴が残る。 |
| CSV出力 | 小規模データを出力でき、監査ログが残る。 |
| BrowserE2E | プロジェクト選択、キーワード探索、検索ボリューム登録、CSV出力ボタン操作が成功する。 |

## 9. インシデント記録

障害発生時は以下を残す。

| 項目 | 内容 |
| --- | --- |
| 発生日時 | Asia/TokyoとUTCを併記する。 |
| 影響範囲 | 画面、API、Worker、外部API、データ。 |
| correlation_id | 関連するID。 |
| job_id | 関連ジョブ。 |
| external_request_id | 外部API requestId。 |
| 原因 | 入力、外部API、クレジット、認証、DB、実装不具合など。 |
| 対応 | 実施した操作、再実行、設定変更。 |
| 再発防止 | テスト追加、監視追加、Runbook更新。 |
