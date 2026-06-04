# 基本設計書

**ラッコキーワードAPIを中核にしたSEOインテリジェンス基盤**

_SEO Intelligence Platform / SEOインテリジェンス基盤_

| 項目 | 内容 |
| --- | --- |
| 文書ID | BD-RKSEO-001 |
| 作成日 | 2026-05-30 |
| 対象システム | ラッコキーワードAPIを中核にしたSEO・コンテンツ・競合分析・順位監視プラットフォーム |
| 想定技術 | .NET 10 LTS / ASP.NET Core / Blazor Web App / PostgreSQL / Redis / Worker Service |
| 入力仕様 | rakko-keyword-api-docs.json（OpenAPI 3.0、API v1.4.1） |
| 関連設計 | requirements.md / api_design.md / db_design.md / screen_design.md / job_design.md / test_plan.md / external_api_design.md / operations_runbook.md / environment_setup.md / adr/ |
| 作成方針 | 上記で列挙した全ユースケースを、API連携・DB・非同期ジョブ・AI支援・外部連携を組み合わせて実現する。 |

> 注意: ラッコキーワードAPI単独で取得できないSearch Console実績値、GA4実セッション、CMS投稿などは、本設計では拡張コネクタとして定義する。

## 改訂履歴

| 版 | 日付 | 内容 | 作成/更新 |
| --- | --- | --- | --- |
| 1.0 | 2026-05-30 | 初版作成。要件定義書に対応した基本設計。 | ChatGPT |
| 1.1 | 2026-05-30 | API設計書・DB設計書を追加し、基本設計の正本範囲と詳細設計への参照を整理。 | ChatGPT |
| 1.2 | 2026-05-30 | 画面設計、ジョブ設計、テスト計画、外部API連携、運用、環境構築、ADRへの参照を追加。 | ChatGPT |

## 目次

- 1. 設計方針

- 2. 全体アーキテクチャ

- 3. 技術スタック

- 4. ソリューション/レイヤ構成

- 5. 主要コンポーネント設計

- 6. ドメインモデル/DB基本設計

- 7. 内部API基本設計

- 8. ラッコキーワードAPI連携設計

- 9. 非同期ジョブ設計

- 10. スコアリング/分析ロジック

- 11. 画面基本設計

- 12. セキュリティ設計

- 13. ログ/監視/運用設計

- 14. インフラ/デプロイ設計

- 15. テスト設計

- 16. 開発・移行方針

- 付録A. 外部APIマッピング

- 付録B. リクエストDTO制約

- 付録C. API別データ保存方針

- 付録D. 参照資料

## 1. 設計方針

- 外部APIへの依存をInfrastructure層に閉じ込め、Application層はユースケース単位のサービスとして実装する。

- 即時応答が必要な画面操作と、外部API連携を伴う重い処理を明確に分離し、非同期ジョブで実行する。

- APIレスポンスのローデータと正規化データを両方保持し、再解析・監査・将来拡張に備える。

- 初期版は開発者本人のみの単一利用者・単一ワークスペースとして設計し、APIキー保護、プロジェクトスコープ、監査ログを初期設計から組み込む。

- AI生成は「根拠データに基づく下書き」とし、プロンプト・参照データ・出力を履歴化する。

- .NET 10 LTSを主ターゲットに、ASP.NET Core、Worker Service、EF Core、Blazor Web Appで統一する。

### 1.1 設計文書の責務分担

| 文書 | 正本範囲 |
| --- | --- |
| requirements.md | スコープ、機能要件、画面要件、データ要件、非機能要件、受入基準。 |
| basic_design.md | アーキテクチャ、レイヤ構成、主要コンポーネント、ジョブ、分析ロジック、セキュリティ、運用、テスト。 |
| api_design.md | 内部API、レスポンス形式、エラー、非同期ジョブAPI、外部APIマッピング、入力制約。 |
| db_design.md | DBテーブル、カラム、リレーション、制約、インデックス、保持、マイグレーション。 |
| screen_design.md | 画面項目、操作、状態、バリデーション、API対応。 |
| job_design.md | ジョブ、キュー、状態遷移、リトライ、分割、通知。 |
| test_plan.md | テストレベル、受入基準トレース、障害系、性能/セキュリティ。 |
| external_api_design.md | 外部API認証、DTO生成、クレジット、キャッシュ、契約テスト。 |
| operations_runbook.md | 定常確認、障害対応、復元、スモークテスト。 |
| environment_setup.md | ローカル構成、環境変数、Secret、DB初期化、起動確認。 |
| adr/ | 技術選定と設計判断の記録。 |

同じ内容が複数文書に出る場合、要件判断はrequirements.md、API契約はapi_design.md、DB論理設計はdb_design.mdを優先する。

## 2. 全体アーキテクチャ

### 2.1 論理構成

```text
+----------------------------+
| Browser / Client           |
| Blazor Web App             |
+-------------+--------------+
              |
              v
+-------------+--------------+
| ASP.NET Core API / BFF     |  Single user auth, REST API, Validation
+------+------+--------------+
       |      |
       |      v
       |  +---+-------------------+
       |  | Application Services  |  Use cases, Scoring, Brief generation
       |  +---+-------------------+
       |      |
       v      v
+------+------+-+       +-------------------+       +----------------------+
| PostgreSQL     |<----->| Worker Service    |<----->| Rakko Keyword API    |
| Domain data    |       | Jobs, Polling      |       | X-API-Key            |
+------+---------+       +---------+---------+       +----------------------+
       |                           |
       v                           v
+------+---------+       +---------+---------+
| Redis / Locks  |       | Connectors         |
| Cache, locks   |       | AI, Discord, GSC... |
+----------------+       +-------------------+
```

### 2.2 物理構成候補

| 環境 | 推奨構成 |
| --- | --- |
| 開発 | Docker Compose: Web/API, Worker, PostgreSQL, Redis, LocalStack/MinIO。APIキーは開発用Key VaultまたはUser Secrets。 |
| ステージング | Azure Container AppsまたはApp Service、PostgreSQL Flexible Server、Azure Cache for Redis、Key Vault、Application Insights。 |
| 本番 | Azure Container Apps/Kubernetes/App Serviceのいずれか。API/Worker分離、Auto Scale、Private Endpoint、WAF、監視/バックアップ有効化。 |

## 3. 技術スタック

| 分類 | 技術 | 用途/理由 |
| --- | --- | --- |
| Runtime | .NET 10 LTS | 新規開発の標準。ランタイム、SDK、CI/CD、コンテナベースイメージを.NET 10で統一する。 |
| Backend | ASP.NET Core Web API / Minimal APIs | REST API、BFF、OpenAPI、認証/認可、バリデーション。 |
| Frontend | Blazor Web App | .NETで画面開発を統一。SSR/Interactive ServerまたはWASMを要件に応じて選択。 |
| Worker/Job Queue | .NET Worker Service + Hangfire + PostgreSQL storage | 外部API連携、ポーリング、リトライ、レポート生成、Discord通知、定期実行、ジョブ管理画面。 |
| DB | PostgreSQL + EF Core | 正規化データ、JSONBローデータ、全文検索補助、集計。 |
| Cache/Coordination | Redis | キャッシュ、分散ロック、レート制御、一時状態管理。ジョブの永続化とスケジューリングはHangfireのPostgreSQL storageで行う。 |
| Auth | 開発環境保護 + 必要時のみ単一管理者ログイン | 初期版は開発者本人のみ。外部公開時はASP.NET Core Identity等で単一管理者ログインを追加する。 |
| Observability | OpenTelemetry + Application Insights/Grafana | ログ、トレース、メトリクス、ジョブ監視。 |
| Reports | ClosedXML / QuestPDF | Phase 3のExcel/PDF/共有URL出力。Phase 1はCSV出力のみ。 |
| AI | IAiContentService abstraction | Azure OpenAI/OpenAI/社内LLMを差し替え可能にする。 |

## 4. ソリューション/レイヤ構成

```text
SeoIntelligence.sln
  src/
    SeoIntelligence.Web/             Blazor Web App, UI components, BFF client
    SeoIntelligence.Api/             ASP.NET Core API endpoints, Auth, OpenAPI
    SeoIntelligence.Application/     Use cases, DTOs, validators, scoring services
    SeoIntelligence.Domain/          Entities, value objects, domain services
    SeoIntelligence.Infrastructure/  EF Core, Rakko client, AI/Discord/GSC adapters
    SeoIntelligence.Worker/          Background jobs, schedulers, polling
    SeoIntelligence.Contracts/       Shared contracts, API response models
  tests/
    UnitTests/ IntegrationTests/ ContractTests/ E2ETests/
  tools/
    OpenApiCodegen/ Migrations/ SeedData/
```

### 4.1 依存方向

- Domainは他プロジェクトへ依存しない。

- ApplicationはDomainとContractsへ依存し、Infrastructureへ依存しない。外部APIはインターフェースで抽象化する。

- InfrastructureはApplicationのインターフェースを実装する。EF Core、HTTPクライアント、AI、通知をここに配置する。

- Api/Web/WorkerはApplicationを呼び出す。Workerはジョブ起点でユースケースを実行する。

- MVPの永続化アクセスはInfrastructure層の`SeoIntelligenceDbContext`と`IDbContextFactory<SeoIntelligenceDbContext>`を基本とし、汎用Repositoryは導入しない。Application層にはEF Core型を公開しない。

## 5. 主要コンポーネント設計

| コンポーネント | 責務 |
| --- | --- |
| ProjectContextService | ワークスペース/プロジェクトコンテキストを全ユースケースに注入する。 |
| DashboardService | Phase 1のキーワード探索、一括検索ボリューム、機会スコア、APIクレジットを集計する。Phase 2では競合、獲得語/ページ、コンテンツ分析、記事ブリーフ、順位、順位アラート指標を追加し、リライト/カニバリ/レポート/AI指標はPhase 3で追加する。 |
| AdministrationService | workspaces、projects、sites、api_credentials、notification_channels、外部連携スタブ設定の管理操作を実行する。 |
| MasterDataService | ラッコキーワードAPIの地域/言語マスタを同期し、プロジェクト既定値と調査条件の入力候補を提供する。 |
| KeywordDiscoveryService | サジェスト、関連語、LSI/PAA、FAQ、同時ランクインを統合する。 |
| SearchVolumeService | 一括検索ボリューム登録、status監視、results保存、トレンド集計。 |
| CompetitorAnalysisService | 競合抽出、獲得キーワード、獲得ページ、ギャップ分析。 |
| ContentAnalysisService | 集客コンテンツ、見出し、共起語を取得し、ブリーフ材料を生成。 |
| RankTrackingService | 順位チェック登録、結果保存、順位分布、アラート判定。 |
| ScoringService | 機会スコア、リライト優先度、広告/SEO判断、競合ギャップ優先度。 |
| BriefGenerationService | クラスタ/FAQ/見出し/共起語/競合URLから記事構成書を生成。 |
| AiAssistantService | 自然言語からツール呼び出し計画を作り、Application Serviceを実行する。 |
| ReportService | 月次レポート、競合レポート、順位レポート、記事ブリーフ出力。 |
| DataTransferService | Phase 1のCSVエクスポート、Phase 3のCSV/Excelエクスポート・インポート、検証エラー管理、出力/取込監査を実行する。 |
| ExternalApiUsageService | external_api_callsを正本としてクレジット消費を集計し、APIキー状態、レート制限、429/402/403の扱いを制御する。日次/月次予算や予算超過による事前停止は管理しない。 |
| NotificationService | Discord Webhook通知。Webhook設定、テンプレート、送信履歴をnotification_channels/notification_deliveriesで管理する。 |

## 6. ドメインモデル/DB基本設計

DBはPostgreSQLを想定し、業務データはリレーショナルに正規化し、外部APIレスポンスのスナップショットや分析条件はJSONBで保持する。初期版は単一ワークスペースだが、将来拡張とプロジェクトスコープの明確化のため、グローバルマスタを除く主要テーブルにはworkspace_idまたはproject_idを持たせる。

DBテーブル、型、FK、インデックス、保持期間、マイグレーション方針の正本は`docs/db_design.md`とする。本章はアーキテクチャ判断に必要な主要テーブル群と設計意図を示す。

グローバルマスタは、keywordsや地域/言語マスタなどワークスペース非依存で重複排除する参照データに限定する。keyword_metrics/keyword_monthly_volumesはキーワード、地域、言語、契約スコープ、取得日時単位の共有キャッシュとし、プロジェクト別の取得条件・出自・分析結果はjobs、job_external_requests、external_api_calls、search_volume_results等の業務テーブルで追跡する。

検索指標キャッシュの共有は内部最適化として扱い、契約スコープが一致する場合だけ再利用する。契約スコープはapi_contract_scopesを正本とし、scope_keyはプロバイダー、契約プラン、データ利用範囲、適用期間から生成する。外部APIを実行した場合はexternal_api_callsにapi_credential_id、api_contract_scope_id、contract_scope_key、consumed_credit、job_id、correlation_id、actorを保存し、キャッシュ再利用時は業務テーブル側でcache_hitとして記録する。画面/APIには必ずワークスペース/プロジェクトスコープの取得条件と成果物として返す。

| テーブル | 用途 | 主なカラム |
| --- | --- | --- |
| workspaces | 単一ワークスペース設定 | id, name, default_location, default_language, retention_settings_json, notification_defaults_json, status, created_at, updated_at |
| projects | SEOプロジェクト | id, workspace_id, name, default_location, default_language, kpi_json, memo, status, created_at, updated_at, archived_at |
| sites | 対象サイト | id, project_id, domain, canonical_url, type, memo, status, created_at, updated_at, archived_at |
| competitor_sites | 競合サイト | id, project_id, domain, source, duplicate_rate, estimated_traffic |
| api_credentials | 外部API接続 | id, workspace_id, provider, key_ref, status, created_at, updated_at, disabled_at |
| api_contract_scopes | API契約スコープ履歴 | id, workspace_id, provider, plan_name, api_key_limit, data_usage_scope, confirmed_at, confirmed_by, effective_from, effective_to, scope_key, status, created_at |
| notification_channels | Discord Webhook通知設定 | id, workspace_id, project_id, channel_type, name, webhook_secret_ref, event_types_json, status, created_at, updated_at, disabled_at |
| notification_deliveries | 通知送信履歴 | id, workspace_id, project_id, channel_id, job_id, resource_type, resource_id, event_type, payload_hash, status, error_message, retry_count, next_retry_at, sent_at, delivered_at, correlation_id, created_at |
| external_connector_settings | 外部連携スタブ設定 | id, workspace_id, project_id, connector_type, name, auth_ref, settings_json, status, created_at, updated_at, disabled_at |
| external_connector_runs | 外部連携スタブ実行履歴 | id, connector_setting_id, workspace_id, project_id, run_type, status, request_json, result_summary_json, error_json, started_at, completed_at, created_at |
| locations | 地域マスタ | id, provider, location_code, location_name, country_code, status, synced_at |
| languages | 言語マスタ | id, provider, language_code, language_name, status, synced_at |
| external_api_calls | API呼び出し監査 | id, workspace_id, project_id, job_id, api_credential_id, api_contract_scope_id, provider, endpoint, request_hash, request_uri, response_hash, response_uri, contract_scope_key, cache_hit, status_code, consumed_credit, duration_ms, error_code, correlation_id, actor, retained_until, created_at |
| jobs | 非同期ジョブ共通 | id, workspace_id, project_id, job_type, status, progress, retry_count, next_run_at, result_resource_type, result_resource_id, error_json, idempotency_key, request_hash, requested_by, created_at, updated_at, completed_at |
| job_external_requests | 外部requestId追跡 | id, job_id, endpoint, external_request_id, sequence_no, status, retry_count, source_call_id, consumed_credit, error_json, created_at, updated_at, completed_at |
| keyword_seeds | シードキーワード | id, project_id, seed, source, memo |
| keywords | キーワード正規化マスタ | id, normalized_text, language, text_hash |
| keyword_metrics | 指標履歴/最新指標 | keyword_id, location, language, contract_scope_key, source_call_id, search_volume, seo_difficulty, cpc, competition, first_seen_range, fetched_at |
| keyword_monthly_volumes | 月別検索数 | keyword_id, location, language, contract_scope_key, source_call_id, year_month, search_volume, fetched_at |
| project_keyword_scores | プロジェクト別機会スコア | project_id, keyword_id, location, language, source_call_id, opportunity_score, score_components_json, scored_at |
| keyword_suggestions | サジェスト結果 | seed_id, keyword_id, engine, suggest_class, engine_count, first_seen_range |
| related_keywords | 関連語結果 | seed_id, keyword_id, match_type, metrics_snapshot_json |
| questions | FAQ/PAA質問 | id, project_id, seed_keyword_id, question_text, source, importance |
| lsi_paa_items | LSI/PAA | id, seed_keyword_id, type, keyword_id, question_text, importance |
| ranking_keywords | 同時ランクイン語 | seed_keyword_id, keyword_id, word_count, relevance, metrics_snapshot_json |
| search_volume_jobs | 検索ボリュームジョブ | job_id, location, language, aggregation_months, request_options_json, status_json |
| search_volume_results | 検索ボリューム結果 | job_id, keyword_id, data_source, source_call_id, cache_hit, metrics_snapshot_json, trends_json |
| influx_keyword_results | 獲得キーワード結果 | project_id, target, keyword_id, rank, ranked_url, estimated_traffic, metrics_snapshot_json |
| influx_page_results | 獲得ページ結果 | project_id, target, page_url, title, keyword_count, estimated_traffic, traffic_value, top_keyword_id |
| competitive_results | 競合抽出結果 | project_id, site_domain, estimated_traffic, traffic_value, keyword_count, duplicate_rate, unique_counts_json |
| content_search_results | 集客コンテンツ結果 | project_id, keyword_id, url, domain, title, description, estimated_traffic, traffic_value, top_keyword_id |
| serp_headline_pages | 見出し対象ページ | project_id, keyword_id, rank, url, title, description, headline_count, word_count |
| serp_headlines | 見出し明細 | page_id, level, text, order_no |
| co_occurrence_words | 共起語 | project_id, keyword_id, word, occurrence_counts_json, site_counts_json |
| co_occurrence_page_details | 共起語URL別詳細 | co_word_id, rank, url, title, count, count_in_headline, count_in_title |
| topic_clusters | トピッククラスター | id, project_id, name, parent_id, representative_keyword_id, score |
| cluster_keywords | クラスタ所属語 | cluster_id, keyword_id, role, opportunity_score, intent_label |
| article_briefs | 記事ブリーフ | id, project_id, cluster_id, title, target_keyword_id, current_version, content_json, review_status, status |
| rewrite_tasks | リライトタスク | id, project_id, target_url, priority_score, reason_json, status, assignee_actor |
| cannibalization_candidates | カニバリ候補 | id, project_id, keyword_id, primary_url, competing_urls_json, severity_score, evidence_json, recommendation_json, status, detected_at |
| rank_check_jobs | 順位チェックジョブ | job_id, depth, match_type, with_metrics, request_options_json, status_json |
| rank_check_targets | 順位チェック対象 | job_id, target, target_type |
| rank_results | 順位結果 | job_id, project_id, keyword_id, target, position, ranked_url, estimated_traffic, metrics_snapshot_json, source_call_id, contract_scope_key, checked_at |
| alerts | アラート定義 | id, project_id, alert_type, condition_json, notification_channel_id, status, last_triggered_at |
| alert_events | アラート発火履歴 | id, alert_id, project_id, job_id, keyword_id, event_type, previous_value_json, current_value_json, evidence_json, notification_delivery_id, triggered_at, resolved_at |
| reports | レポート | id, project_id, report_type, period, format, current_version, file_uri, share_token_hash, share_expires_at, status, generated_by |
| artifact_versions | 成果物バージョン | id, workspace_id, project_id, artifact_type, artifact_id, version_no, content_hash, content_uri, content_json, created_by, review_status, change_summary, created_at |
| data_exports | データ出力履歴 | id, workspace_id, project_id, export_type, format, filter_json, file_uri, status, requested_by, created_at, completed_at |
| data_imports | データ取込履歴 | id, workspace_id, project_id, import_type, format, source_file_uri, status, validation_errors_json, requested_by, created_at, completed_at |
| ai_sessions / ai_messages | AIアシスタント履歴 | id, workspace_id, project_id, actor, session_id, message_role, prompt, response, tool_calls_json, reference_data_json, redaction_status, review_status, token_usage, created_at |
| audit_logs | 操作監査 | id, workspace_id, actor, action, resource_type, resource_id, before_after_json, correlation_id, ip_address, user_agent, created_at |

### 6.1 主なインデックス方針

| テーブル | インデックス | 目的 |
| --- | --- | --- |
| keywords | UNIQUE(language, text_hash), GIN(normalized_text gin_trgm_ops) | 重複排除、候補語検索 |
| locations | UNIQUE(provider, location_code), INDEX(status) | 地域マスタ同期、入力候補 |
| languages | UNIQUE(provider, language_code), INDEX(status) | 言語マスタ同期、入力候補 |
| workspaces | UNIQUE(name), INDEX(status) | 単一ワークスペース設定、将来拡張時の識別 |
| api_contract_scopes | UNIQUE(scope_key), INDEX(workspace_id, provider, status), INDEX(effective_from, effective_to) | 契約スコープの正本、キャッシュ再利用判定 |
| keyword_metrics | INDEX(keyword_id, location, language, contract_scope_key, fetched_at desc), INDEX(source_call_id) | 最新指標・履歴参照、契約スコープ別キャッシュ制御 |
| keyword_monthly_volumes | INDEX(keyword_id, location, language, contract_scope_key, year_month, fetched_at desc), INDEX(source_call_id) | 月別推移、取得回ごとの履歴参照、契約スコープ別キャッシュ制御 |
| project_keyword_scores | UNIQUE(project_id, keyword_id, location, language), INDEX(project_id, opportunity_score desc), INDEX(source_call_id) | ダッシュボード、キーワード探索結果、機会スコア上位表示 |
| rank_results | INDEX(project_id, keyword_id, target, checked_at desc), INDEX(position), INDEX(source_call_id), INDEX(contract_scope_key) | 順位履歴、順位帯抽出、外部API出自追跡 |
| cannibalization_candidates | INDEX(project_id, keyword_id, detected_at desc), INDEX(project_id, status, severity_score desc) | カニバリ候補一覧、優先度順表示 |
| influx_keyword_results | INDEX(project_id, target), INDEX(keyword_id), INDEX(rank) | 競合ギャップ、順位条件 |
| content_search_results | INDEX(project_id, keyword_id), INDEX(domain), GIN(title/description) | コンテンツ検索・ベンチマーク |
| alert_events | INDEX(project_id, triggered_at desc), INDEX(alert_id, triggered_at desc), INDEX(notification_delivery_id) | アラート発火履歴、定義別履歴、通知結果追跡 |
| jobs | INDEX(status, next_run_at), INDEX(workspace_id, project_id, created_at), UNIQUE(workspace_id, COALESCE(project_id, zero_uuid), job_type, idempotency_key) WHERE idempotency_key IS NOT NULL | Workerポーリング・再実行、ジョブ履歴、冪等登録 |
| job_external_requests | INDEX(job_id, sequence_no), INDEX(external_request_id), INDEX(status, updated_at) | 分割ジョブの外部requestId追跡、ポーリング |
| external_api_calls | INDEX(provider, endpoint, created_at), INDEX(status_code), INDEX(contract_scope_key), INDEX(response_hash) | API監査・エラー分析、契約スコープ確認、ローデータ追跡 |
| notification_channels | INDEX(workspace_id, project_id, status), INDEX(channel_type) | Discord通知先解決 |
| notification_deliveries | INDEX(workspace_id, project_id, created_at), INDEX(status, next_retry_at), INDEX(job_id), INDEX(resource_type, resource_id), INDEX(correlation_id) | 送信履歴、再送制御、送信元追跡 |
| external_connector_settings | INDEX(workspace_id, project_id, connector_type, status) | Phase 3外部連携スタブ設定の検索 |
| external_connector_runs | INDEX(connector_setting_id, created_at), INDEX(workspace_id, project_id, status) | スタブ接続テスト/実行履歴 |
| reports | INDEX(project_id, report_type, period, format), INDEX(share_expires_at) | レポート一覧、形式別検索、共有URL期限管理 |
| artifact_versions | UNIQUE(artifact_type, artifact_id, version_no), INDEX(workspace_id, project_id, created_at) | 成果物の版管理、編集履歴 |
| data_exports | INDEX(workspace_id, project_id, created_at), INDEX(status, created_at) | 出力履歴、完了待ち管理 |
| data_imports | INDEX(workspace_id, project_id, created_at), INDEX(status, created_at) | 取込履歴、エラー確認 |
| ai_sessions / ai_messages | INDEX(workspace_id, project_id, created_at), INDEX(actor), INDEX(session_id) | AI履歴、実行者監査、セッション追跡 |
| audit_logs | INDEX(workspace_id, created_at), INDEX(correlation_id), INDEX(actor) | 監査追跡、相関ID検索 |

初期版ではusers、roles、user_rolesを持たない。操作主体は固定値developerとしてjobs、external_api_calls、audit_logsに保存する。複数ユーザー化が必要になった時点で、Phase 4拡張としてユーザー/ロールテーブルとRBACを追加する。

api_contract_scopesはラッコキーワードAPIの契約プラン、APIキー上限、データ利用範囲、確認日、確認者、適用期間を保持する。管理画面/APIでは管理せず、初期データと運用手順で登録する。契約内容を変更した場合はSeedDataまたはマイグレーション相当の保守手順で既存行をarchivedにして新しいscope_keyを発行し、過去データの再利用可否を後から追跡できるようにする。

アプリ内ではクレジット予算を管理しない。consumedCreditはexternal_api_callsに保存し、ダッシュボードと管理画面では全体/プロジェクト/APIキー/ジョブ別の消費量として集計する。日次/月次の区切りはAsia/Tokyo基準で表示するが、予算上限や承認制による実行停止は行わない。

external_api_callsのrequest_uri/response_uriはStorage上の圧縮JSONを指し、request_hash/response_hashで改ざん検知と重複確認を行う。retained_untilはワークスペースのデータ保持期間から算出し、ローデータ削除後も監査に必要なハッシュ、ステータス、消費クレジット、契約スコープ、job_id、correlation_id、actorは保持する。

job_external_requestsは、1つの内部ジョブが複数の外部requestIdへ分割される場合の正本とする。search_volume_jobs/rank_check_jobsは調査条件を保持し、各分割単位のポーリング状態、リトライ、消費クレジット、source_call_idはjob_external_requestsとexternal_api_callsで追跡する。

artifact_versionsは記事ブリーフ、レポート、AI生成結果など成果物の共通版管理テーブルとする。各成果物テーブルのcurrent_versionは最新表示用であり、編集履歴、レビュー状態、参照データ、差分説明はartifact_versionsまたはai_sessions / ai_messagesで追跡する。初期版は単一利用者のため、rewrite_tasks.assignee_actorなどの担当者欄は固定値developerを保存し、usersテーブルへの外部キーは持たせない。

### 6.2 削除/無効化方針

業務データは原則として物理削除しない。プロジェクト、サイトはstatus=archivedとしてアーカイブし、API認証情報、通知設定はstatus=disabledとして無効化する。DELETE系内部APIはこの状態更新を行い、参照系APIは既定でactiveのみ返す。監査ログ、外部API呼び出し履歴、分析結果、レポートは参照整合性と監査性を優先して保持する。

### 6.3 ステータス定義

| 対象 | status値 | 備考 |
| --- | --- | --- |
| workspaces | active | 初期版は単一ワークスペースのみ。停止や複数化はPhase 4以降で扱う。 |
| projects / sites | active / archived | archivedは一覧の既定結果から除外し、復元APIでactiveへ戻す。 |
| api_credentials / notification_channels / external_connector_settings / alerts | active / disabled | disabledは外部API実行、Discord Webhook送信、外部連携スタブ実行、アラート判定の対象外とし、再有効化APIでactiveへ戻す。 |
| api_contract_scopes | active / archived | archivedは過去契約スコープとして保持し、新規API実行やキャッシュ再利用判定の既定対象から除外する。 |
| notification_deliveries | pending / retrying / succeeded / failed | retryingは再送待ち、failedは最大再送超過または致命的エラー。 |
| data_exports / data_imports | queued / running / waiting_external / succeeded / failed_retryable / failed_fatal / canceled | jobs.statusと同じ語彙で管理し、ファイル出力/取込の履歴画面では必要に応じて表示名を簡略化する。 |
| jobs | queued / running / waiting_external / succeeded / failed_retryable / failed_fatal / canceled | 9.1のジョブ状態遷移に従う。 |

## 7. 内部API基本設計

内部APIのURL、共通レスポンス、エラー形式、入力制約、外部APIマッピングの正本は`docs/api_design.md`とする。本章は画面・Application Service・Workerの接続点を把握するための概要として維持する。

| Method | Path | 概要 |
| --- | --- | --- |
| GET | /api/admin/workspace | 単一ワークスペース設定取得 |
| PUT | /api/admin/workspace | 単一ワークスペース設定更新 |
| GET | /api/projects | プロジェクト一覧 |
| POST | /api/projects | プロジェクト作成 |
| GET | /api/projects/{projectId} | プロジェクト詳細取得 |
| PUT | /api/projects/{projectId} | プロジェクト更新 |
| DELETE | /api/projects/{projectId} | プロジェクトアーカイブ（status=archived） |
| POST | /api/projects/{projectId}/restore | プロジェクト復元（status=active） |
| GET | /api/projects/{projectId}/sites | サイト一覧 |
| POST | /api/projects/{projectId}/sites | サイト作成 |
| GET | /api/projects/{projectId}/sites/{siteId} | サイト詳細取得 |
| PUT | /api/projects/{projectId}/sites/{siteId} | サイト更新 |
| DELETE | /api/projects/{projectId}/sites/{siteId} | サイトアーカイブ（status=archived） |
| POST | /api/projects/{projectId}/sites/{siteId}/restore | サイト復元（status=active） |
| GET | /api/admin/api-credentials | API認証情報一覧。キー値は返却しない |
| POST | /api/admin/api-credentials | API認証情報作成。secretValue指定時はKey Vault等へ保存し、keyRef指定時は既存Secret参照としてDBにはkey_refのみ保存 |
| GET | /api/admin/api-credentials/{credentialId} | API認証情報詳細取得。キー値は返却しない |
| PUT | /api/admin/api-credentials/{credentialId} | API認証情報更新。秘密値変更はrotateで行う |
| DELETE | /api/admin/api-credentials/{credentialId} | API認証情報無効化（status=disabled） |
| POST | /api/admin/api-credentials/{credentialId}/enable | API認証情報再有効化（status=active） |
| POST | /api/admin/api-credentials/{credentialId}/rotate | newSecretValueまたはnewKeyRefによるAPI認証情報ローテーション |
| GET | /api/admin/notification-channels | Discord通知設定一覧 |
| POST | /api/admin/notification-channels | Discord Webhook通知設定作成 |
| GET | /api/admin/notification-channels/{channelId} | Discord Webhook通知設定詳細取得 |
| PUT | /api/admin/notification-channels/{channelId} | Discord Webhook通知設定更新 |
| DELETE | /api/admin/notification-channels/{channelId} | Discord Webhook通知設定無効化（status=disabled） |
| POST | /api/admin/notification-channels/{channelId}/enable | Discord Webhook通知設定再有効化（status=active） |
| POST | /api/admin/notification-channels/{channelId}/test | Discord Webhookテスト通知送信 |
| GET | /api/admin/notification-deliveries | Discord Webhook通知送信履歴一覧 |
| GET | /api/admin/notification-deliveries/{deliveryId} | Discord Webhook通知送信履歴詳細取得 |
| POST | /api/admin/notification-deliveries/{deliveryId}/retry | Discord Webhook通知の手動再送 |
| GET | /api/jobs | 管理/監査向けジョブキュー・履歴一覧。status、job_type、project_id、期間で検索し、滞留・失敗・次回実行時刻を返す |
| GET | /api/jobs/{jobId} | 管理/監査向け非同期ジョブ共通状態取得。job_type、status、progress、result_resource、error、retry_countを返す。プロジェクト画面ではproject-scopedな個別ジョブAPIを優先する |
| POST | /api/jobs/{jobId}/cancel | 実行前または待機中ジョブのキャンセル |
| POST | /api/jobs/{jobId}/retry | retry可能な失敗ジョブの手動再実行 |
| GET | /api/projects/{projectId}/dashboard | 段階拡張ダッシュボード指標取得。Phase 1はキーワード探索、一括検索ボリューム、機会スコア、APIクレジットを返し、Phase 2で競合、獲得語/ページ、コンテンツ分析、記事ブリーフ、順位、順位アラート指標を追加する |
| GET | /api/master-data/locations | 地域マスタ一覧取得 |
| GET | /api/master-data/languages | 言語マスタ一覧取得 |
| POST | /api/admin/master-data/sync | 地域/言語マスタ同期ジョブ登録 |
| POST | /api/projects/{projectId}/keyword-discovery/suggest | サジェスト/関連語/LSI/PAA/FAQ統合調査。軽量条件のみ同期取得し、それ以外はジョブ登録 |
| POST | /api/projects/{projectId}/search-volume/jobs | 一括検索ボリューム調査ジョブ登録 |
| GET | /api/projects/{projectId}/search-volume/jobs/{jobId} | ジョブ状態取得 |
| GET | /api/projects/{projectId}/search-volume/jobs/{jobId}/results | 検索ボリューム結果取得 |
| GET | /api/projects/{projectId}/clusters | トピッククラスター一覧取得 |
| GET | /api/projects/{projectId}/clusters/{clusterId} | トピッククラスター詳細取得 |
| POST | /api/projects/{projectId}/clusters/generate | トピッククラスター生成 |
| GET | /api/projects/{projectId}/competitors | 競合分析結果一覧取得 |
| POST | /api/projects/{projectId}/competitors/analyze | 競合抽出・獲得語/ページ取得ジョブ登録 |
| GET | /api/projects/{projectId}/influx-keywords | 獲得キーワード結果一覧取得 |
| GET | /api/projects/{projectId}/influx-pages | 獲得ページ結果一覧取得 |
| GET | /api/projects/{projectId}/content-analyses | コンテンツ分析結果一覧取得 |
| POST | /api/projects/{projectId}/content/analyze | コンテンツ検索・見出し・共起語分析ジョブ登録 |
| GET | /api/projects/{projectId}/briefs | 記事ブリーフ一覧取得 |
| POST | /api/projects/{projectId}/briefs/generate | 記事ブリーフ生成 |
| GET | /api/projects/{projectId}/briefs/{briefId} | 記事ブリーフ詳細取得 |
| PUT | /api/projects/{projectId}/briefs/{briefId} | 記事ブリーフ本文、レビュー状態、ステータス更新 |
| GET | /api/projects/{projectId}/briefs/{briefId}/versions | 記事ブリーフ版履歴取得 |
| POST | /api/projects/{projectId}/briefs/{briefId}/export | 記事ブリーフのMarkdown/CSV等の出力ジョブ登録 |
| POST | /api/projects/{projectId}/rank-check/jobs | 順位チェック登録 |
| GET | /api/projects/{projectId}/rank-check/jobs/{jobId}/results | 順位結果取得 |
| GET | /api/projects/{projectId}/rank-results | 順位履歴・順位分布取得 |
| GET | /api/projects/{projectId}/alerts | アラート定義一覧取得 |
| GET | /api/projects/{projectId}/alert-events | アラート発火履歴一覧取得。alert_id、event_type、期間で検索 |
| POST | /api/projects/{projectId}/alerts | アラート条件作成 |
| PUT | /api/projects/{projectId}/alerts/{alertId} | アラート条件更新 |
| DELETE | /api/projects/{projectId}/alerts/{alertId} | アラート条件無効化（status=disabled） |
| POST | /api/projects/{projectId}/alerts/{alertId}/enable | アラート条件再有効化（status=active） |
| GET | /api/projects/{projectId}/rewrite/tasks | リライト候補一覧 |
| GET | /api/projects/{projectId}/rewrite/tasks/{taskId} | リライトタスク詳細取得 |
| PUT | /api/projects/{projectId}/rewrite/tasks/{taskId} | リライトタスクのステータス、優先度、担当者（developer固定）、メモを更新 |
| GET | /api/projects/{projectId}/cannibalization/candidates | カニバリ候補一覧。対象キーワード、競合URL、深刻度、推奨対応、根拠データを返す |
| POST | /api/projects/{projectId}/cannibalization/refresh | カニバリ候補の再計算ジョブ登録 |
| POST | /api/projects/{projectId}/reports | Phase 3: レポート生成 |
| GET | /api/projects/{projectId}/reports/{reportId} | Phase 3: レポート詳細取得 |
| GET | /api/projects/{projectId}/reports/{reportId}/download | Phase 3: レポートファイルの短時間ダウンロードURL発行。発行操作をaudit_logsへ記録 |
| POST | /api/projects/{projectId}/reports/{reportId}/share | Phase 3: 共有URL発行。share_token_hashとshare_expires_atを更新 |
| DELETE | /api/projects/{projectId}/reports/{reportId}/share | Phase 3: 共有URL失効。share_token_hashを無効化し監査ログを記録 |
| GET | /api/report-shares/{token} | Phase 3: 共有URLによるレポート公開取得。期限切れ・失効済みは404または410 |
| POST | /api/projects/{projectId}/exports/csv | Phase 1向けCSVエクスポートジョブ登録。対象データとフィルタ条件を指定 |
| POST | /api/projects/{projectId}/exports | Phase 3: CSV/Excelエクスポートジョブ登録。format、export_type、フィルタ条件を指定 |
| GET | /api/projects/{projectId}/exports/{exportId} | Phase 1以降: エクスポート状態/ファイル情報取得。MVPはCSVのみ |
| GET | /api/projects/{projectId}/exports/{exportId}/download | Phase 1以降: CSV/Excelファイルダウンロード。MVPはCSVのみ |
| POST | /api/projects/{projectId}/imports/upload-url | Phase 3: CSV/Excelインポート元ファイルをStorageへ直接アップロードするための期限付きURLを発行 |
| POST | /api/projects/{projectId}/imports | Phase 3: CSV/Excelインポートジョブ登録。import_type、format、source_file_uri、検証モードを指定 |
| GET | /api/projects/{projectId}/imports/{importId} | Phase 3: インポート状態/検証結果取得 |
| GET | /api/projects/{projectId}/imports/{importId}/errors | Phase 3: インポート検証エラー一覧取得 |
| GET | /api/projects/{projectId}/connectors | Phase 3: GSC/GA4/CMS/BI等の外部連携スタブ設定一覧 |
| POST | /api/projects/{projectId}/connectors | Phase 3: 外部連携スタブ設定作成。Secret/OAuth実値は保存しない |
| PUT | /api/projects/{projectId}/connectors/{connectorId} | Phase 3: 外部連携スタブ設定更新 |
| DELETE | /api/projects/{projectId}/connectors/{connectorId} | Phase 3: 外部連携スタブ設定無効化（status=disabled） |
| POST | /api/projects/{projectId}/connectors/{connectorId}/test | Phase 3: 実データ取得を伴わない接続テスト/スタブ実行 |
| GET | /api/projects/{projectId}/connectors/{connectorId}/runs | Phase 3: 外部連携スタブ実行履歴取得 |
| POST | /api/projects/{projectId}/ai/chat | AIアシスタント実行 |
| GET | /api/admin/external-api-calls | API呼び出し・クレジット監査 |
| GET | /api/admin/audit-logs | 監査ログ一覧。期間、actor、resource_type、correlation_idで検索 |
| GET | /api/admin/audit-logs/{auditLogId} | 監査ログ詳細取得 |

### 7.1 API共通仕様

```text
Response envelope:
{
  "requestId": "internal-correlation-id",
  "result": true,
  "data": { ... },
  "errors": [],
  "meta": {
    "jobId": "...",
    "externalRequestId": "...",
    "consumedCredit": 1.0
  }
}
```

- すべてのAPIでCorrelation IDを発行し、ログ・ジョブ・外部API呼び出しに引き継ぐ。

- プロジェクト配下の業務APIは`/api/projects/{projectId}/...`を正本とし、request body内のprojectIdは受け付けない。対象リソースがURL上のprojectIdに属することをDBで検証し、不一致は404または403として扱う。

- バリデーションエラーは400、認可エラーは403、未認証は401、外部API都合の一時失敗は503またはジョブ失敗として返す。

- 重い処理は202 Accepted + jobId/statusUrlを返し、フロントエンドはジョブ状態をポーリングまたはSignalRで購読する。

- キーワード探索の同期取得は、1シード、選択API数5以下、各APIのlimitが100以下、推定消費クレジットが5以下、かつキャッシュ利用可能な場合に限定する。それ以外は202 Accepted + jobId/statusUrlでジョブ化する。

- 競合分析、コンテンツ分析、記事ブリーフ生成、レポート生成、CSV/Excelエクスポート、CSV/Excelインポートなど個別の進捗取得APIを持たない非同期処理は、共通の`GET /api/jobs/{jobId}`で状態を取得する。管理画面のジョブキューは`GET /api/jobs`で一覧・検索する。完了後は`result_resource`に作成されたレポート、エクスポート、インポート、ブリーフ等の参照を返す。

- DELETE系APIは物理削除ではなく、対象に応じてstatus=archivedまたはstatus=disabledへ更新する。restore/enable系APIはstatus=activeへ戻す。既定の一覧APIはactiveのみ返し、管理画面では状態フィルタでarchived/disabledを表示できる。

### 7.2 非同期ジョブ共通レスポンス

```json
{
  "requestId": "internal-correlation-id",
  "result": true,
  "data": {
    "jobId": "internal-job-id",
    "jobType": "ContentAnalyzeJob",
    "status": "running",
    "progress": 45,
    "statusUrl": "/api/jobs/internal-job-id",
    "externalRequestId": null,
    "resultResource": null,
    "retryCount": 1,
    "nextRunAt": "2026-05-30T12:00:00Z",
    "error": null
  },
  "errors": [],
  "meta": {
    "jobId": "internal-job-id",
    "externalRequestId": null,
    "consumedCredit": 0
  }
}
```

ジョブ登録APIの202 Accepted、`GET /api/jobs/{jobId}`、個別ジョブ状態取得APIは共通レスポンスエンベロープで`data`にジョブレスポンスを格納する。`resultResource`は完了後に`resourceType`、`resourceId`、`downloadUrl`等を持つ。ダウンロードURLはプロジェクトスコープ確認後に短時間だけ有効なURLとして発行し、発行・失効・ダウンロードはaudit_logsへ記録する。

### 7.3 ファイル入出力方式

CSV/ExcelインポートはPhase 3対象とし、Phase 1/MVPではAPIを公開しない。APIサーバが大容量ファイルを直接保持しない方式とする。クライアントは`POST /api/projects/{projectId}/imports/upload-url`で期限付きアップロードURLを取得し、Storageへファイルをアップロードした後、`POST /api/projects/{projectId}/imports`へ`source_file_uri`、`format`、`import_type`、`validation_mode`を渡して取込ジョブを登録する。WorkerはStorageからファイルを読み取り、検証結果をdata_imports.validation_errors_jsonへ保存する。

MVPの一括検索ボリューム画面で扱うCSVはインポート機能ではない。Blazor UIがブラウザ内でCSVをパースし、空行除外・重複除外・上限検証を行ったうえで、`POST /api/projects/{projectId}/search-volume/jobs`へ`keywords` JSON配列として送信する。APIサーバへCSVファイル本体はアップロードしない。

Phase 1のエクスポートは`POST /api/projects/{projectId}/exports/csv`によるCSV出力に限定する。Phase 3のCSV/ExcelエクスポートはDataExportJobがStorageへファイルを生成し、data_exports.file_uriに保存する。ダウンロード時はスコープ確認後に短時間だけ有効なURLを発行し、発行・ダウンロード操作をaudit_logsへ記録する。

## 8. ラッコキーワードAPI連携設計

### 8.1 クライアント構成

```text
public interface IRakkoKeywordClient
{
    Task<SuggestKeywordsResponse> SuggestKeywordsAsync(SuggestKeywordsRequest request, CancellationToken ct);
    Task<RelatedKeywordsResponse> RelatedKeywordsAsync(RelatedKeywordsRequest request, CancellationToken ct);
    Task<OtherKeywordsResponse> OtherKeywordsAsync(OtherKeywordsRequest request, CancellationToken ct);
    Task<QuestionSearchResponse> QuestionSearchAsync(QuestionSearchRequest request, CancellationToken ct);
    Task<RankingKeywordsResponse> RankingKeywordsAsync(RankingKeywordsRequest request, CancellationToken ct);
    Task<SearchVolumeRegisterResponse> RegisterSearchVolumeAsync(SearchVolumeRegisterRequest request, CancellationToken ct);
    Task<SearchVolumeStatusResponse> GetSearchVolumeStatusAsync(long requestId, CancellationToken ct);
    Task<SearchVolumeResultsResponse> GetSearchVolumeResultsAsync(long requestId, SearchVolumeResultsRequest request, CancellationToken ct);
    Task<SearchRankRegisterResponse> RegisterSearchRankAsync(SearchRankRegisterRequest request, CancellationToken ct);
    Task<SearchRankStatusResponse> GetSearchRankStatusAsync(string requestId, CancellationToken ct);
    Task<SearchRankResultsResponse> GetSearchRankResultsAsync(string requestId, SearchRankResultsRequest request, CancellationToken ct);
    // influx, competitive, content, headline, co-occurrence, locations, languagesも同様に定義
}
```

### 8.2 HTTP/エラー処理

| HTTP | 意味 | 本システムの処理 |
| --- | --- | --- |
| 200/201 | 成功 | レスポンスを保存し、meta.consumedCreditを記録。ジョブを次ステップへ進める。 |
| 400 | バリデーションエラー | 内部バリデーション不足として扱い、再試行せず開発/運用へDiscord通知。画面には入力修正を促す。 |
| 402 | クレジット不足 | 再試行しない。ジョブをfailed_fatalにし、Discordへ通知する。アプリ内の予算上限設定や承認制は持たない。 |
| 403 | 認証エラー | 再試行しない。APIキー無効または設定不備としてDiscordへ通知。 |
| 429 | レート制限 | 指数バックオフ + ジッターで再キュー。同時実行数を動的に下げる。 |
| 500 | 内部エラー | 短期リトライ後、失敗ジョブとして保持。 |
| 503 | サービス利用不可 | 長めのバックオフで再キュー。一定回数超過でDiscord通知。 |

### 8.3 APIマッピング

| Method | Path | Summary | Request DTO | 用途 |
| --- | --- | --- | --- | --- |
| POST | /v1/suggest-keywords | サジェストキーワード取得 | SuggestKeywordsDto | サジェスト収集、検索ソース別候補語取得、候補語拡張（EC/YouTube/画像企画は推奨バックログ） |
| POST | /v1/related-keywords | 関連キーワード取得 | RelatedKeywordsDto | 関連語辞書、ロングテール探索、除外語候補 |
| POST | /v1/other-keywords | 潜在的な検索キーワード/質問（LSI/PAA）取得 | OtherKeywordsDto | LSI/PAA、検索意図、FAQ/見出し候補 |
| POST | /v1/question-search | よくある質問検索取得 | SearchQuestionDto | FAQ抽出、FAQPage候補、顧客疑問DB |
| POST | /v1/ranking-keywords | 同時ランクインキーワード取得 | RankingKeywordsDto | 同時ランクイン、クラスタリング、1記事で狙う語の推定 |
| POST | /v1/search-volume | 一括キーワード調査登録 | SearchVolumeHistoryDto | 最大50,000語の一括需要調査登録 |
| GET | /v1/search-volume/{requestId}/status | 一括キーワード調査ステータス取得 | - | 一括調査ジョブの完了監視 |
| POST | /v1/search-volume/{requestId}/results | 一括キーワード調査データ取得 | SearchVolumeResultsDto | 検索ボリューム、SEO難易度、CPC、トレンド取得 |
| GET | /v1/search-volume/locations | 地域一覧取得 | - | 地域マスタ同期 |
| GET | /v1/search-volume/languages | 言語一覧取得 | - | 言語マスタ同期 |
| POST | /v1/influx-keywords | 獲得キーワード調査取得 | InfluxKeywordsKeywordDto | 自社/競合の獲得キーワード、ギャップ分析 |
| POST | /v1/influx-pages | 獲得ページ調査取得 | InfluxPagesDto | 獲得ページ、稼ぎ頭ページ、リライト候補 |
| POST | /v1/competitive | 競合サイト抽出 | CompetitiveDto | 競合ドメイン抽出、重複率、競合独自キーワード数 |
| POST | /v1/content-search | 集客コンテンツ検索 | ContentSearchDto | SEO集客コンテンツ検索、記事企画ベンチマーク |
| POST | /v1/headline | 見出し抽出取得 | HeadlineDto | SERP上位20ページ見出し、構成案、網羅性評価 |
| POST | /v1/co-occurrence | 共起語取得 | CoOccurrenceDto | 共起語、語彙網羅率、リライト差分 |
| POST | /v1/search-rank | 検索順位チェック登録 | SearchRankHistoryDto | 順位チェック登録 |
| GET | /v1/search-rank/{requestId}/status | 検索順位チェックステータス取得 | - | 順位チェックジョブの完了監視 |
| POST | /v1/search-rank/{requestId}/results | 検索順位チェック結果データ取得 | SearchRankResultsDto | 順位結果、順位分布、推定流入、アラート基礎データ |

## 9. 非同期ジョブ設計

| ジョブ名 | 処理内容 | 起動 |
| --- | --- | --- |
| KeywordDiscoveryJob | suggest/related/other/question/rankingを必要に応じて連続実行し、統合結果を保存。 | 即時または非同期 |
| RegisterSearchVolumeJob | 最大50,000語を分割・重複排除し/v1/search-volumeへ登録。 | 非同期 |
| PollSearchVolumeStatusJob | requestIdのstatusを確認。完了まで再スケジュール。 | ポーリング |
| FetchSearchVolumeResultsJob | 完了後にresultsを取得し、月別検索数・指標を保存。 | 非同期 |
| CompetitorRefreshJob | competitive/influx-keywords/influx-pagesを実行し、競合/ギャップを更新。 | 定期 |
| ContentAnalyzeJob | content-search/headline/co-occurrenceを実行し、記事ブリーフ材料を保存。 | 非同期 |
| RegisterRankCheckJob | keywords×targetsを分割し/v1/search-rankへ登録。 | 定期/手動 |
| PollRankStatusJob | 検索順位チェックのstatusを確認。 | ポーリング |
| FetchRankResultsJob | 順位結果と順位分布を保存し、アラート判定を起動。 | 非同期 |
| GenerateBriefJob | クラスタ、FAQ、見出し、共起語、競合ページからブリーフ生成。 | 非同期 |
| RewriteScoringJob | 順位・流入・共起語不足・見出し不足からリライト優先度を再計算。 | 定期 |
| CannibalizationDetectionJob | 同一keyword_idに複数URLがランクインする候補を検出し、根拠データと推奨対応を保存。 | 定期/手動 |
| MonthlyReportJob | 月次レポートを生成し、reports.format/file_uri、artifact_versions、audit_logsを更新し、必要に応じて共有URL発行とDiscord通知を行う。 | 定期 |
| DataExportJob | CSV/Excelエクスポートを生成し、data_exportsとaudit_logsを更新する。 | 非同期 |
| DataImportJob | CSV/Excelファイルを検証し、キーワード、順位、競合、ブリーフ、タスクを取り込む。 | 非同期 |

### 9.1 ジョブ状態遷移

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

`waiting_external`のキャンセルは内部ジョブのポーリング/結果取得を停止する操作とし、外部API側のrequestId自体は取り消さない。キャンセル後に外部API側で完了した結果は業務テーブルへ取り込まず、既存の外部API呼び出し記録と操作監査のみ保持する。

`failed_retryable`は外部requestId登録前、ポーリング中、結果取得/保存中のいずれでも発生し得る。再試行時は同じ内部`jobs.id`を`queued`へ戻し、既存の`job_external_requests`と`external_api_calls`を参照して重複登録や二重取込を避ける。

### 9.2 一括検索ボリューム調査シーケンス

1. UI/APIがキーワードリスト、地域、言語、SEO難易度取得フラグ、集計期間を受け付ける。

1. Application層で重複排除、上限チェック、クレジット消費見込みの表示、ジョブ登録を行う。

1. Workerがキーワード数、外部API上限、レート制御に応じて分割し、分割単位ごとに/v1/search-volumeへ登録してjob_external_requestsへexternal_request_idを保存する。

1. PollSearchVolumeStatusJobが分割単位ごとにstatusを確認し、isCompleted=trueまで再スケジュールする。

1. 完了後、/v1/search-volume/{requestId}/resultsを取得し、external_api_calls.idをsource_call_idとしてcontract_scope_key、response_uriと紐付けてkeyword_metricsとkeyword_monthly_volumesを更新する。

1. 必要に応じてScoringServiceで機会スコア・トレンド係数を再計算する。

### 9.3 順位チェックシーケンス

1. 対象キーワードと`targets`（URL/ドメイン）、matchType、depth、月間検索数/SEO難易度取得有無を受け付ける。

1. `targets`は1から50件、depth選択肢、キーワード重複排除を検証する。

1. Workerがキーワード数とURL数に応じて分割し、分割単位ごとに/v1/search-rankへ登録してjob_external_requestsへexternal_request_idを保存する。

1. 分割単位ごとにstatusをポーリングし、SERPと検索ボリューム/SEO難易度の処理完了を待つ。

1. resultsを取得し、external_api_calls.idをsource_call_idとしてcontract_scope_keyとともにrank_resultsに保存。順位分布と推定流入を集計する。

1. Phase 2ではアラート条件を評価し、`alert_events`とDiscord `rank_alert`通知を更新する。カニバリ候補更新と月次レポート材料更新はPhase 3の`CannibalizationDetectionJob` / `MonthlyReportJob`で扱う。

## 10. スコアリング/分析ロジック

| ロジック | 概要 |
| --- | --- |
| 機会スコア | 正規化検索ボリューム × (1 - SEO難易度/100) × トレンド係数 × 商業性係数 × 関連度係数。 |
| クラスタリング | 同時ランクイン度、語彙類似度、PAA/FAQ類似度、SERP上位URL重複、検索意図ラベルを特徴量にしてクラスタ化。 |
| 検索意図推定 | 修飾語辞書（とは/方法/料金/比較/口コミ/おすすめ/購入など）とSERP/FAQ文脈で Know/Do/Buy/Compare/Trouble に分類。 |
| 競合ギャップ | 競合獲得キーワード集合 - 自社獲得キーワード集合。流入価値・難易度・関連度で優先順位付け。 |
| リライト優先度 | 4-10位/11-20位、検索ボリューム、難易度、流入価値、共起語不足、見出し不足、前年比上昇を加点。 |
| カニバリ検出 | 同一keyword_idに複数ranked_urlが存在し、順位差が小さい/変動が交互に発生する場合に候補化。 |
| 広告/SEO判断 | CPC高・難易度高は広告/LP、CPC高・難易度低はSEO優先、検索需要低は長期/FAQ候補に分類。 |
| アラート判定 | 前回順位との差分、順位帯移動、圏外化、競合との差分、推定流入損失額でDiscord通知閾値を判定。 |

### 10.1 機会スコア計算例

```text
volumeScore      = log10(searchVolume + 1) / log10(maxVolume + 1)
difficultyScore  = 1 - (seoDifficulty / 100)
trendScore       = clamp(1 + changeRate3m / 100, 0.5, 1.8)
commercialScore  = normalize(cpc) * 0.7 + normalize(competition) * 0.3
relevanceScore   = rankingKeywordRelevance or clusterSimilarity
opportunityScore = 100 * volumeScore * difficultyScore * trendScore * (0.7 + 0.3 * commercialScore) * relevanceScore
```

`maxVolume`は同一プロジェクト、地域、言語、スコアリング実行単位内の最大検索ボリュームとし、全キーワードの検索ボリュームが0の場合は`volumeScore=0`とする。`normalize(cpc)`と`normalize(competition)`は同一実行単位内のmin-max正規化で0から1へ丸め、値が全件同一または欠損の場合は0とする。`relevanceScore`は同時ランクイン度またはクラスタ類似度を0から1へ正規化した値を使い、根拠がない検索ボリューム単独候補では0.6を既定値にする。`changeRate3m`が算出できない場合は`trendScore=1.0`とする。

機会スコアは関連度やプロジェクト既定地域/言語に依存するため、`project_keyword_scores`を正本にする。`score_components_json`にはvolumeScore、difficultyScore、trendScore、commercialScore、relevanceScore、使用したmetric/source_call_idを保存し、ダッシュボードとキーワード探索画面はこの値を参照する。

### 10.2 リライト優先度計算例

```text
rewriteScore =
  positionBandWeight(4-10:1.0, 11-20:0.8, 21-50:0.4)
  * volumeScore
  * trafficValueScore
  * (1 - difficultyPenalty)
  + missingHeadingScore
  + missingCoOccurrenceScore
  + trendBoost
  + cannibalizationPenaltyOrBoost
```

## 11. 画面基本設計

| 画面ID | 画面名 | 対象フェーズ | 主なUI要素/処理 |
| --- | --- | --- | --- |
| S-001 | 起動/プロジェクト選択 | MVP | 単一利用者として起動し、既定ワークスペース内でプロジェクトを切り替える。SSO/テナント選択/権限別メニューは初期版では扱わない。 |
| S-010 | ホームダッシュボード | MVP（段階拡張） | Phase 1ではキーワード探索、一括検索ボリューム、機会スコア、APIクレジットを表示する。Phase 2では競合、コンテンツ、記事ブリーフ、順位、順位アラート指標を追加し、リライト、カニバリ、レポート、AI関連指標はPhase 3で追加する。 |
| S-020 | キーワード探索 | MVP | シード入力、検索エンジン選択、サジェスト/関連語/LSI/PAA/FAQ統合結果、フィルタ、保存。 |
| S-030 | 一括検索ボリューム | MVP | CSV貼付/アップロード、地域/言語、ジョブ登録、進捗、結果テーブル、トレンドグラフ。 |
| S-040 | トピッククラスター | Phase 2 | クラスタ一覧、親子関係、代表語、記事候補、機会スコア、内部リンク候補。 |
| S-050 | 競合分析 | Phase 2 | 競合サイト一覧、重複率、競合独自語、自社独自語、流入/集客価値比較。 |
| S-060 | 獲得キーワード/ページ | Phase 2 | ドメイン/URL入力、獲得語、順位、流入、ページ価値、ギャップ抽出。 |
| S-070 | コンテンツ分析 | Phase 2 | 集客コンテンツ、見出し比較、共起語、FAQ、構成差分。 |
| S-080 | 記事ブリーフ | Phase 2（AI案はPhase 3） | ターゲット語、検索意図、構成、必須語、FAQ、競合URL、エクスポート。AI生成案はPhase 3で追加する。 |
| S-090 | リライト管理 | Phase 3 | 優先度、対象URL、不足見出し/語彙、順位、流入価値、カニバリ候補、ステータス、担当者（初期版はdeveloper固定）。 |
| S-100 | 順位監視 | Phase 2 | キーワード/ターゲット登録、順位分布、履歴、競合比較、アラート条件。 |
| S-110 | EC/YouTube/画像企画 | 推奨 | 検索ソース別の候補語、商品/動画/画像SEOタグ、季節性、商業性。 |
| S-120 | レポート | Phase 3 | 月次レポート作成、テンプレート、PDF/Excel/共有URL、Discord通知履歴。 |
| S-130 | AIアシスタント | Phase 3 | 自然言語から調査ジョブ、ブリーフ生成、差分分析、レポート要約を実行。 |
| S-900 | 管理 | MVP（段階拡張） | MVPはワークスペース設定、APIキー、クレジット消費、Discord通知設定、監査ログ、ジョブキュー。Phase 2では`rank_alert`、`alert_events`、Phase 2ジョブ種別、監査検索導線を追加する。Phase 3で外部連携スタブ設定と実行履歴を追加する。 |

### 11.1 代表画面レイアウト

```text
キーワード探索画面
--------------------------------------------------------
[プロジェクト] [地域] [言語]
[シードキーワード入力___________________] [調査開始]
[検索ソース: Google Bing YouTube Amazon 楽天 Shopping Image]
[フィルタ: Volume min/max | Difficulty | CPC | Competition | 出現時期]
--------------------------------------------------------
| keyword | source | class | volume | difficulty | cpc | competition | score |
--------------------------------------------------------
[一括ボリューム調査] [CSV出力]
Phase 2追加: [クラスタ生成] [ブリーフ作成]
記事ブリーフ画面
--------------------------------------------------------
左: ターゲットキーワード/クラスタ/FAQ/共起語/競合URL
右: 構成書エディタ
  - 想定検索意図
  - タイトル案
  - H2/H3構成
  - 必須トピック
  - FAQ
  - 内部リンク候補
  - メタディスクリプション案
[保存] [Markdown出力]
Phase 3追加: [AI再生成] [PDF出力] [共有URL発行]
```

## 12. セキュリティ設計

| 項目 | 設計 |
| --- | --- |
| 認証 | 初期版は開発者本人のみの利用を前提に、ローカル実行ではOS/開発環境の保護に委ねる。本番相当環境へ公開する場合は単一管理者ログイン + Cookie/BFF構成にする。 |
| 認可 | 初期版はRBACを実装せず、プロジェクト配下の業務APIはURL上のprojectIdでスコープを確定し、対象リソースが同一project_idに属することを必ず検証する。Phase 4で複数ユーザー化する場合にPolicy-based authorizationを追加する。 |
| 秘密情報 | APIキーはKey Vault参照。設定値はOptions patternで注入し、ログ出力禁止。 |
| 監査 | 外部API実行、キー操作、ジョブ操作、CSV/Excel出力、レポート出力/ダウンロード、AI実行をaudit_logsへ固定の操作主体developerで保存。 |
| 入力検証 | FluentValidation。URL/ドメイン、キーワード数、limit、sortBy/orderBy、matchTypeをサーバー側で検証。 |
| 出力制御 | Phase 3の共有URLは期限付き署名、スコープ確認を必須にする。CSV/Excel出力とレポートダウンロードは短時間URLを発行し、監査ログを必須にする。 |
| AI | プロンプトに秘密情報を含めない。根拠データIDを保持し、画面上でAI生成であることを明示。 |

## 13. ログ/監視/運用設計

| 対象 | 設計 |
| --- | --- |
| 構造化ログ | workspace_id, project_id, job_id, external_request_id, correlation_id, endpoint, status_code, elapsed_msを含める。 |
| メトリクス | API呼び出し数、消費クレジット、429/402/403/5xx数、ジョブ成功率、キュー滞留数、P95応答時間。 |
| トレース | API -> Application -> Worker -> External API -> DB保存までOpenTelemetryで追跡。 |
| アラート | ジョブ失敗率>5%、API 402発生、403連続、429急増、キュー滞留、DB接続エラー、レポート生成失敗。 |
| 運用画面 | ジョブ再実行、キャンセル、APIキー停止、クレジット消費確認、失敗詳細閲覧。 |

## 14. インフラ/デプロイ設計

| コンポーネント | 設計 |
| --- | --- |
| Web/API | コンテナ化したASP.NET Core。水平スケール、ヘルスチェック、WAF/リバースプロキシ配下。 |
| Worker | Web/APIとは別プロセス。ジョブ種別ごとにキュー/ワーカー数を調整。 |
| DB | PostgreSQL Flexible Server等。自動バックアップ、PITR、Read Replica検討。 |
| Redis | キャッシュ、分散ロック、レート制御、一時状態管理。ジョブキューはHangfireのPostgreSQL storageで永続化する。 |
| Storage | 外部APIローデータ、レポート、エクスポートファイル、インポート元ファイル、テンプレートをBlob Storage等に保存。 |
| Key Vault | APIキー、AIキー、OAuthシークレット、署名キーを保管。 |
| CI/CD | GitHub Actions/Azure DevOps。build, test, migration dry-run, container scan, deploy, smoke test。 |

### 14.1 環境変数例

```text
ConnectionStrings__Default=Host=...;Database=seo;Username=...;
Redis__ConnectionString=...
Hangfire__Storage=PostgreSQL
RakkoKeyword__BaseUrl=https://api.rakkokeyword.com
RakkoKeyword__ApiKeySecretName=rakko-keyword-api-key-prod
RakkoKeyword__MaxConcurrentRequests=2
RakkoKeyword__Retry__MaxAttempts=5
Jobs__SearchVolumePollIntervalSeconds=60
Jobs__RankCheckPollIntervalSeconds=60
Credits__ResetTimeZone=Asia/Tokyo
Discord__DefaultWebhookSecretName=discord-webhook-prod
Ai__Provider=AzureOpenAI
Observability__OtlpEndpoint=...
```

## 15. テスト設計

| 種類 | 対象 |
| --- | --- |
| 単体テスト | スコアリング、クラスタリング、入力検証、プロジェクトスコープ判定、DTOマッピング。 |
| 統合テスト | DB、ジョブ、Redis、API、外部APIモック、ローデータ保存、契約スコープ別キャッシュ、分割requestId追跡、成果物バージョン、CSV/Excel入出力。 |
| 契約テスト | ラッコAPI OpenAPI由来のリクエスト/レスポンス互換、エラー形式、必須項目。 |
| E2Eテスト | キーワード探索からブリーフ生成、順位チェック、カニバリ検出、レポート出力までの主要フロー。 |
| 負荷テスト | 大量キーワード、同時ジョブ、画面P95、DBインデックス、キュー滞留。 |
| セキュリティテスト | プロジェクトスコープ混在、APIキー漏えい、CSV出力監査、CSRF。 |
| 障害テスト | 429/402/403/500/503、DB一時断、Worker停止、ジョブ再実行。 |

## 16. 開発・移行方針

- OpenAPI JSONをリポジトリにvendor仕様として保存し、更新時は差分レビューを必須にする。

- 外部API DTOは自動生成を基本とし、Application層では業務DTOへ変換する。

- MVPではSearch Console/GA4/CMSの実データ連携を対象外とする。Phase 3ではGSC/GA4/CMS/BIの拡張インターフェース、設定API、Secret参照、接続テストスタブ、実行履歴までを実装し、実データ連携コネクタは推奨バックログとして扱う。

- 既存キーワードリストは、Phase 3以降はCSV/Excelインポートでkeyword_seedsとkeywordsへ登録する。初期移行が必要な場合は、開発者限定のSeedDataまたは一時取込手順として詳細設計で分離する。

- 本番移行前に、APIキー設定、クレジット消費確認、レート制限、通知先、バックアップ復元をチェックリスト化する。

- 詳細設計では、内部APIごとのrequest/response、DBの型/FK/NULL制約、プロジェクトスコープ検証、ジョブごとの再試行上限、画面項目定義を確定する。現時点の詳細設計正本はapi_design.md、db_design.md、screen_design.md、job_design.md、test_plan.md、external_api_design.md、operations_runbook.md、environment_setup.md、adr/とする。

## 付録A. 外部APIマッピング

外部APIマッピングの正本は`docs/api_design.md`の「ラッコキーワードAPI連携」とする。OpenAPI更新時はapi_design.mdを更新し、本付録は参照先として維持する。

## 付録B. リクエストDTO制約

主要リクエストDTOの必須項目・上限制約・デフォルト値を設計時の入力検証に反映する。実装時の正本は`docs/api_design.md`と`rakko-keyword-api-docs.json`とし、本付録は基本設計上の参照用要約とする。

| DTO | 必須項目 | 主な制約/デフォルト |
| --- | --- | --- |
| SuggestKeywordsDto | keyword | keyword: 1文字以上。<br>modes: google / bing / youtube / googleVideo / amazon / rakuten / googleShopping / googleImage。省略時は google。<br>increaseKeyword: 省略時は false。<br>filter: 検索ボリューム、SEO難易度、CPC、競合性、出現時期、サジェストクラス等。<br>sortBy: keyword / suggestClass / seoDifficulty / searchVolume / cpc / competition / firstSeenRange。省略時は searchVolume。<br>orderBy: asc / desc。省略時は desc。<br>limit: 1以上。省略時は全件。 |
| RelatedKeywordsDto | keyword | keyword: 1文字以上。<br>matchType: partialMatch / phraseMatch / prefixMatch / suffixMatch / wordMatch。省略時は partialMatch。<br>filter: 検索ボリューム、SEO難易度、CPC、競合性、出現時期等。<br>sortBy: seoDifficulty / searchVolume / cpc / competition / firstSeenRange。省略時は searchVolume。<br>orderBy: asc / desc。省略時は desc。<br>limit: 1〜25000。省略時は 1000。 |
| OtherKeywordsDto | keyword | keyword: 1文字以上。<br>sortBy: importance / seoDifficulty / searchVolume / cpc / competition / firstSeenRange。省略時は importance。<br>orderBy: asc / desc。省略時は desc。 |
| SearchQuestionDto | keyword | keyword: 1文字以上。<br>limit: 1〜200。省略時は 100。 |
| RankingKeywordsDto | keyword | keyword: 1文字以上。<br>searchTop: 3 / 5 / 10 / 20 / 30 / 50。省略時は 20。<br>searchRange: 10 / 20 / 30 / 50 / 100。省略時は 50。<br>filter: キーワード、SEO難易度、検索ボリューム、CPC、競合性、関連度等。<br>sortBy: seoDifficulty / searchVolume / cpc / competition / relevance。省略時は relevance。<br>orderBy: asc / desc。省略時は desc。<br>limit: 1〜5000。省略時は 500。 |
| SearchVolumeHistoryDto | keywords | keywords: 1〜50000件。<br>seoDifficulty: 省略時は false。<br>dataCompletion: 省略時は true。<br>location: Google Ads API LocationCriterion 準拠。省略時は Japan。<br>language: Google Ads API LanguageCriterion 準拠。省略時は Japanese。<br>deduplicate: 省略時は true。<br>aggregationPeriodMonths: 12 / 24 / 36 / 48。省略時は 12。 |
| SearchVolumeResultsDto | - | noiseReduction: 省略時は true。<br>filter: キーワード、SEO難易度、検索ボリューム、CPC、競合性等。<br>sortBy: keyword / seoDifficulty / searchVolume / rateOfChange / cpc / competition。省略時は searchVolume。<br>orderBy: asc / desc。省略時は desc。<br>limit: 1〜50000。省略時は 100。 |
| InfluxKeywordsKeywordDto | targets | targets: 1〜20件。対象ドメインまたはURLとマッチタイプの配列。<br>keywordCollapse: 省略時は false。<br>filter: キーワード、SEO難易度、検索順位、検索ボリューム、CPC、競合性、推定流入数等。<br>sortBy: keyword / seoDifficulty / rank / searchVolume / cpc / competition / etv。省略時は etv。<br>orderBy: asc / desc。省略時は desc。<br>limit: 1〜10000。省略時は 100。 |
| InfluxPagesDto | targets | targets: 1〜20件。対象ドメインまたはURLとマッチタイプの配列。<br>topKeywordCollapse: 省略時は false。<br>filter: 合計推定流入数、キーワード数、合計集客価値、タイトル、URL、トップキーワード、SEO難易度等。<br>sortBy: totalEtv / totalTrafficValue / keywordCount。省略時は totalEtv。<br>orderBy: asc / desc。省略時は desc。<br>limit: 1〜10000。省略時は 100。 |
| CompetitiveDto | url | url: 対象ドメインURL。<br>sortBy: duplicate / duplicateRate / competitorUnique / targetUnique / etv / keywordCount / trafficValue / pageCount。省略時は etv。<br>orderBy: asc / desc。省略時は desc。 |
| ContentSearchDto | keyword | keyword: 1文字以上。<br>searchTarget: title / keyword / description / titleAndKeyword / titleAndKeywordAndDescription。省略時は titleAndKeywordAndDescription。<br>isAdvancedSearch: 省略時は true。<br>topKeywordCollapse: 省略時は false。<br>filter: 推定流入数、ランクインキーワード数、集客価値、タイトル、URL、トップキーワード、ディスクリプション、SEO難易度等。<br>sortBy: estimatedTraffic / trafficValue / rankingKeywordCount。省略時は trafficValue。<br>orderBy: asc / desc。省略時は desc。<br>limit: 1〜5000。省略時は 100。 |
| HeadlineDto | keyword | keyword: 1文字以上。<br>lessHeadlines: 省略時は false。<br>lessCharacters: 省略時は false。<br>h1 / h2 / h3 / h4: 省略時は true。<br>h5 / h6: 省略時は false。<br>sortBy: position / title / headlineCount / wordCount。省略時は position。<br>orderBy: asc / desc。省略時は asc。<br>limit: 1〜20。省略時は 20。 |
| CoOccurrenceDto | keyword | keyword: 1文字以上。<br>getDetails: 省略時は true。<br>sortBy: word / occurrencePageCount / occurrenceTitleCount / occurrenceHeadingCount / siteCountTotal / siteCountHeading。省略時は siteCountTotal。<br>orderBy: asc / desc。省略時は desc。<br>limit: 1以上。省略時は全件。 |
| SearchRankHistoryDto | keywords, urls | keywords: 1件以上。<br>urls: 1〜50件。<br>matchType: url / forward_url / domain / sub_domain。省略時は sub_domain。<br>depth: 30 / 40 / 50 / 60 / 70 / 80 / 90 / 100。省略時は 30。<br>isSearchVolumeAndSeoDifficultyEnabled: 省略時は false。<br>deduplicate: 省略時は true。 |
| SearchRankResultsDto | - | filter: キーワード、SEO難易度、検索ボリューム等。<br>sortBy: keyword / seoDifficulty / searchVolume。省略時は searchVolume。<br>orderBy: asc / desc。省略時は desc。<br>limit: 1以上。省略時は 100。<br>withAggregation: 省略時は false。 |

## 付録C. API別データ保存方針

API別の保存先、カラム、リレーション、インデックスの正本は`docs/db_design.md`とする。本付録は外部APIとデータ領域の対応関係を示す要約である。

| API | 保存先 | 備考 |
| --- | --- | --- |
| suggest-keywords | keyword_suggestions, keywords, keyword_metrics, external_api_calls | engine/suggestClass/metricsを保存。ローデータはexternal_api_callsのrequest_uri/response_uriへ保存。 |
| related-keywords | related_keywords, keywords, keyword_metrics, external_api_calls | matchTypeとmetricsを保存。ローデータはexternal_api_callsのrequest_uri/response_uriへ保存。 |
| other-keywords/question-search | lsi_paa_items, questions, external_api_calls | type, importance, question/keywordを保存。 |
| ranking-keywords | ranking_keywords, external_api_calls | relevanceとmetricsを保存しクラスタ生成に利用。 |
| search-volume | search_volume_jobs, job_external_requests, search_volume_results, keyword_metrics, keyword_monthly_volumes, external_api_calls | 非同期ジョブ。分割requestId、trends/monthlySearchVolume、契約スコープを時系列化。 |
| influx-keywords/pages | influx_keyword_results, influx_page_results, external_api_calls | 自社/競合分析、リライト候補、ギャップ分析。 |
| competitive | competitive_results, competitor_sites, external_api_calls | 競合サイトと重複率/独自語数を保存。 |
| content-search/headline/co-occurrence | content_search_results, serp_headline_pages, serp_headlines, co_occurrence_words, external_api_calls | 記事ブリーフ・リライト差分の根拠。 |
| search-rank | rank_check_jobs, job_external_requests, rank_check_targets, rank_results, alerts, alert_events, external_api_calls | Phase 2では順位履歴、順位分布、順位アラートを保存する。カニバリ候補とレポート材料はPhase 3で順位履歴から派生させる。 |
| exports/imports | data_exports, data_imports, audit_logs | CSV/Excel入出力の条件、ファイル参照、検証エラー、操作履歴を保存。 |

## 付録D. 参照資料

- rakko-keyword-api-docs.json（OpenAPI 3.0、ラッコキーワードAPI v1.4.1、アップロードファイル）

- requirements.md

- api_design.md

- db_design.md

- screen_design.md

- job_design.md

- test_plan.md

- external_api_design.md

- operations_runbook.md

- environment_setup.md

- adr/

- Microsoft Learn: .NET releases, patches, and support

- Microsoft Learn: Microsoft .NET and .NET Core lifecycle
