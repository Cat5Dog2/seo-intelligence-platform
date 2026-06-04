# DB設計書

**ラッコキーワードAPIを中核にしたSEOインテリジェンス基盤**

_SEO Intelligence Platform / SEOインテリジェンス基盤_

| 項目 | 内容 |
| --- | --- |
| 文書ID | DB-RKSEO-001 |
| 作成日 | 2026-05-30 |
| 対象DB | PostgreSQL |
| 利用方式 | EF Core / Hangfire PostgreSQL storage / JSONB / Blob Storage連携 |
| 関連文書 | requirements.md / basic_design.md / api_design.md |

## 改訂履歴

| 版 | 日付 | 内容 | 作成/更新 |
| --- | --- | --- | --- |
| 1.0 | 2026-05-30 | 初版作成。論理テーブル、主要カラム、制約、インデックス、保持方針を定義。 | ChatGPT |

## 1. 目的

本書は、SEOインテリジェンス基盤のDB論理設計を定義する。外部APIローデータ、正規化キーワード、分析結果、ジョブ、成果物、監査ログを分離し、プロジェクトスコープと契約スコープを追跡可能にする。

## 2. 設計方針

| 方針 | 内容 |
| --- | --- |
| PostgreSQL中心 | 業務データはPostgreSQLに保存し、ローデータ本体や生成ファイルはStorageへ逃がす。 |
| グローバルマスタ限定 | `keywords`、`locations`、`languages`のみワークスペース非依存で共有する。 |
| プロジェクトスコープ | 業務結果テーブルは原則`workspace_id`または`project_id`を持つ。 |
| 契約スコープ追跡 | 検索指標キャッシュは`contract_scope_key`が一致する場合のみ再利用する。 |
| 監査優先 | APIキー操作、外部API呼び出し、ジョブ、出力、AI実行は監査可能な形で保持する。 |
| ソフト削除 | 業務データは物理削除せず、`status`で無効化/アーカイブする。 |
| JSONB併用 | 可変な分析条件、外部レスポンススナップショット、AI参照データはJSONBで保持する。 |

## 3. 共通データ型・規約

| 項目 | 規約 |
| --- | --- |
| 主キー | 内部IDは`uuid`。アプリケーション生成のUUID v7相当を想定する。 |
| ゼロUUID | インデックス説明上の`zero_uuid`は`00000000-0000-0000-0000-000000000000`を指し、`project_id` NULLのワークスペーススコープを一意化するための表記とする。 |
| 日時 | `timestamptz`、保存はUTC。画面上の日次/月次リセットはAsia/Tokyoで計算する。 |
| 金額/クレジット | `numeric(18,4)`。整数で足りる項目も外部仕様変更に備える。 |
| URL | 原文URLは`text`、検索用ドメインは別カラムに正規化する。 |
| JSON | 条件、外部レスポンス要約、変更前後、AI参照は`jsonb`。 |
| ステータス | DBでは`text` + CHECK制約またはEF Core enum変換で管理する。 |
| 監査主体 | 初期版は固定文字列`developer`を保存する。 |

## 4. スキーマ概要

```text
workspaces
  -> projects
       -> sites
       -> keyword_seeds
       -> jobs -> job_external_requests -> external_api_calls
       -> analysis result tables
       -> article_briefs / rewrite_tasks / reports

keywords, locations, languages
  -> metrics cache tables

api_credentials, api_contract_scopes
  -> external_api_calls

notification_channels
  -> notification_deliveries

external_connector_settings
  -> external_connector_runs
```

## 5. テーブル定義

### 5.1 ワークスペース・管理

| テーブル | 主なカラム | 制約/備考 |
| --- | --- | --- |
| `workspaces` | `id uuid PK`, `name text`, `default_location text`, `default_language text`, `retention_settings_json jsonb`, `notification_defaults_json jsonb`, `status text`, `created_at timestamptz`, `updated_at timestamptz` | 初期版は1行運用。`status=active`のみ使用する。 |
| `projects` | `id uuid PK`, `workspace_id uuid FK`, `name text`, `default_location text`, `default_language text`, `kpi_json jsonb`, `memo text`, `status text`, `created_at`, `updated_at`, `archived_at` | `workspace_id -> workspaces.id`。アーカイブは`status=archived`。 |
| `sites` | `id uuid PK`, `project_id uuid FK`, `domain text`, `canonical_url text`, `type text`, `memo text`, `status text`, `created_at`, `updated_at`, `archived_at` | `project_id -> projects.id`。`type`はown/competitor/reference等。 |
| `api_credentials` | `id uuid PK`, `workspace_id uuid FK`, `provider text`, `key_ref text`, `status text`, `created_at`, `updated_at`, `disabled_at` | キー値は保持しない。`key_ref`のみ保存。 |
| `api_contract_scopes` | `id uuid PK`, `workspace_id uuid FK`, `provider text`, `plan_name text`, `api_key_limit integer`, `data_usage_scope text`, `confirmed_at timestamptz`, `confirmed_by text`, `effective_from date`, `effective_to date`, `scope_key text`, `status text`, `created_at` | `scope_key`は契約スコープ判定の正本。管理画面/APIでは管理せず、初期データまたは運用手順で更新する。 |
| `notification_channels` | `id uuid PK`, `workspace_id uuid FK`, `project_id uuid NULL FK`, `channel_type text`, `name text`, `webhook_secret_ref text`, `event_types_json jsonb`, `status text`, `created_at`, `updated_at`, `disabled_at` | Webhook URL実値は保持しない。 |
| `notification_deliveries` | `id uuid PK`, `workspace_id uuid FK`, `project_id uuid NULL FK`, `channel_id uuid FK`, `job_id uuid NULL FK`, `resource_type text NULL`, `resource_id text NULL`, `event_type text`, `payload_hash text`, `status text`, `error_message text`, `retry_count integer`, `next_retry_at timestamptz`, `sent_at`, `delivered_at`, `correlation_id text`, `created_at` | 再送制御と送信履歴。ジョブ失敗、順位アラート、レポート完了などの送信元を追跡する。 |
| `audit_logs` | `id uuid PK`, `workspace_id uuid FK`, `actor text`, `action text`, `resource_type text`, `resource_id text`, `before_after_json jsonb`, `correlation_id text`, `ip_address inet NULL`, `user_agent text NULL`, `created_at` | APIキー操作、外部実行、出力、AI、共有URLを記録。 |

### 5.2 マスタ・外部API・ジョブ

| テーブル | 主なカラム | 制約/備考 |
| --- | --- | --- |
| `locations` | `id uuid PK`, `provider text`, `location_code text`, `location_name text`, `country_code text`, `status text`, `synced_at timestamptz` | 地域マスタ。`UNIQUE(provider, location_code)`。 |
| `languages` | `id uuid PK`, `provider text`, `language_code text`, `language_name text`, `status text`, `synced_at timestamptz` | 言語マスタ。`UNIQUE(provider, language_code)`。 |
| `external_api_calls` | `id uuid PK`, `workspace_id uuid FK`, `project_id uuid NULL FK`, `job_id uuid NULL FK`, `api_credential_id uuid NULL FK`, `api_contract_scope_id uuid NULL FK`, `provider text`, `endpoint text`, `request_hash text`, `request_uri text`, `response_hash text`, `response_uri text`, `contract_scope_key text`, `cache_hit boolean`, `status_code integer`, `consumed_credit numeric(18,4)`, `duration_ms integer`, `error_code text`, `correlation_id text`, `actor text`, `retained_until timestamptz`, `created_at` | 外部API呼び出し監査。ローデータ本体はStorage。 |
| `jobs` | `id uuid PK`, `workspace_id uuid FK`, `project_id uuid NULL FK`, `job_type text`, `status text`, `progress integer`, `retry_count integer`, `next_run_at timestamptz`, `result_resource_type text`, `result_resource_id uuid NULL`, `error_json jsonb`, `idempotency_key text NULL`, `request_hash text NULL`, `requested_by text`, `created_at`, `updated_at`, `completed_at` | アプリ側ジョブ状態。Hangfireの内部テーブルとは分離する。`Idempotency-Key`指定時は同一スコープの重複登録を抑止する。 |
| `job_external_requests` | `id uuid PK`, `job_id uuid FK`, `endpoint text`, `external_request_id text`, `sequence_no integer`, `status text`, `retry_count integer`, `source_call_id uuid NULL FK`, `consumed_credit numeric(18,4)`, `error_json jsonb`, `created_at`, `updated_at`, `completed_at` | 1ジョブが複数requestIdへ分割されるケースを追跡。 |

### 5.3 キーワード・検索ボリューム

| テーブル | 主なカラム | 制約/備考 |
| --- | --- | --- |
| `keyword_seeds` | `id uuid PK`, `project_id uuid FK`, `seed text`, `source text`, `memo text`, `created_at` | 画面入力やCSV取込の起点語。 |
| `keywords` | `id uuid PK`, `normalized_text text`, `language text`, `text_hash text`, `created_at` | グローバル正規化マスタ。`UNIQUE(language, text_hash)`。 |
| `keyword_metrics` | `id uuid PK`, `keyword_id uuid FK`, `location text`, `language text`, `contract_scope_key text`, `source_call_id uuid NULL FK`, `search_volume integer`, `seo_difficulty numeric(8,4)`, `cpc numeric(18,4)`, `competition numeric(8,4)`, `first_seen_range text`, `fetched_at timestamptz` | 指標履歴/最新指標。契約スコープ単位で再利用判定。 |
| `keyword_monthly_volumes` | `id uuid PK`, `keyword_id uuid FK`, `location text`, `language text`, `contract_scope_key text`, `source_call_id uuid NULL FK`, `year_month char(7)`, `search_volume integer`, `fetched_at timestamptz` | `year_month`は`YYYY-MM`。取得回ごとの月別履歴を保持する。 |
| `project_keyword_scores` | `id uuid PK`, `project_id uuid FK`, `keyword_id uuid FK`, `location text`, `language text`, `source_call_id uuid NULL FK`, `opportunity_score numeric(8,4)`, `score_components_json jsonb`, `scored_at timestamptz` | プロジェクト別の機会スコア正本。関連度や係数がプロジェクト依存のため`keyword_metrics`から分離する。 |
| `keyword_suggestions` | `id uuid PK`, `seed_id uuid FK`, `keyword_id uuid FK`, `engine text`, `suggest_class text`, `engine_count integer`, `first_seen_range text`, `created_at` | サジェスト結果。 |
| `related_keywords` | `id uuid PK`, `seed_id uuid FK`, `keyword_id uuid FK`, `match_type text`, `metrics_snapshot_json jsonb`, `created_at` | 関連語結果。 |
| `questions` | `id uuid PK`, `project_id uuid FK`, `seed_keyword_id uuid NULL FK`, `question_text text`, `source text`, `importance numeric(8,4)`, `created_at` | FAQ/PAA質問。 |
| `lsi_paa_items` | `id uuid PK`, `seed_keyword_id uuid FK`, `type text`, `keyword_id uuid NULL FK`, `question_text text`, `importance numeric(8,4)`, `created_at` | LSI/PAA。 |
| `ranking_keywords` | `id uuid PK`, `seed_keyword_id uuid FK`, `keyword_id uuid FK`, `word_count integer`, `relevance numeric(8,4)`, `metrics_snapshot_json jsonb`, `created_at` | 同時ランクイン語。 |
| `search_volume_jobs` | `job_id uuid PK/FK`, `location text`, `language text`, `aggregation_months integer`, `request_options_json jsonb`, `status_json jsonb` | `job_id -> jobs.id`。 |
| `search_volume_results` | `id uuid PK`, `job_id uuid FK`, `keyword_id uuid FK`, `data_source text`, `source_call_id uuid NULL FK`, `cache_hit boolean`, `metrics_snapshot_json jsonb`, `trends_json jsonb`, `created_at` | UI返却用のプロジェクトスコープ結果。 |

### 5.4 競合・コンテンツ分析

| テーブル | 主なカラム | 制約/備考 |
| --- | --- | --- |
| `competitor_sites` | `id uuid PK`, `project_id uuid FK`, `domain text`, `source text`, `duplicate_rate numeric(8,4)`, `estimated_traffic numeric(18,4)`, `created_at` | 競合候補。 |
| `influx_keyword_results` | `id uuid PK`, `project_id uuid FK`, `target text`, `keyword_id uuid FK`, `rank integer`, `ranked_url text`, `estimated_traffic numeric(18,4)`, `metrics_snapshot_json jsonb`, `created_at` | 獲得キーワード結果。 |
| `influx_page_results` | `id uuid PK`, `project_id uuid FK`, `target text`, `page_url text`, `title text`, `keyword_count integer`, `estimated_traffic numeric(18,4)`, `traffic_value numeric(18,4)`, `top_keyword_id uuid NULL FK`, `created_at` | 獲得ページ結果。 |
| `competitive_results` | `id uuid PK`, `project_id uuid FK`, `site_domain text`, `estimated_traffic numeric(18,4)`, `traffic_value numeric(18,4)`, `keyword_count integer`, `duplicate_rate numeric(8,4)`, `unique_counts_json jsonb`, `created_at` | 競合抽出結果。 |
| `content_search_results` | `id uuid PK`, `project_id uuid FK`, `keyword_id uuid FK`, `url text`, `domain text`, `title text`, `description text`, `estimated_traffic numeric(18,4)`, `traffic_value numeric(18,4)`, `top_keyword_id uuid NULL FK`, `created_at` | 集客コンテンツ検索結果。 |
| `serp_headline_pages` | `id uuid PK`, `project_id uuid FK`, `keyword_id uuid FK`, `rank integer`, `url text`, `title text`, `description text`, `headline_count integer`, `word_count integer`, `created_at` | 見出し抽出対象ページ。 |
| `serp_headlines` | `id uuid PK`, `page_id uuid FK`, `level smallint`, `text text`, `order_no integer` | H1-H6明細。 |
| `co_occurrence_words` | `id uuid PK`, `project_id uuid FK`, `keyword_id uuid FK`, `word text`, `occurrence_counts_json jsonb`, `site_counts_json jsonb`, `created_at` | 共起語集計。 |
| `co_occurrence_page_details` | `id uuid PK`, `co_word_id uuid FK`, `rank integer`, `url text`, `title text`, `count integer`, `count_in_headline integer`, `count_in_title integer` | 共起語URL別詳細。 |

### 5.5 クラスター・記事・順位・レポート

| テーブル | 主なカラム | 制約/備考 |
| --- | --- | --- |
| `topic_clusters` | `id uuid PK`, `project_id uuid FK`, `name text`, `parent_id uuid NULL FK`, `representative_keyword_id uuid NULL FK`, `score numeric(8,4)`, `created_at`, `updated_at` | 親子クラスター。 |
| `cluster_keywords` | `cluster_id uuid FK`, `keyword_id uuid FK`, `role text`, `opportunity_score numeric(8,4)`, `intent_label text` | `PRIMARY KEY(cluster_id, keyword_id)`。 |
| `article_briefs` | `id uuid PK`, `project_id uuid FK`, `cluster_id uuid NULL FK`, `title text`, `target_keyword_id uuid NULL FK`, `current_version integer`, `content_json jsonb`, `review_status text`, `status text`, `created_at`, `updated_at` | 記事ブリーフ本体。 |
| `rewrite_tasks` | `id uuid PK`, `project_id uuid FK`, `target_url text`, `priority_score numeric(8,4)`, `reason_json jsonb`, `status text`, `assignee_actor text`, `created_at`, `updated_at` | 初期版の担当者は`developer`。 |
| `cannibalization_candidates` | `id uuid PK`, `project_id uuid FK`, `keyword_id uuid FK`, `primary_url text`, `competing_urls_json jsonb`, `severity_score numeric(8,4)`, `evidence_json jsonb`, `recommendation_json jsonb`, `status text`, `detected_at timestamptz` | カニバリ候補。 |
| `rank_check_jobs` | `job_id uuid PK/FK`, `depth integer`, `match_type text`, `with_metrics boolean`, `request_options_json jsonb`, `status_json jsonb` | `job_id -> jobs.id`。 |
| `rank_check_targets` | `id uuid PK`, `job_id uuid FK`, `target text`, `target_type text` | URL/ドメイン等の順位チェック対象。 |
| `rank_results` | `id uuid PK`, `job_id uuid FK`, `project_id uuid FK`, `keyword_id uuid FK`, `target text`, `position integer`, `ranked_url text`, `estimated_traffic numeric(18,4)`, `metrics_snapshot_json jsonb`, `source_call_id uuid NULL FK`, `contract_scope_key text`, `checked_at timestamptz` | 順位履歴。外部API結果の出自と契約スコープを追跡する。 |
| `alerts` | `id uuid PK`, `project_id uuid FK`, `alert_type text`, `condition_json jsonb`, `notification_channel_id uuid NULL FK`, `status text`, `last_triggered_at timestamptz`, `created_at`, `updated_at` | アラート定義。発火履歴の正本は`alert_events`。 |
| `alert_events` | `id uuid PK`, `alert_id uuid FK`, `project_id uuid FK`, `job_id uuid NULL FK`, `keyword_id uuid NULL FK`, `event_type text`, `previous_value_json jsonb`, `current_value_json jsonb`, `evidence_json jsonb`, `notification_delivery_id uuid NULL FK`, `triggered_at timestamptz`, `resolved_at timestamptz NULL` | アラート発火履歴の正本。順位差分、圏外化、競合抜かれ等の根拠と通知結果を保持する。 |
| `reports` | `id uuid PK`, `project_id uuid FK`, `report_type text`, `period text`, `format text`, `current_version integer`, `file_uri text`, `share_token_hash text`, `share_expires_at timestamptz`, `status text`, `generated_by text`, `created_at`, `updated_at` | `format`はpdf/excel。共有URLはトークンハッシュのみ保存。 |
| `artifact_versions` | `id uuid PK`, `workspace_id uuid FK`, `project_id uuid NULL FK`, `artifact_type text`, `artifact_id uuid`, `version_no integer`, `content_hash text`, `content_uri text`, `content_json jsonb`, `created_by text`, `review_status text`, `change_summary text`, `created_at` | Phase 2から記事ブリーフ版履歴で使用する。Phase 3でレポート、AI生成物の版管理にも利用する。 |

### 5.6 入出力・AI

| テーブル | 主なカラム | 制約/備考 |
| --- | --- | --- |
| `data_exports` | `id uuid PK`, `workspace_id uuid FK`, `project_id uuid NULL FK`, `export_type text`, `format text`, `filter_json jsonb`, `file_uri text`, `status text`, `requested_by text`, `created_at`, `completed_at` | CSV/Excel出力履歴。 |
| `data_imports` | `id uuid PK`, `workspace_id uuid FK`, `project_id uuid NULL FK`, `import_type text`, `format text`, `source_file_uri text`, `status text`, `validation_errors_json jsonb`, `requested_by text`, `created_at`, `completed_at` | CSV/Excel取込履歴。 |
| `external_connector_settings` | `id uuid PK`, `workspace_id uuid FK`, `project_id uuid NULL FK`, `connector_type text`, `name text`, `auth_ref text NULL`, `settings_json jsonb`, `status text`, `created_at`, `updated_at`, `disabled_at` | GSC/GA4/CMS/BI等のPhase 3スタブ設定。OAuth/APIキー実値は保持しない。 |
| `external_connector_runs` | `id uuid PK`, `connector_setting_id uuid FK`, `workspace_id uuid FK`, `project_id uuid NULL FK`, `run_type text`, `status text`, `request_json jsonb`, `result_summary_json jsonb`, `error_json jsonb`, `started_at timestamptz`, `completed_at timestamptz`, `created_at` | 実データ連携前の接続テスト/スタブ実行履歴。 |
| `ai_sessions` | `id uuid PK`, `workspace_id uuid FK`, `project_id uuid NULL FK`, `actor text`, `title text`, `created_at`, `updated_at` | AI会話セッション。 |
| `ai_messages` | `id uuid PK`, `session_id uuid FK`, `message_role text`, `prompt text`, `response text`, `tool_calls_json jsonb`, `reference_data_json jsonb`, `redaction_status text`, `review_status text`, `token_usage jsonb`, `created_at` | プロンプト、出力、参照データ、トークン使用量。 |

## 6. 主なリレーション

| 関係 | 内容 |
| --- | --- |
| `workspaces -> projects` | 1ワークスペースに複数プロジェクト。初期版は1ワークスペース固定。 |
| `projects -> sites` | プロジェクト配下の自社/競合/参考サイト。 |
| `projects -> jobs` | ジョブはプロジェクトに紐付く。マスタ同期などは`project_id` NULLを許可。 |
| `jobs -> job_external_requests` | 一括処理の分割requestIdを保持する。 |
| `external_api_calls -> api_contract_scopes` | 外部API実行時の契約スコープを追跡する。 |
| `external_connector_settings -> external_connector_runs` | Phase 3外部連携スタブの設定と実行履歴。 |
| `keywords -> keyword_metrics` | グローバルキーワードに対して地域/言語/契約スコープ別の指標を持つ。 |
| `projects/keywords -> project_keyword_scores` | プロジェクト別の機会スコアと算出根拠を保持する。 |
| `topic_clusters -> cluster_keywords` | クラスターとキーワードの多対多。 |
| `article_briefs/reports -> artifact_versions` | 成果物の最新版は本体テーブル、履歴は`artifact_versions`。 |
| `alerts -> alert_events` | アラート定義と発火履歴。通知結果は`notification_delivery_id`で追跡する。 |
| `notification_channels -> notification_deliveries` | 通知設定と送信履歴。送信元は`job_id`、`resource_type`、`resource_id`で追跡する。 |

## 7. インデックス設計

| テーブル | インデックス | 目的 |
| --- | --- | --- |
| `keywords` | `UNIQUE(language, text_hash)`, `GIN(normalized_text gin_trgm_ops)` | 重複排除、候補語検索。 |
| `locations` | `UNIQUE(provider, location_code)`, `INDEX(status)` | 地域マスタ同期。 |
| `languages` | `UNIQUE(provider, language_code)`, `INDEX(status)` | 言語マスタ同期。 |
| `projects` | `INDEX(workspace_id, status)`, `UNIQUE(workspace_id, name)` | プロジェクト一覧、重複防止。 |
| `sites` | `INDEX(project_id, status)`, `INDEX(domain)` | サイト一覧、ドメイン検索。 |
| `api_contract_scopes` | `UNIQUE(scope_key)`, `INDEX(workspace_id, provider, status)`, `INDEX(effective_from, effective_to)` | 契約スコープ判定。 |
| `external_api_calls` | `INDEX(provider, endpoint, created_at)`, `INDEX(status_code)`, `INDEX(contract_scope_key)`, `INDEX(response_hash)`, `INDEX(correlation_id)` | API監査、障害分析、キャッシュ判定。 |
| `jobs` | `INDEX(status, next_run_at)`, `INDEX(workspace_id, project_id, created_at)`, `UNIQUE(workspace_id, COALESCE(project_id, zero_uuid), job_type, idempotency_key) WHERE idempotency_key IS NOT NULL` | Workerポーリング、履歴検索、冪等ジョブ登録。`project_id` NULLのワークスペーススコープジョブも重複抑止対象にする。 |
| `job_external_requests` | `INDEX(job_id, sequence_no)`, `INDEX(external_request_id)`, `INDEX(status, updated_at)` | 外部requestIdポーリング。 |
| `keyword_metrics` | `INDEX(keyword_id, location, language, contract_scope_key, fetched_at DESC)`, `INDEX(source_call_id)` | 最新指標、履歴参照。 |
| `keyword_monthly_volumes` | `INDEX(keyword_id, location, language, contract_scope_key, year_month, fetched_at DESC)`, `INDEX(source_call_id)` | 月別推移、取得回ごとの履歴参照。 |
| `project_keyword_scores` | `UNIQUE(project_id, keyword_id, location, language)`, `INDEX(project_id, opportunity_score DESC)`, `INDEX(source_call_id)` | 機会スコア上位、再計算根拠追跡。 |
| `search_volume_results` | `INDEX(job_id)`, `INDEX(keyword_id)`, `INDEX(cache_hit)` | 調査結果表示。 |
| `rank_results` | `INDEX(project_id, keyword_id, target, checked_at DESC)`, `INDEX(position)`, `INDEX(source_call_id)`, `INDEX(contract_scope_key)` | 順位履歴、順位帯抽出、外部API出自追跡。 |
| `cannibalization_candidates` | `INDEX(project_id, keyword_id, detected_at DESC)`, `INDEX(project_id, status, severity_score DESC)` | カニバリ候補一覧。 |
| `alert_events` | `INDEX(project_id, triggered_at DESC)`, `INDEX(alert_id, triggered_at DESC)`, `INDEX(notification_delivery_id)` | アラート発火履歴、定義別履歴、通知結果追跡。 |
| `notification_deliveries` | `INDEX(workspace_id, project_id, created_at)`, `INDEX(status, next_retry_at)`, `INDEX(job_id)`, `INDEX(resource_type, resource_id)`, `INDEX(correlation_id)` | 送信履歴、再送制御、送信元追跡。 |
| `influx_keyword_results` | `INDEX(project_id, target)`, `INDEX(keyword_id)`, `INDEX(rank)` | 競合ギャップ、順位条件。 |
| `content_search_results` | `INDEX(project_id, keyword_id)`, `INDEX(domain)`, `GIN(to_tsvector('simple', concat_ws(' ', title, description)))` | コンテンツ検索。 |
| `reports` | `INDEX(project_id, report_type, period, format)`, `INDEX(share_expires_at)`, `INDEX(share_token_hash)` | レポート一覧、形式別検索、共有URL。 |
| `artifact_versions` | `UNIQUE(artifact_type, artifact_id, version_no)`, `INDEX(workspace_id, project_id, created_at)` | 成果物履歴。 |
| `data_exports` | `INDEX(workspace_id, project_id, created_at)`, `INDEX(status, created_at)` | 出力履歴。 |
| `data_imports` | `INDEX(workspace_id, project_id, created_at)`, `INDEX(status, created_at)` | 取込履歴。 |
| `external_connector_settings` | `INDEX(workspace_id, project_id, connector_type, status)` | 外部連携スタブ設定検索。 |
| `external_connector_runs` | `INDEX(connector_setting_id, created_at)`, `INDEX(workspace_id, project_id, status)` | スタブ接続テスト/実行履歴。 |
| `ai_sessions` | `INDEX(workspace_id, project_id, created_at)`, `INDEX(actor)` | AI履歴。 |
| `ai_messages` | `INDEX(session_id, created_at)` | 会話履歴。 |
| `audit_logs` | `INDEX(workspace_id, created_at)`, `INDEX(correlation_id)`, `INDEX(actor)`, `INDEX(resource_type, resource_id)` | 監査追跡。 |

## 8. ステータス定義

| 対象 | status値 | 備考 |
| --- | --- | --- |
| `workspaces` | active | 初期版は単一ワークスペースのみ。 |
| `projects`, `sites` | active / archived | 一覧の既定はactiveのみ。 |
| `api_credentials`, `notification_channels`, `external_connector_settings`, `alerts` | active / disabled | disabledは実行対象外。 |
| `api_contract_scopes` | active / archived | archivedは過去契約として保持。 |
| `jobs`, `data_exports`, `data_imports`, `external_connector_runs` | queued / running / waiting_external / succeeded / failed_retryable / failed_fatal / canceled | 非同期ジョブ共通。外部連携スタブは通常queued/running/succeeded/failed_fatalを使う。 |
| `notification_deliveries` | pending / retrying / succeeded / failed | 通知送信履歴。 |
| `article_briefs`, `reports`, `rewrite_tasks`, `cannibalization_candidates` | draft / active / archived / completed | 詳細な表示名は画面側で定義する。 |

## 9. 保持・削除方針

| データ | 既定保持期間 | 削除/無効化 |
| --- | --- | --- |
| 外部APIローデータ | 24か月 | Storage上のJSONを削除し、DBにはハッシュ、URI、ステータス、クレジットを残す。 |
| 加工データ/分析結果 | 24か月 | プロジェクトアーカイブ後も参照整合性を優先して保持する。 |
| レポート/成果物 | 36か月 | `status=archived`とし、ファイル本体は保持期間後に削除可能。 |
| 監査ログ | 36か月 | 原則削除しない。保持短縮時も相関ID、操作、日時は残す。 |
| APIキー/Webhook | 無期限 | 実値はDBに保存せず、参照キーを`disabled`へ変更する。 |

## 10. パーティション・容量対策

初期版では単一DB/通常テーブルで開始する。以下の条件を超える場合は月次パーティションを検討する。

| 対象 | 条件 | 方針 |
| --- | --- | --- |
| `external_api_calls` | 100万行超、または月次検索が遅い | `created_at`月次パーティション。 |
| `audit_logs` | 100万行超 | `created_at`月次パーティション。 |
| `keyword_metrics` | 500万行超 | `fetched_at`月次または`contract_scope_key`単位の分割を検討。 |
| `rank_results` | 500万行超 | `checked_at`月次パーティション。 |

## 11. 初期データ

| データ | 内容 |
| --- | --- |
| `workspaces` | 既定ワークスペースを1件作成。 |
| `api_contract_scopes` | ラッコキーワードAPIの契約確認結果を1件登録。初期想定はスタンダードプラン、APIキー最大5個、社内利用範囲。管理画面/APIでは管理しない。 |
| `locations`, `languages` | 起動後または管理画面からラッコキーワードAPIで同期する。 |

## 12. マイグレーション方針

- EF Core migrationsを使用し、DDL変更はPRでレビューする。
- `GIN(normalized_text gin_trgm_ops)`を使うため、初回マイグレーションで`CREATE EXTENSION IF NOT EXISTS pg_trgm`を適用する。
- 既存データを失う変更、カラム型の縮小、NOT NULL化、ユニーク制約追加は事前データ検査SQLを用意する。
- Hangfireの内部テーブルはアプリ業務テーブルと分けて管理し、業務監査の正本にしない。
- 本番相当環境では、migration dry-run、バックアップ取得、ロールバック手順確認を必須にする。
