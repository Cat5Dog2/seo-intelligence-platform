# TODO

このファイルは、設計ドキュメントを実装しやすい GitHub Issue 相当の単位へ分割したバックログです。実際に GitHub Issue を作る場合は、各 `ISSUE-*` セクションを1 Issueとして転記または自動登録します。

正本ドキュメント:

- `docs/requirements.md`
- `docs/basic_design.md`
- `docs/api_design.md`
- `docs/db_design.md`
- `docs/job_design.md`
- `docs/screen_design.md`
- `docs/test_plan.md`
- `docs/external_api_design.md`
- `docs/operations_runbook.md`
- `docs/environment_setup.md`
- `docs/adr/*`

## Codexへの依頼方法

基本方針:

- [ ] 1回の依頼では原則として1つのIssueだけを指定する。
- [ ] 依頼文にはIssue ID、目的、参照する正本ドキュメント、完了条件、実行してほしい検証を含める。
- [ ] 実装前に関連ファイルと設計ドキュメントを読ませる。
- [ ] 変更範囲をIssue内に限定し、別Issueの実装や大きなリファクタは行わないよう明記する。
- [ ] 秘密値、`.env`、認証情報、DB migration方針など注意が必要な対象は、変更可否を明記する。
- [ ] 依頼の最後に「変更ファイル、検証コマンド、未実行テストと理由を報告して」と書く。

実装依頼テンプレート:

```text
todo.md の ISSUE-MVP-00X「タイトル」を実装してください。

前提:
- 対象Issueの「参照ドキュメント: ...」に記載された正本ドキュメントを必ず確認してください。
- 対象Issueの「関連」「目的」「範囲」「受入条件」「検証」を完了基準にしてください。
- 関連FR/ACがある場合は docs/requirements.md の該当行も確認してください。
- 対象範囲はこのIssue内に限定してください。
- 既存の設計、命名、レイヤ構成に従ってください。
- 秘密値や .env は変更しないでください。

進め方:
- まず todo.md の対象Issueと参照ドキュメント、関連ファイルを確認してください。
- 実装前に、今回触る範囲と想定する検証コマンドを短く共有してください。
- 必要なコード、テスト、ドキュメント更新を行ってください。
- 対象Issueの「検証」に書かれたコマンドを優先し、難しい場合は最小限の関連テストを実行してください。

完了時:
- todo.md の対象Issueで完了したチェック項目を更新してください。
- 変更ファイルを列挙してください。
- 実行した検証コマンドを列挙してください。
- 実行しなかったテストがあれば理由を説明してください。
```

調査だけ依頼するテンプレート:

```text
todo.md の ISSUE-MVP-00X を実装する前に、影響範囲を調査してください。

このターンではコード変更しないでください。
関連ファイル、既存構成、実装方針、リスク、推奨する作業順をまとめてください。
```

レビュー依頼テンプレート:

```text
ISSUE-MVP-00X の実装差分をレビューしてください。

バグ、設計逸脱、セキュリティ、スコープ不一致、テスト不足を優先して指摘してください。
修正はまだ行わず、ファイル/行番号つきで指摘してください。
```

途中再開依頼テンプレート:

```text
ISSUE-MVP-00X の続きから再開してください。

まず git status と関連ファイルを確認し、既存の未コミット変更を尊重してください。
未完了の作業を特定して、Issueの受入条件を満たすところまで実装と検証を進めてください。
```

## 実装ルール

- [ ] Issue着手時に対象フェーズ、関連FR/AC、参照ドキュメントを確認する。
- [ ] API契約は `api_design.md`、DBは `db_design.md`、ジョブは `job_design.md`、画面は `screen_design.md` を優先する。
- [ ] 業務APIは body 内の `projectId` を受け付けず、URL上の `projectId` とDB上の `project_id` を検証する。
- [ ] 秘密値はDB、ログ、レスポンス、監査ログ、テスト出力に出さない。
- [ ] DELETE系は物理削除せず、`archived` または `disabled` へ状態変更する。
- [ ] `jobs` テーブルを業務ジョブ状態の正本にし、Hangfire内部状態を画面/監査の正本にしない。
- [ ] 外部APIのReal利用は明示切替時のみとし、CIと通常開発はMockを既定にする。
- [ ] 外部API request/response 本体と出力ファイルはStorageに置き、DBにはURI、ハッシュ、ステータス、クレジット、契約スコープを残す。
- [ ] APIキー操作、外部API実行、CSV/Excel/レポート出力、AI実行、ジョブ操作、ダウンロードURL発行を監査する。
- [ ] Issue完了時は、変更ファイル、実行した検証コマンド、未実行テストと理由を残す。

## 実装順

- [x] ISSUE-P0-001 ソリューション骨格を作成する
- [x] ISSUE-P0-002 ローカル開発基盤とCI雛形を作成する
- [x] ISSUE-MVP-001 Domain/Application共通基盤を実装する
- [x] ISSUE-MVP-002 MVP DBスキーマとSeedDataを実装する
- [ ] ISSUE-MVP-003 Infrastructure共通基盤を実装する
- [ ] ISSUE-MVP-004 API共通仕様を実装する
- [ ] ISSUE-MVP-005 管理API、プロジェクトAPI、サイトAPIを実装する
- [ ] ISSUE-MVP-006 Secret管理と監査ログを実装する
- [ ] ISSUE-MVP-007 ラッコキーワードAPIクライアントとMockを実装する
- [ ] ISSUE-MVP-008 ジョブ基盤を実装する
- [ ] ISSUE-MVP-009 マスタ同期を実装する
- [ ] ISSUE-MVP-010 キーワード探索を実装する
- [ ] ISSUE-MVP-011 一括検索ボリューム調査を実装する
- [ ] ISSUE-MVP-012 機会スコアとMVPダッシュボード集計を実装する
- [ ] ISSUE-MVP-013 CSV出力を実装する
- [ ] ISSUE-MVP-014 Discord通知を実装する
- [ ] ISSUE-MVP-015 Blazor共通UIと管理画面を実装する
- [ ] ISSUE-MVP-016 Blazorキーワード探索、検索ボリューム、ダッシュボードを実装する
- [ ] ISSUE-MVP-017 MVP受入テストを整備する
- [ ] ISSUE-MVP-018 MVP運用、監視、ドキュメントを整備する
- [ ] ISSUE-P2-001 Phase 2 DB/API/外部API基盤を追加する
- [ ] ISSUE-P2-002 競合分析と獲得語/ページ分析を実装する
- [ ] ISSUE-P2-003 コンテンツ分析と記事ブリーフを実装する
- [ ] ISSUE-P2-004 トピッククラスターを実装する
- [ ] ISSUE-P2-005 順位監視と順位アラートを実装する
- [ ] ISSUE-P2-006 Phase 2 UIを実装する
- [ ] ISSUE-P2-007 Phase 2受入テストを整備する
- [ ] ISSUE-P3-001 Phase 3 DB/API基盤を追加する
- [ ] ISSUE-P3-002 リライト優先度とカニバリ検出を実装する
- [ ] ISSUE-P3-003 レポート生成、共有URL、監査を実装する
- [ ] ISSUE-P3-004 CSV/ExcelインポートとExcelエクスポートを実装する
- [ ] ISSUE-P3-005 AIアシスタントを実装する
- [ ] ISSUE-P3-006 外部連携スタブを実装する
- [ ] ISSUE-P3-007 Phase 3 UIを実装する
- [ ] ISSUE-P3-008 Phase 3受入テストを整備する
- [ ] ISSUE-P4-001 エンタープライズ拡張を設計する
- [ ] ISSUE-BACKLOG-001 推奨バックログを整理する

## Phase 0

### ISSUE-P0-001 ソリューション骨格を作成する

参照ドキュメント: `docs/basic_design.md`, `docs/environment_setup.md`, `docs/adr/0001-dotnet-10-aspnet-core.md`, `docs/adr/0002-blazor-web-app.md`

目的: .NET 10 / Clean Architecture 前提の最小ビルド可能な構成を作る。

範囲:

- [x] `SeoIntelligence.sln` を作成する。
- [x] `src/SeoIntelligence.Domain` を作成する。
- [x] `src/SeoIntelligence.Application` を作成する。
- [x] `src/SeoIntelligence.Contracts` を作成する。
- [x] `src/SeoIntelligence.Infrastructure` を作成する。
- [x] `src/SeoIntelligence.Api` を作成する。
- [x] `src/SeoIntelligence.Web` を Blazor Web App として作成する。
- [x] `src/SeoIntelligence.Worker` を Worker Service として作成する。
- [x] `tests/UnitTests`, `tests/IntegrationTests`, `tests/ContractTests`, `tests/E2ETests` を作成する。
- [x] 依存方向を Domain -> なし、Application -> Domain/Contracts、Infrastructure -> Application実装、Api/Web/Worker -> Application に揃える。

受入条件:

- [x] `dotnet build` が成功する。
- [x] 各プロジェクトの依存方向が設計通りである。

検証:

- [x] `dotnet build`

### ISSUE-P0-002 ローカル開発基盤とCI雛形を作成する

参照ドキュメント: `docs/basic_design.md`, `docs/environment_setup.md`, `docs/operations_runbook.md`, `docs/test_plan.md`, `docs/adr/0003-postgresql-ef-core-jsonb.md`, `docs/adr/0004-hangfire-postgresql-worker.md`, `docs/adr/0005-redis-cache-lock-rate-limit.md`

目的: PostgreSQL、Redis、Storage代替、CIの最小開発ループを作る。

範囲:

- [x] Docker ComposeでPostgreSQL、Redis、Storage代替を起動できるようにする。
- [x] 共通設定、Options、DI、Loggingの雛形を作る。
- [x] OpenTelemetryの導入口を作る。
- [x] CIでbuild、test、migration dry-run、container scan、smoke testを実行する雛形を作る。
- [x] `docs/environment_setup.md` に正式な起動コマンドを追記する。

受入条件:

- [x] ローカル依存サービスを起動できる。
- [x] CI定義が最低限のbuild/testを実行できる。

検証:

- [x] `dotnet build`
- [x] Docker Compose起動確認

## Phase 1: MVP

### ISSUE-MVP-001 Domain/Application共通基盤を実装する

参照ドキュメント: `docs/requirements.md`, `docs/basic_design.md`, `docs/domain_glossary.md`, `docs/mvp_implementation_plan.md`, `docs/implementation_notes.md`

関連: FR-003, NFR-007

目的: MVP機能を実装するためのDomain/Application共通モデルとユースケース境界を作る。

範囲:

- [x] UUID v7相当ID、UTC日時、Asia/Tokyo集計境界、固定actor `developer` の共通方針を実装する。
- [x] 共通Result型、ページング、検索、ソート、エラーコードを実装する。
- [x] キーワードtrim、Unicode正規化、空行除外、重複排除、URL/ドメイン正規化を実装する。
- [x] 共通ステータス値を定義する。
- [x] `ProjectContextService` を実装する。
- [x] `AdministrationService`, `MasterDataService`, `KeywordDiscoveryService`, `SearchVolumeService`, `ScoringService`, `DataTransferService`, `ExternalApiUsageService`, `NotificationService`, `DashboardService` の境界を作る。

受入条件:

- [x] Domainは他プロジェクトへ依存しない。
- [x] ApplicationはInfrastructureへ依存しない。
- [x] 入力正規化とステータス遷移のUnit testがある。

検証:

- [x] `dotnet test --filter Category=Unit`

### ISSUE-MVP-002 MVP DBスキーマとSeedDataを実装する

参照ドキュメント: `docs/db_design.md`, `docs/basic_design.md`, `docs/requirements.md`, `docs/mvp_implementation_plan.md`, `docs/adr/0003-postgresql-ef-core-jsonb.md`, `docs/adr/0007-secret-store-and-audit.md`

関連: FR-001, FR-002, FR-004, FR-005, FR-010, FR-020, FR-030, FR-121, FR-140, AC-019

目的: MVP受入に必要なDBテーブル、制約、インデックス、初期データを実装する。

範囲:

- [x] EF Core DbContextとPostgreSQL接続を実装する。
- [x] 初回migrationで `pg_trgm` 拡張を有効化する。
- [x] 管理系テーブルを作る: `workspaces`, `projects`, `sites`, `api_credentials`, `api_contract_scopes`, `notification_channels`, `notification_deliveries`, `audit_logs`。
- [x] 外部API/ジョブ系テーブルを作る: `locations`, `languages`, `external_api_calls`, `jobs`, `job_external_requests`。
- [x] キーワード系テーブルを作る: `keyword_seeds`, `keywords`, `keyword_suggestions`, `related_keywords`, `questions`, `lsi_paa_items`, `ranking_keywords`。
- [x] 検索ボリューム系テーブルを作る: `search_volume_jobs`, `search_volume_results`, `keyword_metrics`, `keyword_monthly_volumes`, `project_keyword_scores`。
- [x] 出力系テーブルを作る: `data_exports`。
- [x] `db_design.md` のMVP対象インデックスを追加する。
- [x] 既定workspaceのSeedDataを作成する。
- [x] `api_contract_scopes` の初期データを作成する。

受入条件:

- [x] migration適用でMVPテーブルが作成される。
- [x] `keywords` の重複排除、`jobs` のIdempotency制約、主要検索インデックスが存在する。
- [x] 初期workspaceと契約スコープが登録される。

検証:

- [x] `dotnet ef database update --project src/SeoIntelligence.Infrastructure --startup-project src/SeoIntelligence.Api`
- [x] `dotnet test --filter Category=Integration`

### ISSUE-MVP-003 Infrastructure共通基盤を実装する

参照ドキュメント: `docs/basic_design.md`, `docs/db_design.md`, `docs/external_api_design.md`, `docs/job_design.md`, `docs/environment_setup.md`, `docs/adr/0003-postgresql-ef-core-jsonb.md`, `docs/adr/0004-hangfire-postgresql-worker.md`, `docs/adr/0005-redis-cache-lock-rate-limit.md`, `docs/adr/0007-secret-store-and-audit.md`

関連: NFR-003, NFR-004, NFR-005, NFR-008

目的: DB、Storage、Secret、Redis、Hangfireの共通インフラを提供する。

範囲:

- [ ] RepositoryまたはDbContext利用方針を整備する。
- [ ] Storage抽象を実装する。
- [ ] Secret Store抽象を実装する。
- [ ] Redis抽象を実装する。
- [ ] Hangfire PostgreSQL storageを構成する。
- [ ] 構造化ログに `workspace_id`, `project_id`, `job_id`, `external_request_id`, `correlation_id` を含める。

受入条件:

- [ ] DB、Redis、Storage、Secret Storeの疎通確認ができる。
- [ ] 秘密値実体をログに出さない。

検証:

- [ ] `dotnet test --filter Category=Integration`

### ISSUE-MVP-004 API共通仕様を実装する

参照ドキュメント: `docs/api_design.md`, `docs/basic_design.md`, `docs/requirements.md`, `docs/test_plan.md`

関連: AC-008, AC-019

目的: 全APIに共通レスポンス、エラー、Correlation ID、OpenAPI、ヘルスチェックを適用する。

範囲:

- [ ] `/healthz` を実装する。
- [ ] `/readyz` を実装し、DB/Redis接続を確認する。
- [ ] `/openapi/v1.json` を出力する。
- [ ] 共通レスポンスenvelopeを適用する。
- [ ] 共通エラー形式を実装する。
- [ ] `X-Correlation-Id` の受け取り、未指定時生成、ログ/DB保存を実装する。
- [ ] 一覧API共通の `page`, `pageSize`, `status`, `sortBy`, `orderBy`, `q` を実装する。
- [ ] プロジェクトスコープ不一致時の403/404方針を実装する。
- [ ] サーバー側バリデーションを導入する。

受入条件:

- [ ] API smoke testで共通envelopeとエラー形式を確認できる。
- [ ] OpenAPIが出力される。
- [ ] scope不一致が拒否される。

検証:

- [ ] `dotnet test --filter Category=Integration`

### ISSUE-MVP-005 管理API、プロジェクトAPI、サイトAPIを実装する

参照ドキュメント: `docs/api_design.md`, `docs/db_design.md`, `docs/requirements.md`, `docs/mvp_implementation_plan.md`, `docs/api_examples.md`, `docs/test_plan.md`

関連: FR-001, FR-002, FR-004, FR-005, FR-140, AC-010, AC-011

目的: MVPの管理系CRUDとプロジェクト/サイト管理を実装する。

範囲:

- [ ] `GET/PUT /api/admin/workspace`
- [ ] `GET/POST /api/admin/api-credentials`
- [ ] `GET/PUT/DELETE /api/admin/api-credentials/{credentialId}`
- [ ] `POST /api/admin/api-credentials/{credentialId}/enable`
- [ ] `POST /api/admin/api-credentials/{credentialId}/rotate`
- [ ] `GET/POST /api/admin/notification-channels`
- [ ] `GET/PUT/DELETE /api/admin/notification-channels/{channelId}`
- [ ] `POST /api/admin/notification-channels/{channelId}/enable`
- [ ] `POST /api/admin/notification-channels/{channelId}/test`
- [ ] `GET /api/admin/notification-deliveries`
- [ ] `GET /api/admin/notification-deliveries/{deliveryId}`
- [ ] `POST /api/admin/notification-deliveries/{deliveryId}/retry`
- [ ] `GET /api/admin/external-api-calls`
- [ ] `GET /api/admin/audit-logs`
- [ ] `GET /api/admin/audit-logs/{auditLogId}`
- [ ] `GET/POST /api/projects`
- [ ] `GET/PUT/DELETE /api/projects/{projectId}`
- [ ] `POST /api/projects/{projectId}/restore`
- [ ] `GET/POST /api/projects/{projectId}/sites`
- [ ] `GET/PUT/DELETE /api/projects/{projectId}/sites/{siteId}`
- [ ] `POST /api/projects/{projectId}/sites/{siteId}/restore`

受入条件:

- [ ] 管理系CRUDが通る。
- [ ] DELETEは物理削除せず `archived` または `disabled` へ更新する。
- [ ] 復元/再有効化で `active` へ戻せる。
- [ ] 別プロジェクト参照を拒否する。

検証:

- [ ] `dotnet test --filter Category=Integration`

### ISSUE-MVP-006 Secret管理と監査ログを実装する

参照ドキュメント: `docs/api_design.md`, `docs/db_design.md`, `docs/external_api_design.md`, `docs/operations_runbook.md`, `docs/environment_setup.md`, `docs/adr/0007-secret-store-and-audit.md`

関連: FR-004, AC-014

目的: 秘密値非返却と監査ログをMVP全体の横断機能として実装する。

範囲:

- [ ] `secretValue` と `keyRef` の同時指定を禁止する。
- [ ] `secretValue` はSecret Storeへ保存し、DBには `key_ref` のみ保存する。
- [ ] APIキー、Webhook URLの実値をレスポンス、ログ、監査ログへ出さない。
- [ ] APIキー作成/更新/無効化/ローテーションを `audit_logs` に記録する。
- [ ] 外部API実行、CSV出力、ジョブ操作を `audit_logs` に記録する。
- [ ] 監査ログ検索APIで actor、resource、correlation_id、期間検索を行えるようにする。

受入条件:

- [ ] 秘密値がDB、ログ、レスポンス、監査ログに残らない。
- [ ] APIキー操作、外部API実行、CSV出力、ジョブ操作の監査ログを検索できる。

検証:

- [ ] `dotnet test --filter Category=Integration`
- [ ] `dotnet test --filter Category=Security`

### ISSUE-MVP-007 ラッコキーワードAPIクライアントとMockを実装する

参照ドキュメント: `docs/external_api_design.md`, `docs/api_design.md`, `docs/rakko-keyword-api-docs.json`, `docs/test_plan.md`, `docs/adr/0006-openapi-dto-generation.md`, `docs/adr/0007-secret-store-and-audit.md`

関連: FR-010, FR-011, FR-020, FR-021, AC-001

目的: OpenAPI由来DTO、Mock既定、Real切替可能な外部API境界を作る。

範囲:

- [ ] `docs/rakko-keyword-api-docs.json` から外部DTOを生成する仕組みを作る。
- [ ] 生成DTOをInfrastructure層に閉じ込め、Application DTOへ変換する。
- [ ] `IRakkoKeywordClient` を定義する。
- [ ] Mock版 `IRakkoKeywordClient` を実装する。
- [ ] Real版 `IRakkoKeywordClient` を実装する。
- [ ] `X-API-Key` をSecret Storeから取得する。
- [ ] timeout、User-Agent、correlation_idを設定する。
- [ ] `meta.consumedCredit` を `external_api_calls.consumed_credit` に保存する。
- [ ] request/response圧縮JSONをStorageに保存し、DBにはURIとハッシュを保存する。
- [ ] 契約スコープ `api_contract_scopes.scope_key` によるキャッシュ再利用判定を実装する。
- [ ] MVP対象エンドポイントを連携する: suggest, related, other, question, ranking, search-volume register/status/results, locations, languages。

受入条件:

- [ ] CI/通常開発はMockで動作する。
- [ ] Realは明示切替時のみ使う。
- [ ] 主要正常系と429/402/403/500/503の契約/Mockテストが通る。

検証:

- [ ] `dotnet test --filter Category=Contract`
- [ ] `dotnet test --filter Category=Integration`

### ISSUE-MVP-008 ジョブ基盤を実装する

参照ドキュメント: `docs/job_design.md`, `docs/api_design.md`, `docs/db_design.md`, `docs/external_api_design.md`, `docs/operations_runbook.md`, `docs/adr/0004-hangfire-postgresql-worker.md`, `docs/adr/0005-redis-cache-lock-rate-limit.md`

関連: NFR-004, AC-009, AC-014

目的: Hangfire + Worker + アプリ独自 `jobs` による非同期処理の基盤を実装する。

範囲:

- [ ] Worker ServiceをHangfire + PostgreSQL storageで起動する。
- [ ] キューを構成する: `default`, `external-api`, `polling`, `analysis`, `exports`, `notifications`。
- [ ] ジョブ状態遷移を実装する。
- [ ] `queued` と `waiting_external` のキャンセルを実装する。
- [ ] `waiting_external` キャンセル後は以後のポーリング/結果取込を停止する。
- [ ] `failed_retryable` の手動再実行を実装する。
- [ ] `failed_fatal` と `canceled` は同一ジョブ再実行不可にする。
- [ ] Redis lockで同一プロジェクト/同一ジョブ種別/同一対象の重複実行を抑止する。
- [ ] `Idempotency-Key` + `request_hash` + scopeでジョブ二重登録を抑止する。
- [ ] 429/500/503/timeout/DB一時障害のリトライを実装する。
- [ ] 400/402/403を `failed_fatal` として扱う。
- [ ] `GET /api/jobs`, `GET /api/jobs/{jobId}`, `POST /api/jobs/{jobId}/cancel`, `POST /api/jobs/{jobId}/retry` を実装する。

受入条件:

- [ ] `jobs` が業務状態の正本になる。
- [ ] Idempotency-Key重複登録で既存ジョブが返る。
- [ ] 429はretryable、402/403はfatalへ分岐する。
- [ ] ジョブ操作が監査ログに残る。

検証:

- [ ] `dotnet test --filter Category=Integration`

### ISSUE-MVP-009 マスタ同期を実装する

参照ドキュメント: `docs/api_design.md`, `docs/db_design.md`, `docs/job_design.md`, `docs/external_api_design.md`, `docs/mvp_implementation_plan.md`

関連: FR-021

目的: 地域/言語マスタをラッコAPIから同期し、API/UIで利用できるようにする。

範囲:

- [ ] `MasterDataSyncJob` を実装する。
- [ ] `POST /api/admin/master-data/sync` を実装する。
- [ ] `GET /api/master-data/locations` を実装する。
- [ ] `GET /api/master-data/languages` を実装する。
- [ ] `locations`, `languages` のupsertとstatus管理を実装する。

受入条件:

- [ ] マスタ同期ジョブが成功する。
- [ ] 地域/言語一覧がAPIで取得できる。

検証:

- [ ] `dotnet test --filter Category=Integration`

### ISSUE-MVP-010 キーワード探索を実装する

参照ドキュメント: `docs/requirements.md`, `docs/api_design.md`, `docs/db_design.md`, `docs/job_design.md`, `docs/external_api_design.md`, `docs/screen_design.md`, `docs/mvp_implementation_plan.md`, `docs/test_plan.md`

関連: FR-010, FR-011, FR-012, AC-002

目的: 1シードからサジェスト/関連語/LSI/PAA/FAQ/同時ランクインを統合取得し保存する。

範囲:

- [ ] `KeywordDiscoveryJob` を実装する。
- [ ] suggest / related / other / question / ranking を条件に応じて呼び出す。
- [ ] `keyword_seeds`, `keywords`, `keyword_suggestions`, `related_keywords`, `questions`, `lsi_paa_items`, `ranking_keywords` を保存する。
- [ ] 冪等キーを `projectId + normalized seed + sources + filter hash` にする。
- [ ] `POST /api/projects/{projectId}/keyword-discovery/suggest` を実装する。
- [ ] 軽量条件では200、重い条件では202 + `jobId` + `statusUrl` を返す。
- [ ] 候補語、ソース、階層、指標、機会スコア、フィルタ、ソートを返す。

受入条件:

- [ ] 1シードから候補語を統合取得できる。
- [ ] 統合結果を保存、フィルタできる。
- [ ] 取得済みAPIの結果は保存し、未取得APIはretryableとして扱える。

検証:

- [ ] `dotnet test --filter Category=Integration`
- [ ] `dotnet test --filter Category=Contract`

### ISSUE-MVP-011 一括検索ボリューム調査を実装する

参照ドキュメント: `docs/requirements.md`, `docs/api_design.md`, `docs/db_design.md`, `docs/job_design.md`, `docs/external_api_design.md`, `docs/screen_design.md`, `docs/mvp_implementation_plan.md`, `docs/test_plan.md`

関連: FR-020, FR-021, AC-003

目的: 最大50,000語の検索ボリューム調査を登録、ポーリング、結果保存できるようにする。

範囲:

- [ ] `RegisterSearchVolumeJob` を実装する。
- [ ] 最大50,000語を重複除外し、推定クレジットを記録する。
- [ ] 外部API上限、レート制御、入力件数に応じて分割する。
- [ ] `PollSearchVolumeStatusJob` を60秒間隔で再スケジュールする。
- [ ] `FetchSearchVolumeResultsJob` を実装する。
- [ ] `search_volume_results`, `keyword_metrics`, `keyword_monthly_volumes` を保存する。
- [ ] `POST /api/projects/{projectId}/search-volume/jobs` を実装する。
- [ ] `GET /api/projects/{projectId}/search-volume/jobs/{jobId}` を実装する。
- [ ] `GET /api/projects/{projectId}/search-volume/jobs/{jobId}/results` を実装する。

受入条件:

- [ ] 1,000語以上のジョブ登録、完了監視、結果保存ができる。
- [ ] `job_external_requests.external_request_id` が保存される。
- [ ] `waiting_external` キャンセル後は結果取込されない。

検証:

- [ ] `dotnet test --filter Category=Integration`
- [ ] `dotnet test --filter Category=Contract`

### ISSUE-MVP-012 機会スコアとMVPダッシュボード集計を実装する

参照ドキュメント: `docs/requirements.md`, `docs/basic_design.md`, `docs/db_design.md`, `docs/api_design.md`, `docs/screen_design.md`, `docs/mvp_implementation_plan.md`, `docs/domain_glossary.md`

関連: FR-030, FR-110

目的: 検索指標と関連度から機会スコアを算出し、MVPダッシュボードへ表示できる集計を作る。

範囲:

- [ ] `OpportunityScoringJob` を実装する。
- [ ] 検索ボリューム、SEO難易度、CPC、競合性、トレンド、関連度をスコアリングする。
- [ ] スコア算出で`maxVolume`、CPC/競合性の正規化範囲、関連度欠損時、トレンド欠損時の既定値を`basic_design.md`通りに扱う。
- [ ] `project_keyword_scores` に結果を保存する。
- [ ] `score_components_json` に算出根拠を保存する。
- [ ] `GET /api/projects/{projectId}/dashboard` を実装する。
- [ ] キーワード探索件数、一括調査件数、機会スコア上位、クレジット消費、失敗ジョブ、通知失敗を集計する。

受入条件:

- [ ] 検索ボリューム結果取得後に機会スコアが再計算される。
- [ ] ダッシュボードAPIでMVP指標が返る。

検証:

- [ ] `dotnet test --filter Category=Unit`
- [ ] `dotnet test --filter Category=Integration`

### ISSUE-MVP-013 CSV出力を実装する

参照ドキュメント: `docs/requirements.md`, `docs/api_design.md`, `docs/db_design.md`, `docs/job_design.md`, `docs/screen_design.md`, `docs/api_examples.md`, `docs/test_plan.md`

関連: FR-121, AC-013

目的: Phase 1対象データをCSVとしてStorageへ出力し、状態確認、ダウンロード、監査を実装する。

範囲:

- [ ] `DataExportJob` のMVP CSV出力を実装する。
- [ ] `data_exports` を作成/更新する。
- [ ] CSVファイルをStorageへ保存する。
- [ ] 短時間downloadUrlを発行する。
- [ ] 出力、URL発行、ダウンロードを `audit_logs` に記録する。
- [ ] `POST /api/projects/{projectId}/exports/csv` を実装する。
- [ ] `GET /api/projects/{projectId}/exports/{exportId}` を実装する。
- [ ] `GET /api/projects/{projectId}/exports/{exportId}/download` を実装する。

受入条件:

- [ ] CSV出力、状態取得、ダウンロードができる。
- [ ] 出力履歴と監査ログを確認できる。

検証:

- [ ] `dotnet test --filter Category=Integration`
- [ ] `dotnet test --filter Category=E2E`

### ISSUE-MVP-014 Discord通知を実装する

参照ドキュメント: `docs/requirements.md`, `docs/external_api_design.md`, `docs/job_design.md`, `docs/db_design.md`, `docs/api_design.md`, `docs/operations_runbook.md`, `docs/test_plan.md`

関連: AC-012

目的: ジョブ失敗とクレジット不足402をDiscord Webhookへ通知し、送信履歴と再送を実装する。

範囲:

- [ ] `NotificationDeliveryJob` を実装する。
- [ ] 通知種別 `job_failed`, `credit_low` を実装する。
- [ ] Webhook URLをSecret Storeから取得する。
- [ ] 送信履歴を `notification_deliveries` に保存する。
- [ ] 429/5xx/timeoutは `retrying`、最大5回後 `failed` にする。
- [ ] 手動テスト通知と手動再送を実装する。

受入条件:

- [ ] Phase 1通知と送信履歴、失敗時再送状態を確認できる。
- [ ] 402 Mockで `failed_fatal`、Discord通知、監査ログ記録へ分岐する。

検証:

- [ ] `dotnet test --filter Category=Integration`
- [ ] `dotnet test --filter Category=Operational`

### ISSUE-MVP-015 Blazor共通UIと管理画面を実装する

参照ドキュメント: `docs/screen_design.md`, `docs/api_design.md`, `docs/requirements.md`, `docs/basic_design.md`, `docs/mvp_implementation_plan.md`, `docs/adr/0002-blazor-web-app.md`

関連: S-001, S-900, AC-010, AC-014

目的: MVP画面の共通レイアウトと管理画面を実装する。

範囲:

- [ ] Header、Project Switcher、Location/Language、Credit Status、Side Navigation、Main Contentを実装する。
- [ ] 共通コンポーネントを実装する: `ProjectSwitcher`, `LocationLanguageSelector`, `CreditBadge`, `JobProgressPanel`, `DataTable`, `StatusFilter`, `AuditLink`, `ErrorSummary`。
- [ ] S-001 起動/プロジェクト選択を実装する。
- [ ] S-900 管理のMVP範囲を実装する。
- [ ] ワークスペース設定、APIキー、クレジット消費、Discord通知設定、通知履歴、ジョブ一覧、監査ログを表示する。
- [ ] APIキー登録/無効化/ローテーション、通知テスト、ジョブ再実行、監査検索を実装する。
- [ ] APIキーやWebhook URLの実値を画面へ再表示しない。

受入条件:

- [ ] 管理系CRUDを画面から操作できる。
- [ ] 監査ログへ辿れる。
- [ ] 秘密値が画面に出ない。

検証:

- [ ] `dotnet test --filter Category=UI`
- [ ] `dotnet test --filter Category=E2E`

### ISSUE-MVP-016 Blazorキーワード探索、検索ボリューム、ダッシュボードを実装する

参照ドキュメント: `docs/screen_design.md`, `docs/api_design.md`, `docs/requirements.md`, `docs/basic_design.md`, `docs/mvp_implementation_plan.md`, `docs/test_plan.md`, `docs/adr/0002-blazor-web-app.md`

関連: S-010, S-020, S-030, AC-002, AC-003

目的: MVPの主要価値である調査、検索ボリューム、ダッシュボード画面を実装する。

範囲:

- [ ] S-020 キーワード探索を実装する。
- [ ] シード、検索ソース、limit、フィルタ、sortBy/orderBy、同期希望を入力できるようにする。
- [ ] keyword、source、suggest_class、volume、difficulty、cpc、competition、first_seen_range、opportunity_scoreを表示する。
- [ ] 検索ボリューム調査へ送る、CSV出力を実装する。
- [ ] S-030 一括検索ボリュームを実装する。
- [ ] 貼付テキストとCSVファイル選択をブラウザ内でパースし、APIへは `keywords` JSON配列だけを送る。
- [ ] 1から50,000件、重複除外、空行除外、地域/言語必須を検証する。
- [ ] ジョブ登録、キャンセル、再実行、結果フィルタ、CSV出力を実装する。
- [ ] S-010 ホームダッシュボードを実装する。
- [ ] loading、empty、validation error、job running、job failed、retryable状態を共通表示する。

受入条件:

- [ ] キーワード探索の取得、保存、フィルタが画面からできる。
- [ ] CSV入力はブラウザ内でパースされ、APIへCSVファイル本体を送らない。
- [ ] 一括検索ボリュームの進捗と結果を確認できる。

検証:

- [ ] `dotnet test --filter Category=UI`
- [ ] `dotnet test --filter Category=E2E`

### ISSUE-MVP-017 MVP受入テストを整備する

参照ドキュメント: `docs/test_plan.md`, `docs/requirements.md`, `docs/mvp_implementation_plan.md`, `docs/api_design.md`, `docs/db_design.md`, `docs/job_design.md`, `docs/external_api_design.md`, `docs/screen_design.md`

関連: AC-001, AC-002, AC-003, AC-008, AC-009, AC-010, AC-011, AC-012, AC-013, AC-014, AC-019

目的: MVP完了条件に必要な自動テストを整備する。

範囲:

- [ ] Unit: スコアリング、入力検証、DTOマッピング、状態遷移。
- [ ] Integration: CRUD、ジョブ登録、DB保存、スコープ検証、監査ログ。
- [ ] Contract: ラッコAPI DTO、必須項目、エラー形式、`requestId`、`consumedCredit`。
- [ ] UI Component: 入力、バリデーション、空状態、ジョブ進捗表示。
- [ ] E2E: 管理操作、キーワード探索、一括調査、CSV出力。
- [ ] Security: APIキー/Webhook非表示、プロジェクト分離。
- [ ] Operational: 429/402/403/500/503、APIキー無効、ジョブ再実行、通知。
- [ ] T-MVP-001からT-MVP-020を実装する。

受入条件:

- [ ] MVP完了条件のACが通る。
- [ ] 主要障害系がMockで確認済み。

検証:

- [ ] `dotnet test`
- [ ] `dotnet test --filter Category=Unit`
- [ ] `dotnet test --filter Category=Integration`
- [ ] `dotnet test --filter Category=Contract`

### ISSUE-MVP-018 MVP運用、監視、ドキュメントを整備する

参照ドキュメント: `docs/operations_runbook.md`, `docs/environment_setup.md`, `docs/test_plan.md`, `docs/api_examples.md`, `docs/implementation_notes.md`, `docs/basic_design.md`

関連: FR-140, NFR-008

目的: MVPを運用できる最低限の監視、Runbook、ドキュメント更新を行う。

範囲:

- [ ] ジョブ失敗、キュー滞留、402/403、429急増、クレジット消費量を確認できる導線を作る。
- [ ] `job_success_rate`, `job_queue_depth`, `job_duration_p95`, `external_api_429_count`, `external_api_402_count`, `external_api_credit_consumed`, `notification_failure_count`, `retry_count_by_job_type` をメトリクス化する。
- [ ] Runbookのスモークテストを実行できるようにする。
- [ ] `docs/operations_runbook.md` に実装後の具体的な確認導線とコマンドを追記する。
- [ ] `docs/test_plan.md` に正式なテストコマンドとテストDB起動手順を追記する。
- [ ] `docs/api_examples.md` を実装済みレスポンスに合わせて更新する。
- [ ] 仕様変更があれば正本文書とADR/implementation notesを更新する。

受入条件:

- [ ] `/healthz`, `/readyz`, プロジェクト一覧、監査ログ検索、マスタ同期、Discordテスト通知、CSV出力のスモーク確認ができる。
- [ ] MVP運用手順がRunbookに反映されている。

検証:

- [ ] Runbookスモークテスト

## Phase 2: SEO実務拡張

### ISSUE-P2-001 Phase 2 DB/API/外部API基盤を追加する

参照ドキュメント: `docs/requirements.md`, `docs/db_design.md`, `docs/api_design.md`, `docs/job_design.md`, `docs/external_api_design.md`, `docs/test_plan.md`

目的: 競合、コンテンツ、記事ブリーフ、順位監視に必要なテーブルと外部API連携を追加する。

範囲:

- [ ] Phase 2テーブルを追加する: `competitor_sites`, `influx_keyword_results`, `influx_page_results`, `competitive_results`, `content_search_results`, `serp_headline_pages`, `serp_headlines`, `co_occurrence_words`, `co_occurrence_page_details`, `topic_clusters`, `cluster_keywords`, `article_briefs`, `rank_check_jobs`, `rank_check_targets`, `rank_results`, `alerts`, `alert_events`。
- [ ] Phase 2外部APIを連携する: influx-keywords, influx-pages, competitive, content-search, headline, co-occurrence, search-rank register/status/results。
- [ ] Phase 2用インデックスを追加する。

受入条件:

- [ ] Phase 2 migrationが通る。
- [ ] Phase 2外部APIのContract testが通る。

### ISSUE-P2-002 競合分析と獲得語/ページ分析を実装する

参照ドキュメント: `docs/requirements.md`, `docs/api_design.md`, `docs/db_design.md`, `docs/job_design.md`, `docs/external_api_design.md`, `docs/screen_design.md`, `docs/test_plan.md`

関連: FR-050, FR-051, FR-052, AC-004

範囲:

- [ ] `CompetitorRefreshJob` を実装する。
- [ ] `GET /api/projects/{projectId}/competitors`
- [ ] `POST /api/projects/{projectId}/competitors/analyze`
- [ ] `GET /api/projects/{projectId}/influx-keywords`
- [ ] `GET /api/projects/{projectId}/influx-pages`
- [ ] 競合、獲得語、獲得ページ、ギャップを保存/表示できるようにする。

受入条件:

- [ ] 対象ドメインから競合、獲得語、獲得ページ、ギャップを表示できる。

### ISSUE-P2-003 コンテンツ分析と記事ブリーフを実装する

参照ドキュメント: `docs/requirements.md`, `docs/api_design.md`, `docs/db_design.md`, `docs/job_design.md`, `docs/external_api_design.md`, `docs/screen_design.md`, `docs/test_plan.md`

関連: FR-040, FR-060, FR-061, FR-062, AC-005

範囲:

- [ ] `ContentAnalyzeJob` を実装する。
- [ ] `GenerateBriefJob` を実装する。
- [ ] `GET /api/projects/{projectId}/content-analyses`
- [ ] `POST /api/projects/{projectId}/content/analyze`
- [ ] `GET /api/projects/{projectId}/briefs`
- [ ] `POST /api/projects/{projectId}/briefs/generate`
- [ ] `GET /api/projects/{projectId}/briefs/{briefId}`
- [ ] `PUT /api/projects/{projectId}/briefs/{briefId}`
- [ ] `GET /api/projects/{projectId}/briefs/{briefId}/versions`
- [ ] `POST /api/projects/{projectId}/briefs/{briefId}/export`
- [ ] 記事ブリーフの版履歴を `artifact_versions` に保存する。

受入条件:

- [ ] 指定キーワードで集客コンテンツ、見出し、共起語を取得し、記事ブリーフを生成できる。

### ISSUE-P2-004 トピッククラスターを実装する

参照ドキュメント: `docs/requirements.md`, `docs/basic_design.md`, `docs/api_design.md`, `docs/db_design.md`, `docs/job_design.md`, `docs/screen_design.md`, `docs/domain_glossary.md`

関連: FR-031

範囲:

- [ ] `TopicClusterGenerateJob` を実装する。
- [ ] 同時ランクイン度、語彙類似度、検索意図、FAQでクラスタリングする。
- [ ] `GET /api/projects/{projectId}/clusters`
- [ ] `GET /api/projects/{projectId}/clusters/{clusterId}`
- [ ] `POST /api/projects/{projectId}/clusters/generate`

受入条件:

- [ ] クラスタ一覧、親子関係、代表語、記事候補、機会スコアを確認できる。

### ISSUE-P2-005 順位監視と順位アラートを実装する

参照ドキュメント: `docs/requirements.md`, `docs/api_design.md`, `docs/db_design.md`, `docs/job_design.md`, `docs/external_api_design.md`, `docs/screen_design.md`, `docs/test_plan.md`, `docs/operations_runbook.md`

関連: FR-080, FR-081, AC-006, AC-012

範囲:

- [ ] `RegisterRankCheckJob` を実装する。
- [ ] `PollRankStatusJob` を60秒間隔で再スケジュールする。
- [ ] `FetchRankResultsJob` を実装する。
- [ ] `RankAlertEvaluateJob` を実装する。
- [ ] `POST /api/projects/{projectId}/rank-check/jobs`
- [ ] `GET /api/projects/{projectId}/rank-check/jobs/{jobId}/results`
- [ ] `GET /api/projects/{projectId}/rank-results`
- [ ] `GET/POST /api/projects/{projectId}/alerts`
- [ ] `PUT/DELETE /api/projects/{projectId}/alerts/{alertId}`
- [ ] `POST /api/projects/{projectId}/alerts/{alertId}/enable`
- [ ] `GET /api/projects/{projectId}/alert-events`
- [ ] Discord通知 `rank_alert` を実装する。

受入条件:

- [ ] キーワードとURL/ドメインで順位チェックを登録し、結果、順位分布、アラートを確認できる。

### ISSUE-P2-006 Phase 2 UIを実装する

参照ドキュメント: `docs/screen_design.md`, `docs/api_design.md`, `docs/requirements.md`, `docs/basic_design.md`, `docs/adr/0002-blazor-web-app.md`

範囲:

- [ ] S-040 トピッククラスターを実装する。
- [ ] S-050 競合分析を実装する。
- [ ] S-060 獲得キーワード/ページを実装する。
- [ ] S-070 コンテンツ分析を実装する。
- [ ] S-080 記事ブリーフのPhase 2範囲を実装する。
- [ ] S-100 順位監視を実装する。
- [ ] S-010 ダッシュボードに競合、コンテンツ、記事ブリーフ、順位指標を追加する。
- [ ] S-900 管理にPhase 2の通知/ジョブ/監査導線を追加する。

受入条件:

- [ ] Phase 2の主要業務フローが画面から操作できる。

### ISSUE-P2-007 Phase 2受入テストを整備する

参照ドキュメント: `docs/test_plan.md`, `docs/requirements.md`, `docs/api_design.md`, `docs/db_design.md`, `docs/job_design.md`, `docs/external_api_design.md`, `docs/screen_design.md`

範囲:

- [ ] AC-004 競合分析のE2E/Integrationを通す。
- [ ] AC-005 コンテンツ分析/記事ブリーフのE2E/Integrationを通す。
- [ ] AC-006 順位監視/順位分布/`alert_events`/通知連携のE2E/Integrationを通す。
- [ ] AC-012のPhase 2順位アラート通知を通す。
- [ ] 競合、コンテンツ、記事ブリーフ、順位監視の契約テストを通す。

受入条件:

- [ ] Phase 2完了条件が通る。

## Phase 3: 自動化 / AI / 外部連携

### ISSUE-P3-001 Phase 3 DB/API基盤を追加する

参照ドキュメント: `docs/requirements.md`, `docs/db_design.md`, `docs/api_design.md`, `docs/basic_design.md`, `docs/external_api_design.md`, `docs/adr/0007-secret-store-and-audit.md`

範囲:

- [ ] Phase 3テーブルを追加する: `rewrite_tasks`, `cannibalization_candidates`, `reports`, `artifact_versions`, `data_imports`, `external_connector_settings`, `external_connector_runs`, `ai_sessions`, `ai_messages`。
- [ ] `IAiContentService` を定義する。
- [ ] AIプロンプトからAPIキー、Webhook、認証情報、個人情報を除去する共通処理を実装する。
- [ ] 共有URLのトークンハッシュ、期限、失効、期限切れ制御の共通処理を実装する。

受入条件:

- [ ] Phase 3 migrationが通る。
- [ ] Secret実値を返さない設計が保たれる。

### ISSUE-P3-002 リライト優先度とカニバリ検出を実装する

参照ドキュメント: `docs/requirements.md`, `docs/basic_design.md`, `docs/api_design.md`, `docs/db_design.md`, `docs/job_design.md`, `docs/screen_design.md`, `docs/test_plan.md`

関連: FR-070, FR-071, AC-018

範囲:

- [ ] `RewriteScoringJob` を実装する。
- [ ] `CannibalizationDetectionJob` を実装する。
- [ ] `GET /api/projects/{projectId}/rewrite/tasks`
- [ ] `GET /api/projects/{projectId}/rewrite/tasks/{taskId}`
- [ ] `PUT /api/projects/{projectId}/rewrite/tasks/{taskId}`
- [ ] `GET /api/projects/{projectId}/cannibalization/candidates`
- [ ] `POST /api/projects/{projectId}/cannibalization/refresh`

受入条件:

- [ ] リライト候補を優先度付きで確認できる。
- [ ] 同一キーワードに複数URLがランクインする候補を検出し、根拠と推奨対応を確認できる。

### ISSUE-P3-003 レポート生成、共有URL、監査を実装する

参照ドキュメント: `docs/requirements.md`, `docs/api_design.md`, `docs/db_design.md`, `docs/job_design.md`, `docs/screen_design.md`, `docs/test_plan.md`, `docs/operations_runbook.md`

関連: FR-120, AC-007, AC-017

範囲:

- [ ] `MonthlyReportJob` を実装する。
- [ ] PDF/Excelレポート生成を実装する。
- [ ] `POST /api/projects/{projectId}/reports`
- [ ] `GET /api/projects/{projectId}/reports/{reportId}`
- [ ] `GET /api/projects/{projectId}/reports/{reportId}/download`
- [ ] `POST /api/projects/{projectId}/reports/{reportId}/share`
- [ ] `DELETE /api/projects/{projectId}/reports/{reportId}/share`
- [ ] `GET /api/report-shares/{token}`
- [ ] レポート完了通知 `report_completed` を実装する。

受入条件:

- [ ] 月次レポートをPDF/Excelまたは共有URLとして出力できる。
- [ ] ダウンロード、共有URL発行/失効/期限切れが監査される。

### ISSUE-P3-004 CSV/ExcelインポートとExcelエクスポートを実装する

参照ドキュメント: `docs/requirements.md`, `docs/api_design.md`, `docs/db_design.md`, `docs/job_design.md`, `docs/screen_design.md`, `docs/test_plan.md`

関連: FR-150, AC-016

範囲:

- [ ] `DataImportJob` を実装する。
- [ ] `POST /api/projects/{projectId}/exports` をCSV/Excel対応で実装する。
- [ ] `POST /api/projects/{projectId}/imports/upload-url`
- [ ] `POST /api/projects/{projectId}/imports`
- [ ] `GET /api/projects/{projectId}/imports/{importId}`
- [ ] `GET /api/projects/{projectId}/imports/{importId}/errors`
- [ ] 検証エラーと取込履歴を保存する。

受入条件:

- [ ] キーワード、順位、競合、ブリーフ、タスクをCSV/Excelで検証付き取込できる。

### ISSUE-P3-005 AIアシスタントを実装する

参照ドキュメント: `docs/requirements.md`, `docs/basic_design.md`, `docs/api_design.md`, `docs/db_design.md`, `docs/external_api_design.md`, `docs/screen_design.md`, `docs/test_plan.md`, `docs/adr/0007-secret-store-and-audit.md`

関連: FR-041, AC-015

範囲:

- [ ] `AiAssistantJob` を実装する。
- [ ] `POST /api/projects/{projectId}/ai/chat`
- [ ] 自然言語から調査ジョブ、ブリーフ生成、差分分析、レポート要約を実行する。
- [ ] prompt、response、tool_calls、reference_data、token_usage、review_statusを保存する。
- [ ] AI出力を人間レビュー前提の成果物として扱う。

受入条件:

- [ ] AI生成で使用したプロンプト、参照データ、出力、実行者、token_usageを保存し、画面で確認できる。

### ISSUE-P3-006 外部連携スタブを実装する

参照ドキュメント: `docs/requirements.md`, `docs/api_design.md`, `docs/db_design.md`, `docs/external_api_design.md`, `docs/screen_design.md`, `docs/test_plan.md`, `docs/adr/0007-secret-store-and-audit.md`

関連: FR-130, AC-020

範囲:

- [ ] `GET /api/projects/{projectId}/connectors`
- [ ] `POST /api/projects/{projectId}/connectors`
- [ ] `PUT /api/projects/{projectId}/connectors/{connectorId}`
- [ ] `DELETE /api/projects/{projectId}/connectors/{connectorId}` を `status=disabled` 更新として実装する。
- [ ] `POST /api/projects/{projectId}/connectors/{connectorId}/test`
- [ ] `GET /api/projects/{projectId}/connectors/{connectorId}/runs`
- [ ] GSC/GA4/CMS/BIの設定、Secret参照、接続テスト履歴を保存する。
- [ ] 実データ取得は行わない。

受入条件:

- [ ] コネクタ設定を作成/更新/無効化できる。
- [ ] Secret実値を返さず、接続テストスタブと実行履歴を確認できる。

### ISSUE-P3-007 Phase 3 UIを実装する

参照ドキュメント: `docs/screen_design.md`, `docs/api_design.md`, `docs/requirements.md`, `docs/basic_design.md`, `docs/adr/0002-blazor-web-app.md`

範囲:

- [ ] S-080にAI再生成を追加する。
- [ ] S-090 リライト管理を実装する。
- [ ] S-120 レポートを実装する。
- [ ] S-130 AIアシスタントを実装する。
- [ ] S-900 管理に外部連携スタブ設定と実行履歴を追加する。
- [ ] S-010 ダッシュボードにリライト、カニバリ、レポート、AI関連指標を追加する。

受入条件:

- [ ] Phase 3の主要業務フローが画面から操作できる。

### ISSUE-P3-008 Phase 3受入テストを整備する

参照ドキュメント: `docs/test_plan.md`, `docs/requirements.md`, `docs/api_design.md`, `docs/db_design.md`, `docs/job_design.md`, `docs/external_api_design.md`, `docs/screen_design.md`

範囲:

- [ ] AC-007 レポート生成、ダウンロード、共有URL、期限切れ/失効制御を通す。
- [ ] AC-015 AI prompt/response/reference/token_usage保存とレビュー状態を通す。
- [ ] AC-016 CSV/Excelインポート、検証エラー、取込履歴を通す。
- [ ] AC-017 レポート形式、file_uri、共有URL発行/失効/ダウンロード監査を通す。
- [ ] AC-018 カニバリ候補、根拠データ、推奨対応を通す。
- [ ] AC-020 外部連携スタブ、Secret非返却、接続テスト履歴を通す。

受入条件:

- [ ] Phase 3完了条件が通る。

## Phase 4

### ISSUE-P4-001 エンタープライズ拡張を設計する

参照ドキュメント: `docs/requirements.md`, `docs/basic_design.md`, `docs/api_design.md`, `docs/db_design.md`, `docs/operations_runbook.md`, `docs/adr/README.md`

範囲:

- [ ] 複数ユーザーを設計/実装する。
- [ ] ロール/RBACを設計/実装する。
- [ ] SSOを設計/実装する。
- [ ] 監査/承認フローを設計/実装する。
- [ ] マルチリージョン構成を検討/実装する。
- [ ] カスタムスコアリングを設計/実装する。
- [ ] クライアントポータルを設計/実装する。

受入条件:

- [ ] Phase 4の詳細要件とADRが追加されている。

## 推奨バックログ

### ISSUE-BACKLOG-001 推奨バックログを整理する

参照ドキュメント: `docs/requirements.md`, `docs/basic_design.md`, `docs/screen_design.md`, `docs/external_api_design.md`, `docs/domain_glossary.md`

範囲:

- [ ] S-110 EC/YouTube/画像企画を実装候補として整理する。
- [ ] Amazon/楽天/Shopping/Image/YouTubeサジェストから商品名、動画タイトル、alt、タグ候補を抽出する。
- [ ] 広告/LP判断を実装候補として整理する。
- [ ] CPCと広告競合性から広告出稿、LP化、SEO記事化、除外語候補を提案する。
- [ ] GSC実データ連携を実装候補として整理する。
- [ ] GA4実データ連携を実装候補として整理する。
- [ ] CMS投稿/同期連携を実装候補として整理する。
- [ ] BI/DWH実データ出力連携を実装候補として整理する。
- [ ] 被リンク分析、技術SEOクローラー、本文全文解析の追加可否を検討する。

受入条件:

- [ ] 費用対効果、契約条件、Phase昇格条件が整理されている。

## 横断検証コマンド

- [ ] `dotnet build`
- [ ] `dotnet test`
- [ ] `dotnet test --filter Category=Unit`
- [ ] `dotnet test --filter Category=Integration`
- [ ] `dotnet test --filter Category=Contract`
- [ ] `dotnet ef database update --project src/SeoIntelligence.Infrastructure --startup-project src/SeoIntelligence.Api`
- [ ] `dotnet run --project src/SeoIntelligence.Api`
- [ ] `dotnet run --project src/SeoIntelligence.Worker`
