# ジョブ設計書

**ラッコキーワードAPIを中核にしたSEOインテリジェンス基盤**

_SEO Intelligence Platform / SEOインテリジェンス基盤_

| 項目 | 内容 |
| --- | --- |
| 文書ID | JOB-RKSEO-001 |
| 作成日 | 2026-05-30 |
| 対象 | Worker Service / Hangfire / 外部APIポーリング / 通知 / レポート生成 |
| 関連文書 | requirements.md / basic_design.md / api_design.md / db_design.md / external_api_design.md |

## 改訂履歴

| 版 | 日付 | 内容 | 作成/更新 |
| --- | --- | --- | --- |
| 1.0 | 2026-05-30 | 初版作成。ジョブ一覧、状態遷移、キュー、リトライ、主要シーケンスを定義。 | ChatGPT |

## 1. 目的

本書は、非同期処理の実行単位、キュー、状態、リトライ、冪等性、外部requestId追跡、通知、監査を定義する。API設計書の非同期APIとDB設計書の`jobs`、`job_external_requests`、`external_api_calls`に対応する。

## 2. 共通設計

| 項目 | 設計 |
| --- | --- |
| 実行基盤 | .NET Worker Service + Hangfire + PostgreSQL storage。 |
| 状態正本 | アプリ業務状態は`jobs`テーブル。Hangfire内部テーブルは実行管理用であり監査正本にしない。 |
| 外部requestId | `job_external_requests`に分割単位で保持する。 |
| 外部API監査 | `external_api_calls`にリクエスト/レスポンスURI、ハッシュ、消費クレジット、契約スコープを保存する。 |
| 冪等性 | ジョブ登録APIは`Idempotency-Key`を受け付け、`jobs.idempotency_key`と`request_hash`で同一スコープ・同一条件の重複登録を抑止する。 |
| 分散ロック | 同一プロジェクト/同一ジョブ種別/同一対象の重複実行はRedis lockで抑止する。 |
| クレジット監視 | 外部API実行前に推定消費クレジットを記録し、実行後は`external_api_calls.consumed_credit`へ保存する。予算上限による事前停止は行わない。 |
| 通知 | 致命的失敗、クレジット不足402、順位アラート、レポート完了をDiscordへ送る。 |

## 3. キュー設計

| キュー | 対象ジョブ | 初期ワーカー数 | 備考 |
| --- | --- | --- | --- |
| `default` | 軽量ジョブ、管理系同期 | 1 | マスタ同期や短時間処理。 |
| `external-api` | ラッコキーワードAPI連携 | 2 | レート制限と外部API上限を優先。 |
| `polling` | statusポーリング | 1 | 長時間待機を短いジョブの再スケジュールで扱う。 |
| `analysis` | スコアリング、クラスタリング、カニバリ検出 | 1 | DB負荷を監視しながら増やす。 |
| `exports` | CSV/Excel/PDF/レポート | 1 | Storage書き込みと監査を伴う。 |
| `notifications` | Discord通知 | 1 | 失敗時は再送制御。 |
| `ai` | AI生成 | 1 | Phase 3。秘密情報除去とtoken_usage保存が必須。 |

## 4. 状態遷移

```text
queued -> canceled
queued -> running -> succeeded
queued -> running -> failed_retryable -> queued
queued -> running -> failed_fatal
queued -> running -> waiting_external -> canceled
queued -> running -> waiting_external -> failed_retryable -> queued
queued -> running -> waiting_external -> running -> failed_retryable -> queued
queued -> running -> waiting_external -> running -> failed_fatal
queued -> running -> waiting_external -> running -> succeeded
```

| status | 更新タイミング | 操作可否 |
| --- | --- | --- |
| queued | ジョブ登録直後、再試行待ち | cancel可 |
| running | Workerが処理開始 | cancel不可 |
| waiting_external | 外部APIの非同期requestId完了待ち | cancel可 |
| succeeded | 全処理成功 | 結果参照可 |
| failed_retryable | 429/500/503、DB一時障害など | retry可 |
| failed_fatal | 400/402/403、入力不正など | retry不可 |
| canceled | 実行前または待機中にキャンセル | retry不可 |

`waiting_external`のキャンセルは、外部API側のrequestId自体を取り消さず、内部ジョブのポーリングと結果取得を停止する。キャンセル後に外部API側で完了しても、結果は業務テーブルへ取り込まず、既存の`external_api_calls`と`audit_logs`を監査記録として保持する。

`failed_retryable`は外部requestId登録前、ポーリング中、結果取得/保存中のいずれでも発生し得る。再試行時は同じ内部`jobs.id`を`queued`へ戻し、既存の`job_external_requests`と`external_api_calls`を参照して重複登録や二重取込を避ける。

## 5. リトライ設計

| エラー | 処理 | 最大回数 | 通知 |
| --- | --- | --- | --- |
| 外部API 429 | 指数バックオフ + ジッター、同時実行数を一時低下 | 5 | 連続発生時に通知 |
| 外部API 500 | 短期リトライ | 3 | 最終失敗時 |
| 外部API 503 | 長めのバックオフ | 5 | 最終失敗時 |
| 外部API 400 | 入力/マッピング不備としてfailed_fatal | 0 | 開発者通知 |
| 外部API 402 | クレジット不足としてfailed_fatal | 0 | 即時通知 |
| 外部API 403 | APIキー無効としてfailed_fatal | 0 | 即時通知 |
| DB一時障害 | retryableとして再実行 | 3 | 最終失敗時 |
| 通知送信失敗 | `notification_deliveries`をretryingへ更新 | 5 | 管理画面で確認 |

## 6. ジョブ一覧

| ジョブ | Phase | キュー | 起動 | 主な出力 |
| --- | --- | --- | --- | --- |
| `MasterDataSyncJob` | MVP | default | 手動/定期 | `locations`、`languages` |
| `KeywordDiscoveryJob` | MVP | external-api | 手動 | `keyword_suggestions`、`related_keywords`、`questions`、`lsi_paa_items`、`ranking_keywords` |
| `RegisterSearchVolumeJob` | MVP | external-api | 手動 | `search_volume_jobs`、`job_external_requests` |
| `PollSearchVolumeStatusJob` | MVP | polling | 再スケジュール | `job_external_requests.status` |
| `FetchSearchVolumeResultsJob` | MVP | external-api | status完了後 | `search_volume_results`、`keyword_metrics`、`keyword_monthly_volumes` |
| `OpportunityScoringJob` | MVP | analysis | 調査完了後 | opportunity score更新 |
| `DataExportJob` | MVP/Phase 3 | exports | 手動 | `data_exports`、Storageファイル、`audit_logs` |
| `CompetitorRefreshJob` | Phase 2 | external-api | 手動/定期 | `competitive_results`、`influx_keyword_results`、`influx_page_results` |
| `ContentAnalyzeJob` | Phase 2 | external-api | 手動 | `content_search_results`、`serp_headline_pages`、`co_occurrence_words` |
| `TopicClusterGenerateJob` | Phase 2 | analysis | 手動 | `topic_clusters`、`cluster_keywords` |
| `GenerateBriefJob` | Phase 2 | analysis | 手動 | `article_briefs`、`artifact_versions` |
| `RegisterRankCheckJob` | Phase 2 | external-api | 手動/定期 | `rank_check_jobs`、`job_external_requests` |
| `PollRankStatusJob` | Phase 2 | polling | 再スケジュール | `job_external_requests.status` |
| `FetchRankResultsJob` | Phase 2 | external-api | status完了後 | `rank_results`、アラート評価 |
| `RankAlertEvaluateJob` | Phase 2 | analysis | 順位取得後 | `alerts`、`alert_events`、`notification_deliveries` |
| `RewriteScoringJob` | Phase 3 | analysis | 手動/定期 | `rewrite_tasks` |
| `CannibalizationDetectionJob` | Phase 3 | analysis | 手動/定期 | `cannibalization_candidates` |
| `MonthlyReportJob` | Phase 3 | exports | 定期/手動 | `reports`、`artifact_versions`、Storageファイル、`audit_logs`、`notification_deliveries` |
| `DataImportJob` | Phase 3 | exports | 手動 | `data_imports`、取込対象テーブル |
| `AiAssistantJob` | Phase 3 | ai | 手動 | `ai_sessions`、`ai_messages`、成果物 |
| `NotificationDeliveryJob` | MVP | notifications | イベント発生時 | `notification_deliveries` |

## 7. 主要ジョブ詳細

### 7.1 KeywordDiscoveryJob

| 項目 | 内容 |
| --- | --- |
| 入力 | `projectId`、seed keyword、sources、filter、limit、sortBy/orderBy。 |
| 処理 | suggest、related、other、question、rankingを条件に応じて呼び出し、キーワード正規化と重複排除を行う。 |
| 出力 | `keyword_seeds`、`keywords`、`keyword_suggestions`、`related_keywords`、`questions`、`lsi_paa_items`、`ranking_keywords`。 |
| 冪等キー | projectId + normalized seed + sources + filter hash。 |
| 失敗処理 | 取得済みAPIの結果は保存し、未取得APIはretryableとして再実行可能にする。 |

### 7.2 SearchVolume系ジョブ

| 項目 | 内容 |
| --- | --- |
| 登録 | `RegisterSearchVolumeJob`が最大50,000語を重複排除し、クレジット消費見込みを記録して外部APIへ登録する。 |
| 分割 | 外部API上限、レート制御、入力件数に応じて複数の`job_external_requests`へ分割する。 |
| ポーリング | `PollSearchVolumeStatusJob`が完了まで再スケジュールする。 |
| 結果取得 | `FetchSearchVolumeResultsJob`が結果を取得し、`keyword_metrics`と`keyword_monthly_volumes`を更新する。 |
| 監査 | 各外部呼び出しを`external_api_calls`へ保存する。 |

### 7.3 RankCheck系ジョブ

| 項目 | 内容 |
| --- | --- |
| 登録 | keywords、targets、matchType、depth、withMetricsを検証し外部APIへ登録する。 |
| 上限 | targetsは1から50件、depthは許可値のみ。 |
| 結果 | `rank_results`へ順位、ranked_url、推定流入、source_call_id、contract_scope_key、checked_atを保存する。 |
| 後続 | Phase 2では`RankAlertEvaluateJob`で`alert_events`を保存し、必要に応じて`rank_alert`通知を送る。カニバリ候補更新と月次レポート材料更新はPhase 3の`CannibalizationDetectionJob` / `MonthlyReportJob`で扱う。 |

### 7.4 ContentAnalyzeJob

| 項目 | 内容 |
| --- | --- |
| 入力 | keyword、content-search/headline/co-occurrenceの実行有無、limit。 |
| 処理 | 集客コンテンツ、SERP見出し、共起語を順に取得する。 |
| 出力 | `content_search_results`、`serp_headline_pages`、`serp_headlines`、`co_occurrence_words`、`co_occurrence_page_details`。 |
| 後続 | `GenerateBriefJob`の根拠データになる。 |

### 7.5 DataExportJob

| 項目 | 内容 |
| --- | --- |
| 入力 | export_type、format、filter、columns。 |
| 処理 | プロジェクトスコープを検証し、DBから抽出してStorageへファイルを保存する。 |
| 出力 | `data_exports.file_uri`、認証必須のdownloadUrl（`.../content`）、`audit_logs`。 |
| Phase | MVPはCSVのみ。Phase 3でExcelを追加する。 |

### 7.6 MonthlyReportJob

| 項目 | 内容 |
| --- | --- |
| 入力 | report_type、period、format（pdf/excel）、sections、share_expires_at。 |
| 処理 | プロジェクトスコープを検証し、集計データと成果物バージョンを作成してStorageへ保存する。 |
| 出力 | `reports.format`、`reports.file_uri`、`reports.current_version`、`artifact_versions`、`audit_logs`。レポート完了通知を送る場合は`notification_deliveries.resource_type=report`、`resource_id=reportId`で送信元を保持する。 |
| ダウンロード | `GET /api/projects/{projectId}/reports/{reportId}/download`が取得先URLを返し、発行操作を`audit_logs`へ記録する。ファイル本体は`.../content`が配信し、取得を`audit_logs`へ記録する。 |

### 7.7 NotificationDeliveryJob

| 項目 | 内容 |
| --- | --- |
| 入力 | event_type、payload、channel_id、job_id、resource_type、resource_id、correlation_id。 |
| 処理 | Secret StoreからWebhook URLを取得してDiscordへ送信する。 |
| 出力 | `notification_deliveries.status`、`sent_at`、`delivered_at`、送信元参照、エラー情報。 |
| 再送 | retrying状態で`next_retry_at`以降に再送する。 |

## 8. スケジュール

| ジョブ | 頻度 | 備考 |
| --- | --- | --- |
| `MasterDataSyncJob` | 週次または手動 | ラッコAPI仕様変更時にも実行。 |
| `PollSearchVolumeStatusJob` | 60秒間隔 | `nextRunAt`で個別制御。 |
| `PollRankStatusJob` | 60秒間隔 | `nextRunAt`で個別制御。 |
| `CompetitorRefreshJob` | 週次 | Phase 2以降。 |
| `RegisterRankCheckJob` | 日次または週次 | 重要キーワードのみ日次。 |
| `RewriteScoringJob` | 週次 | 順位取得後に再計算。 |
| `MonthlyReportJob` | 月次 | Asia/Tokyoの月初に前月分を生成。 |

## 9. 監視メトリクス

| メトリクス | 用途 |
| --- | --- |
| job_success_rate | ジョブ成功率。 |
| job_queue_depth | キュー滞留数。 |
| job_duration_p95 | ジョブ種別ごとの処理時間。 |
| external_api_429_count | レート制限検知。 |
| external_api_credit_consumed | クレジット消費。 |
| external_api_402_count | クレジット不足検知。 |
| notification_failure_count | 通知失敗検知。 |
| retry_count_by_job_type | 再試行過多の検出。 |

## 10. 受入観点

| 観点 | 確認内容 |
| --- | --- |
| 冪等性 | 同じIdempotency-Keyと同一request hashで二重登録されない。 |
| 分割 | 50,000語調査や複数URL順位チェックが分割される。 |
| クレジット | consumedCreditが外部API呼び出しごとに保存され、402時にfailed_fatalと通知へ分岐する。 |
| リトライ | 429/500/503は再試行、400/402/403は再試行しない。 |
| 監査 | 外部API実行、ジョブ操作、出力が監査ログに残る。 |
| 通知 | ジョブ失敗、クレジット不足、順位アラート、レポート完了が通知される。 |
