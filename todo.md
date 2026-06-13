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
- [x] ISSUE-MVP-003 Infrastructure共通基盤を実装する
- [x] ISSUE-MVP-004 API共通仕様を実装する
- [x] ISSUE-MVP-005 管理API、プロジェクトAPI、サイトAPIを実装する
- [x] ISSUE-MVP-006 Secret管理と監査ログを実装する
- [x] ISSUE-MVP-007 ラッコキーワードAPIクライアントとMockを実装する
- [x] ISSUE-MVP-008 ジョブ基盤を実装する
- [x] ISSUE-MVP-009 マスタ同期を実装する
- [x] ISSUE-MVP-010 キーワード探索を実装する
- [x] ISSUE-MVP-011 一括検索ボリューム調査を実装する
- [x] ISSUE-MVP-012 機会スコアとMVPダッシュボード集計を実装する
- [x] ISSUE-MVP-013 CSV出力を実装する
- [x] ISSUE-MVP-014 Discord通知を実装する
- [x] ISSUE-MVP-015 Blazor共通UIと管理画面を実装する
- [x] ISSUE-MVP-016 Blazorキーワード探索、検索ボリューム、ダッシュボードを実装する
- [x] ISSUE-MVP-017 MVP受入テストを整備する
- [x] ISSUE-MVP-018 MVP運用、監視、ドキュメントを整備する
- [x] ISSUE-P2-001 Phase 2 DB/API/外部API基盤を追加する
- [x] ISSUE-P2-002 競合分析と獲得語/ページ分析を実装する
- [x] ISSUE-P2-003 コンテンツ分析と記事ブリーフを実装する
- [x] ISSUE-P2-004 トピッククラスターを実装する
- [x] ISSUE-P2-005 順位監視と順位アラートを実装する
- [x] ISSUE-P2-006 Phase 2 UIを実装する
- [x] ISSUE-P2-007 Phase 2受入テストを整備する
- [x] ISSUE-P3-001 Phase 3 DB/API基盤を追加する
- [x] ISSUE-P3-002 リライト優先度とカニバリ検出を実装する
- [x] ISSUE-P3-003 レポート生成、共有URL、監査を実装する
- [x] ISSUE-P3-004 CSV/ExcelインポートとExcelエクスポートを実装する
- [x] ISSUE-P3-005 AIアシスタントを実装する
- [x] ISSUE-P3-006 外部連携スタブを実装する
- [x] ISSUE-P3-007 Phase 3 UIを実装する
- [x] ISSUE-P3-008 Phase 3受入テストを整備する
- [ ] ISSUE-REF-001 コードベース保守性リファクタリングを実施する
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

- [x] RepositoryまたはDbContext利用方針を整備する。
- [x] Storage抽象を実装する。
- [x] Secret Store抽象を実装する。
- [x] Redis抽象を実装する。
- [x] Hangfire PostgreSQL storageを構成する。
- [x] 構造化ログに `workspace_id`, `project_id`, `job_id`, `external_request_id`, `correlation_id` を含める。

受入条件:

- [x] DB、Redis、Storage、Secret Storeの疎通確認ができる。
- [x] 秘密値実体をログに出さない。

検証:

- [x] `dotnet test --filter Category=Integration`

### ISSUE-MVP-004 API共通仕様を実装する

参照ドキュメント: `docs/api_design.md`, `docs/basic_design.md`, `docs/requirements.md`, `docs/test_plan.md`

関連: AC-008, AC-019

目的: 全APIに共通レスポンス、エラー、Correlation ID、OpenAPI、ヘルスチェックを適用する。

範囲:

- [x] `/healthz` を実装する。
- [x] `/readyz` を実装し、DB/Redis接続を確認する。
- [x] `/openapi/v1.json` を出力する。
- [x] 共通レスポンスenvelopeを適用する。
- [x] 共通エラー形式を実装する。
- [x] `X-Correlation-Id` の受け取り、未指定時生成、ログ/DB保存を実装する。
- [x] 一覧API共通の `page`, `pageSize`, `status`, `sortBy`, `orderBy`, `q` を実装する。
- [x] プロジェクトスコープ不一致時の403/404方針を実装する。
- [x] サーバー側バリデーションを導入する。

受入条件:

- [x] API smoke testで共通envelopeとエラー形式を確認できる。
- [x] OpenAPIが出力される。
- [x] scope不一致が拒否される。

検証:

- [x] `dotnet test --filter Category=Integration`

### ISSUE-MVP-005 管理API、プロジェクトAPI、サイトAPIを実装する

参照ドキュメント: `docs/api_design.md`, `docs/db_design.md`, `docs/requirements.md`, `docs/mvp_implementation_plan.md`, `docs/api_examples.md`, `docs/test_plan.md`

関連: FR-001, FR-002, FR-004, FR-005, FR-140, AC-010, AC-011

目的: MVPの管理系CRUDとプロジェクト/サイト管理を実装する。

範囲:

- [x] `GET/PUT /api/admin/workspace`
- [x] `GET/POST /api/admin/api-credentials`
- [x] `GET/PUT/DELETE /api/admin/api-credentials/{credentialId}`
- [x] `POST /api/admin/api-credentials/{credentialId}/enable`
- [x] `POST /api/admin/api-credentials/{credentialId}/rotate`
- [x] `GET/POST /api/admin/notification-channels`
- [x] `GET/PUT/DELETE /api/admin/notification-channels/{channelId}`
- [x] `POST /api/admin/notification-channels/{channelId}/enable`
- [x] `POST /api/admin/notification-channels/{channelId}/test`
- [x] `GET /api/admin/notification-deliveries`
- [x] `GET /api/admin/notification-deliveries/{deliveryId}`
- [x] `POST /api/admin/notification-deliveries/{deliveryId}/retry`
- [x] `GET /api/admin/external-api-calls`
- [x] `GET /api/admin/audit-logs`
- [x] `GET /api/admin/audit-logs/{auditLogId}`
- [x] `GET/POST /api/projects`
- [x] `GET/PUT/DELETE /api/projects/{projectId}`
- [x] `POST /api/projects/{projectId}/restore`
- [x] `GET/POST /api/projects/{projectId}/sites`
- [x] `GET/PUT/DELETE /api/projects/{projectId}/sites/{siteId}`
- [x] `POST /api/projects/{projectId}/sites/{siteId}/restore`

受入条件:

- [x] 管理系CRUDが通る。
- [x] DELETEは物理削除せず `archived` または `disabled` へ更新する。
- [x] 復元/再有効化で `active` へ戻せる。
- [x] 別プロジェクト参照を拒否する。

検証:

- [x] `dotnet test --filter Category=Integration`

### ISSUE-MVP-006 Secret管理と監査ログを実装する

参照ドキュメント: `docs/api_design.md`, `docs/db_design.md`, `docs/external_api_design.md`, `docs/operations_runbook.md`, `docs/environment_setup.md`, `docs/adr/0007-secret-store-and-audit.md`

関連: FR-004, AC-014

目的: 秘密値非返却と監査ログをMVP全体の横断機能として実装する。

範囲:

- [x] `secretValue` と `keyRef` の同時指定を禁止する。
- [x] `secretValue` はSecret Storeへ保存し、DBには `key_ref` のみ保存する。
- [x] APIキー、Webhook URLの実値をレスポンス、ログ、監査ログへ出さない。
- [x] APIキー作成/更新/無効化/ローテーションを `audit_logs` に記録する。
- [x] 外部API実行、CSV出力、ジョブ操作を `audit_logs` に記録する。
- [x] 監査ログ検索APIで actor、resource、correlation_id、期間検索を行えるようにする。

受入条件:

- [x] 秘密値がDB、ログ、レスポンス、監査ログに残らない。
- [x] APIキー操作、外部API実行、CSV出力、ジョブ操作の監査ログを検索できる。

検証:

- [x] `dotnet test --filter Category=Integration`
- [x] `dotnet test --filter Category=Security`

### ISSUE-MVP-007 ラッコキーワードAPIクライアントとMockを実装する

参照ドキュメント: `docs/external_api_design.md`, `docs/api_design.md`, `docs/rakko-keyword-api-docs.json`, `docs/test_plan.md`, `docs/adr/0006-openapi-dto-generation.md`, `docs/adr/0007-secret-store-and-audit.md`

関連: FR-010, FR-011, FR-020, FR-021, AC-001

目的: OpenAPI由来DTO、Mock既定、Real切替可能な外部API境界を作る。

範囲:

- [x] `docs/rakko-keyword-api-docs.json` から外部DTOを生成する仕組みを作る。
- [x] 生成DTOをInfrastructure層に閉じ込め、Application DTOへ変換する。
- [x] `IRakkoKeywordClient` を定義する。
- [x] Mock版 `IRakkoKeywordClient` を実装する。
- [x] Real版 `IRakkoKeywordClient` を実装する。
- [x] `X-API-Key` をSecret Storeから取得する。
- [x] timeout、User-Agent、correlation_idを設定する。
- [x] `meta.consumedCredit` を `external_api_calls.consumed_credit` に保存する。
- [x] request/response圧縮JSONをStorageに保存し、DBにはURIとハッシュを保存する。
- [x] 契約スコープ `api_contract_scopes.scope_key` によるキャッシュ再利用判定を実装する。
- [x] MVP対象エンドポイントを連携する: suggest, related, other, question, ranking, search-volume register/status/results, locations, languages。

受入条件:

- [x] CI/通常開発はMockで動作する。
- [x] Realは明示切替時のみ使う。
- [x] 主要正常系と429/402/403/500/503の契約/Mockテストが通る。

検証:

- [x] `dotnet test --filter Category=Contract`
- [x] `dotnet test --filter Category=Integration`

### ISSUE-MVP-008 ジョブ基盤を実装する

参照ドキュメント: `docs/job_design.md`, `docs/api_design.md`, `docs/db_design.md`, `docs/external_api_design.md`, `docs/operations_runbook.md`, `docs/adr/0004-hangfire-postgresql-worker.md`, `docs/adr/0005-redis-cache-lock-rate-limit.md`

関連: NFR-004, AC-009, AC-014

目的: Hangfire + Worker + アプリ独自 `jobs` による非同期処理の基盤を実装する。

範囲:

- [x] Worker ServiceをHangfire + PostgreSQL storageで起動する。
- [x] キューを構成する: `default`, `external-api`, `polling`, `analysis`, `exports`, `notifications`。
- [x] ジョブ状態遷移を実装する。
- [x] `queued` と `waiting_external` のキャンセルを実装する。
- [x] `waiting_external` キャンセル後は以後のポーリング/結果取込を停止する。
- [x] `failed_retryable` の手動再実行を実装する。
- [x] `failed_fatal` と `canceled` は同一ジョブ再実行不可にする。
- [x] Redis lockで同一プロジェクト/同一ジョブ種別/同一対象の重複実行を抑止する。
- [x] `Idempotency-Key` + `request_hash` + scopeでジョブ二重登録を抑止する。
- [x] 429/500/503/timeout/DB一時障害のリトライを実装する。
- [x] 400/402/403を `failed_fatal` として扱う。
- [x] `GET /api/jobs`, `GET /api/jobs/{jobId}`, `POST /api/jobs/{jobId}/cancel`, `POST /api/jobs/{jobId}/retry` を実装する。

受入条件:

- [x] `jobs` が業務状態の正本になる。
- [x] Idempotency-Key重複登録で既存ジョブが返る。
- [x] 429はretryable、402/403はfatalへ分岐する。
- [x] ジョブ操作が監査ログに残る。

検証:

- [x] `dotnet test --filter Category=Integration`

### ISSUE-MVP-009 マスタ同期を実装する

参照ドキュメント: `docs/api_design.md`, `docs/db_design.md`, `docs/job_design.md`, `docs/external_api_design.md`, `docs/mvp_implementation_plan.md`

関連: FR-021

目的: 地域/言語マスタをラッコAPIから同期し、API/UIで利用できるようにする。

範囲:

- [x] `MasterDataSyncJob` を実装する。
- [x] `POST /api/admin/master-data/sync` を実装する。
- [x] `GET /api/master-data/locations` を実装する。
- [x] `GET /api/master-data/languages` を実装する。
- [x] `locations`, `languages` のupsertとstatus管理を実装する。

受入条件:

- [x] マスタ同期ジョブが成功する。
- [x] 地域/言語一覧がAPIで取得できる。

検証:

- [x] `dotnet test --filter Category=Integration`

### ISSUE-MVP-010 キーワード探索を実装する

参照ドキュメント: `docs/requirements.md`, `docs/api_design.md`, `docs/db_design.md`, `docs/job_design.md`, `docs/external_api_design.md`, `docs/screen_design.md`, `docs/mvp_implementation_plan.md`, `docs/test_plan.md`

関連: FR-010, FR-011, FR-012, AC-002

目的: 1シードからサジェスト/関連語/LSI/PAA/FAQ/同時ランクインを統合取得し保存する。

範囲:

- [x] `KeywordDiscoveryJob` を実装する。
- [x] suggest / related / other / question / ranking を条件に応じて呼び出す。
- [x] `keyword_seeds`, `keywords`, `keyword_suggestions`, `related_keywords`, `questions`, `lsi_paa_items`, `ranking_keywords` を保存する。
- [x] 冪等キーを `projectId + normalized seed + sources + filter hash` にする。
- [x] `POST /api/projects/{projectId}/keyword-discovery/suggest` を実装する。
- [x] 軽量条件では200、重い条件では202 + `jobId` + `statusUrl` を返す。
- [x] 候補語、ソース、階層、指標、機会スコア、フィルタ、ソートを返す。

受入条件:

- [x] 1シードから候補語を統合取得できる。
- [x] 統合結果を保存、フィルタできる。
- [x] 取得済みAPIの結果は保存し、未取得APIはretryableとして扱える。

検証:

- [x] `dotnet test --filter Category=Integration`
- [x] `dotnet test --filter Category=Contract`

### ISSUE-MVP-011 一括検索ボリューム調査を実装する

参照ドキュメント: `docs/requirements.md`, `docs/api_design.md`, `docs/db_design.md`, `docs/job_design.md`, `docs/external_api_design.md`, `docs/screen_design.md`, `docs/mvp_implementation_plan.md`, `docs/test_plan.md`

関連: FR-020, FR-021, AC-003

目的: 最大50,000語の検索ボリューム調査を登録、ポーリング、結果保存できるようにする。

範囲:

- [x] `RegisterSearchVolumeJob` を実装する。
- [x] 最大50,000語を重複除外し、推定クレジットを記録する。
- [x] 外部API上限、レート制御、入力件数に応じて分割する。
- [x] `PollSearchVolumeStatusJob` を60秒間隔で再スケジュールする。
- [x] `FetchSearchVolumeResultsJob` を実装する。
- [x] `search_volume_results`, `keyword_metrics`, `keyword_monthly_volumes` を保存する。
- [x] `POST /api/projects/{projectId}/search-volume/jobs` を実装する。
- [x] `GET /api/projects/{projectId}/search-volume/jobs/{jobId}` を実装する。
- [x] `GET /api/projects/{projectId}/search-volume/jobs/{jobId}/results` を実装する。

受入条件:

- [x] 1,000語以上のジョブ登録、完了監視、結果保存ができる。
- [x] `job_external_requests.external_request_id` が保存される。
- [x] `waiting_external` キャンセル後は結果取込されない。

検証:

- [x] `dotnet test --filter Category=Integration`
- [x] `dotnet test --filter Category=Contract`

### ISSUE-MVP-012 機会スコアとMVPダッシュボード集計を実装する

参照ドキュメント: `docs/requirements.md`, `docs/basic_design.md`, `docs/db_design.md`, `docs/api_design.md`, `docs/screen_design.md`, `docs/mvp_implementation_plan.md`, `docs/domain_glossary.md`

関連: FR-030, FR-110

目的: 検索指標と関連度から機会スコアを算出し、MVPダッシュボードへ表示できる集計を作る。

範囲:

- [x] `OpportunityScoringJob` を実装する。
- [x] 検索ボリューム、SEO難易度、CPC、競合性、トレンド、関連度をスコアリングする。
- [x] スコア算出で`maxVolume`、CPC/競合性の正規化範囲、関連度欠損時、トレンド欠損時の既定値を`basic_design.md`通りに扱う。
- [x] `project_keyword_scores` に結果を保存する。
- [x] `score_components_json` に算出根拠を保存する。
- [x] `GET /api/projects/{projectId}/dashboard` を実装する。
- [x] キーワード探索件数、一括調査件数、機会スコア上位、クレジット消費、失敗ジョブ、通知失敗を集計する。

受入条件:

- [x] 検索ボリューム結果取得後に機会スコアが再計算される。
- [x] ダッシュボードAPIでMVP指標が返る。

検証:

- [x] `dotnet test --filter Category=Unit`
- [x] `dotnet test --filter Category=Integration`

### ISSUE-MVP-013 CSV出力を実装する

参照ドキュメント: `docs/requirements.md`, `docs/api_design.md`, `docs/db_design.md`, `docs/job_design.md`, `docs/screen_design.md`, `docs/api_examples.md`, `docs/test_plan.md`

関連: FR-121, AC-013

目的: Phase 1対象データをCSVとしてStorageへ出力し、状態確認、ダウンロード、監査を実装する。

範囲:

- [x] `DataExportJob` のMVP CSV出力を実装する。
- [x] `data_exports` を作成/更新する。
- [x] CSVファイルをStorageへ保存する。
- [x] 短時間downloadUrlを発行する。
- [x] 出力、URL発行、ダウンロードを `audit_logs` に記録する。
- [x] `POST /api/projects/{projectId}/exports/csv` を実装する。
- [x] `GET /api/projects/{projectId}/exports/{exportId}` を実装する。
- [x] `GET /api/projects/{projectId}/exports/{exportId}/download` を実装する。

受入条件:

- [x] CSV出力、状態取得、ダウンロードができる。
- [x] 出力履歴と監査ログを確認できる。

検証:

- [x] `dotnet test --filter Category=Integration`
- [x] `dotnet test --filter Category=E2E`

### ISSUE-MVP-014 Discord通知を実装する

参照ドキュメント: `docs/requirements.md`, `docs/external_api_design.md`, `docs/job_design.md`, `docs/db_design.md`, `docs/api_design.md`, `docs/operations_runbook.md`, `docs/test_plan.md`

関連: AC-012

目的: ジョブ失敗とクレジット不足402をDiscord Webhookへ通知し、送信履歴と再送を実装する。

範囲:

- [x] `NotificationDeliveryJob` を実装する。
- [x] 通知種別 `job_failed`, `credit_low` を実装する。
- [x] Webhook URLをSecret Storeから取得する。
- [x] 送信履歴を `notification_deliveries` に保存する。
- [x] 429/5xx/timeoutは `retrying`、最大5回後 `failed` にする。
- [x] 手動テスト通知と手動再送を実装する。

受入条件:

- [x] Phase 1通知と送信履歴、失敗時再送状態を確認できる。
- [x] 402 Mockで `failed_fatal`、Discord通知、監査ログ記録へ分岐する。

検証:

- [x] `dotnet test --filter Category=Integration`
- [x] `dotnet test --filter Category=Operational`

### ISSUE-MVP-015 Blazor共通UIと管理画面を実装する

参照ドキュメント: `docs/screen_design.md`, `docs/api_design.md`, `docs/requirements.md`, `docs/basic_design.md`, `docs/mvp_implementation_plan.md`, `docs/adr/0002-blazor-web-app.md`

関連: S-001, S-900, AC-010, AC-014

目的: MVP画面の共通レイアウトと管理画面を実装する。

範囲:

- [x] Header、Project Switcher、Location/Language、Credit Status、Side Navigation、Main Contentを実装する。
- [x] 共通コンポーネントを実装する: `ProjectSwitcher`, `LocationLanguageSelector`, `CreditBadge`, `JobProgressPanel`, `DataTable`, `StatusFilter`, `AuditLink`, `ErrorSummary`。
- [x] S-001 起動/プロジェクト選択を実装する。
- [x] S-900 管理のMVP範囲を実装する。
- [x] ワークスペース設定、APIキー、クレジット消費、Discord通知設定、通知履歴、ジョブ一覧、監査ログを表示する。
- [x] APIキー登録/無効化/ローテーション、通知テスト、ジョブ再実行、監査検索を実装する。
- [x] APIキーやWebhook URLの実値を画面へ再表示しない。

受入条件:

- [x] 管理系CRUDを画面から操作できる。
- [x] 監査ログへ辿れる。
- [x] 秘密値が画面に出ない。

検証:

- [x] `dotnet test --filter Category=UI`
- [x] `dotnet test --filter Category=E2E`

### ISSUE-MVP-016 Blazorキーワード探索、検索ボリューム、ダッシュボードを実装する

参照ドキュメント: `docs/screen_design.md`, `docs/api_design.md`, `docs/requirements.md`, `docs/basic_design.md`, `docs/mvp_implementation_plan.md`, `docs/test_plan.md`, `docs/adr/0002-blazor-web-app.md`

関連: S-010, S-020, S-030, AC-002, AC-003

目的: MVPの主要価値である調査、検索ボリューム、ダッシュボード画面を実装する。

範囲:

- [x] S-020 キーワード探索を実装する。
- [x] シード、検索ソース、limit、フィルタ、sortBy/orderBy、同期希望を入力できるようにする。
- [x] keyword、source、suggest_class、volume、difficulty、cpc、competition、first_seen_range、opportunity_scoreを表示する。
- [x] 検索ボリューム調査へ送る、CSV出力を実装する。
- [x] S-030 一括検索ボリュームを実装する。
- [x] 貼付テキストとCSVファイル選択をブラウザ内でパースし、APIへは `keywords` JSON配列だけを送る。
- [x] 1から50,000件、重複除外、空行除外、地域/言語必須を検証する。
- [x] ジョブ登録、キャンセル、再実行、結果フィルタ、CSV出力を実装する。
- [x] S-010 ホームダッシュボードを実装する。
- [x] loading、empty、validation error、job running、job failed、retryable状態を共通表示する。

受入条件:

- [x] キーワード探索の取得、保存、フィルタが画面からできる。
- [x] CSV入力はブラウザ内でパースされ、APIへCSVファイル本体を送らない。
- [x] 一括検索ボリュームの進捗と結果を確認できる。

検証:

- [x] `dotnet test --filter Category=UI`
- [x] `dotnet test --filter Category=E2E`

### ISSUE-MVP-017 MVP受入テストを整備する

参照ドキュメント: `docs/test_plan.md`, `docs/requirements.md`, `docs/mvp_implementation_plan.md`, `docs/api_design.md`, `docs/db_design.md`, `docs/job_design.md`, `docs/external_api_design.md`, `docs/screen_design.md`

関連: AC-001, AC-002, AC-003, AC-008, AC-009, AC-010, AC-011, AC-012, AC-013, AC-014, AC-019

目的: MVP完了条件に必要な自動テストを整備する。

範囲:

- [x] Unit: スコアリング、入力検証、DTOマッピング、状態遷移。
- [x] Integration: CRUD、ジョブ登録、DB保存、スコープ検証、監査ログ。
- [x] Contract: ラッコAPI DTO、必須項目、エラー形式、`requestId`、`consumedCredit`。
- [x] UI Component: 入力、バリデーション、空状態、ジョブ進捗表示。
- [x] E2E: 管理操作、キーワード探索、一括調査、CSV出力。
- [x] Security: APIキー/Webhook非表示、プロジェクト分離。
- [x] Operational: 429/402/403/500/503、APIキー無効、ジョブ再実行、通知。
- [x] T-MVP-001からT-MVP-020を実装する。

受入条件:

- [x] MVP完了条件のACが通る。
- [x] 主要障害系がMockで確認済み。

検証:

- [x] `dotnet test`
- [x] `dotnet test --filter Category=Unit`
- [x] `dotnet test --filter Category=Integration`
- [x] `dotnet test --filter Category=Contract`

### ISSUE-MVP-018 MVP運用、監視、ドキュメントを整備する

参照ドキュメント: `docs/operations_runbook.md`, `docs/environment_setup.md`, `docs/test_plan.md`, `docs/api_examples.md`, `docs/implementation_notes.md`, `docs/basic_design.md`

関連: FR-140, NFR-008

目的: MVPを運用できる最低限の監視、Runbook、ドキュメント更新を行う。

範囲:

- [x] ダッシュボード、管理画面、管理APIでジョブ失敗、実行中/滞留ジョブ、402/403、クレジット消費量、通知失敗を確認できる導線を作る。
- [x] 429急増を管理画面またはメトリクスで明示的に確認できる導線を作る。
- [x] `job_success_rate`, `job_queue_depth`, `job_duration_p95`, `external_api_429_count`, `external_api_402_count`, `external_api_credit_consumed`, `notification_failure_count`, `retry_count_by_job_type` をメトリクス化する。
- [x] `/healthz`, `/readyz` の最小スモークテストを `scripts/smoke-test.sh` / `scripts/smoke-test.ps1` で実行できるようにする。
- [x] Runbookのスモークテストをプロジェクト一覧、監査ログ検索、マスタ同期、Discordテスト通知、CSV出力まで拡張する。
- [x] `docs/operations_runbook.md` に実装後の具体的な確認導線とコマンドを追記する。
- [x] `docs/test_plan.md` に正式なテストコマンドとテストDB起動手順を追記する。
- [x] `docs/api_examples.md` を実装済みレスポンスに合わせて更新する。
- [x] 仕様変更があれば正本文書とADR/implementation notesを更新する。

受入条件:

- [x] `/healthz`, `/readyz` のスモーク確認ができる。
- [x] プロジェクト一覧、監査ログ検索、マスタ同期、Discordテスト通知、CSV出力のスモーク確認ができる。
- [x] MVP運用手順の具体的な確認コマンドがRunbookに反映されている。

検証:

- [x] 包括Runbookスモークテスト

## Phase 2: SEO実務拡張

### ISSUE-P2-001 Phase 2 DB/API/外部API基盤を追加する

参照ドキュメント: `docs/requirements.md`, `docs/db_design.md`, `docs/api_design.md`, `docs/job_design.md`, `docs/external_api_design.md`, `docs/test_plan.md`

目的: 競合、コンテンツ、記事ブリーフ、順位監視に必要なテーブルと外部API連携を追加する。

範囲:

- [x] Phase 2テーブルを追加する: `competitor_sites`, `influx_keyword_results`, `influx_page_results`, `competitive_results`, `content_search_results`, `serp_headline_pages`, `serp_headlines`, `co_occurrence_words`, `co_occurrence_page_details`, `topic_clusters`, `cluster_keywords`, `article_briefs`, `artifact_versions`, `rank_check_jobs`, `rank_check_targets`, `rank_results`, `alerts`, `alert_events`。
- [x] Phase 2外部APIを連携する: influx-keywords, influx-pages, competitive, content-search, headline, co-occurrence, search-rank register/status/results。
- [x] Phase 2用インデックスを追加する。

受入条件:

- [x] Phase 2 migrationが通る。
- [x] Phase 2外部APIのContract testが通る。

### ISSUE-P2-002 競合分析と獲得語/ページ分析を実装する

参照ドキュメント: `docs/requirements.md`, `docs/api_design.md`, `docs/db_design.md`, `docs/job_design.md`, `docs/external_api_design.md`, `docs/screen_design.md`, `docs/test_plan.md`

関連: FR-050, FR-051, FR-052, AC-004

範囲:

- [x] `CompetitorRefreshJob` を実装する。
- [x] `GET /api/projects/{projectId}/competitors`
- [x] `POST /api/projects/{projectId}/competitors/analyze`
- [x] `GET /api/projects/{projectId}/influx-keywords`
- [x] `GET /api/projects/{projectId}/influx-pages`
- [x] 競合、獲得語、獲得ページ、ギャップを保存/表示できるようにする。

受入条件:

- [x] 対象ドメインから競合、獲得語、獲得ページ、ギャップを表示できる。

### ISSUE-P2-003 コンテンツ分析と記事ブリーフを実装する

参照ドキュメント: `docs/requirements.md`, `docs/api_design.md`, `docs/db_design.md`, `docs/job_design.md`, `docs/external_api_design.md`, `docs/screen_design.md`, `docs/test_plan.md`

関連: FR-040, FR-060, FR-061, FR-062, AC-005

範囲:

- [x] `ContentAnalyzeJob` を実装する。
- [x] `GenerateBriefJob` を実装する。
- [x] `GET /api/projects/{projectId}/content-analyses`
- [x] `POST /api/projects/{projectId}/content/analyze`
- [x] `GET /api/projects/{projectId}/briefs`
- [x] `POST /api/projects/{projectId}/briefs/generate`
- [x] `GET /api/projects/{projectId}/briefs/{briefId}`
- [x] `PUT /api/projects/{projectId}/briefs/{briefId}`
- [x] `GET /api/projects/{projectId}/briefs/{briefId}/versions`
- [x] `POST /api/projects/{projectId}/briefs/{briefId}/export`
- [x] 記事ブリーフの版履歴を `artifact_versions` に保存する。

受入条件:

- [x] 指定キーワードで集客コンテンツ、見出し、共起語を取得し、記事ブリーフを生成できる。

### ISSUE-P2-004 トピッククラスターを実装する

参照ドキュメント: `docs/requirements.md`, `docs/basic_design.md`, `docs/api_design.md`, `docs/db_design.md`, `docs/job_design.md`, `docs/screen_design.md`, `docs/domain_glossary.md`

関連: FR-031

範囲:

- [x] `TopicClusterGenerateJob` を実装する。
- [x] 同時ランクイン度、語彙類似度、検索意図、FAQでクラスタリングする。
- [x] `GET /api/projects/{projectId}/clusters`
- [x] `GET /api/projects/{projectId}/clusters/{clusterId}`
- [x] `POST /api/projects/{projectId}/clusters/generate`

受入条件:

- [x] クラスタ一覧、親子関係、代表語、記事候補、機会スコアを確認できる。

### ISSUE-P2-005 順位監視と順位アラートを実装する

参照ドキュメント: `docs/requirements.md`, `docs/api_design.md`, `docs/db_design.md`, `docs/job_design.md`, `docs/external_api_design.md`, `docs/screen_design.md`, `docs/test_plan.md`, `docs/operations_runbook.md`

関連: FR-080, FR-081, AC-006, AC-012

範囲:

- [x] `RegisterRankCheckJob` を実装する。
- [x] `PollRankStatusJob` を60秒間隔で再スケジュールする。
- [x] `FetchRankResultsJob` を実装する。
- [x] `RankAlertEvaluateJob` を実装する。
- [x] `POST /api/projects/{projectId}/rank-check/jobs`
- [x] `GET /api/projects/{projectId}/rank-check/jobs/{jobId}/results`
- [x] `GET /api/projects/{projectId}/rank-results`
- [x] `GET/POST /api/projects/{projectId}/alerts`
- [x] `PUT/DELETE /api/projects/{projectId}/alerts/{alertId}`
- [x] `POST /api/projects/{projectId}/alerts/{alertId}/enable`
- [x] `GET /api/projects/{projectId}/alert-events`
- [x] Discord通知 `rank_alert` を実装する。
- [x] Phase 2の順位監視は順位結果、順位分布、`alert_events`、`rank_alert`までを対象とし、カニバリ候補更新と月次レポート材料更新はPhase 3で扱う。

受入条件:

- [x] キーワードとURL/ドメインで順位チェックを登録し、結果、順位分布、アラートを確認できる。

### ISSUE-P2-006 Phase 2 UIを実装する

参照ドキュメント: `docs/screen_design.md`, `docs/api_design.md`, `docs/requirements.md`, `docs/basic_design.md`, `docs/adr/0002-blazor-web-app.md`

範囲:

- [x] S-040 トピッククラスターを実装する。
- [x] S-050 競合分析を実装する。
- [x] S-060 獲得キーワード/ページを実装する。
- [x] S-070 コンテンツ分析を実装する。
- [x] S-080 記事ブリーフのPhase 2範囲を実装する。
- [x] S-100 順位監視を実装する。
- [x] S-010 ダッシュボードに競合、コンテンツ、記事ブリーフ、順位指標を追加し、Phase 2用のdashboardレスポンス項目を返す。
- [x] S-900 管理にPhase 2の通知/ジョブ/監査導線を追加する。

受入条件:

- [x] Phase 2の主要業務フローが画面から操作できる。

### ISSUE-P2-007 Phase 2受入テストを整備する

参照ドキュメント: `docs/test_plan.md`, `docs/requirements.md`, `docs/api_design.md`, `docs/db_design.md`, `docs/job_design.md`, `docs/external_api_design.md`, `docs/screen_design.md`

範囲:

- [x] AC-004 競合分析のE2E/Integrationを通す。
- [x] AC-005 コンテンツ分析/記事ブリーフのE2E/Integrationを通す。
- [x] AC-006 順位監視/順位分布/`alert_events`/通知連携のE2E/Integrationを通す。
- [x] AC-012のPhase 2順位アラート通知を通す。
- [x] S-010のPhase 2ダッシュボードレスポンスと表示のIntegration/E2Eを通す。
- [x] 競合、コンテンツ、記事ブリーフ、順位監視の契約テストを通す。

受入条件:

- [x] Phase 2完了条件が通る。

## Phase 3: 自動化 / AI / 外部連携

### ISSUE-P3-001 Phase 3 DB/API基盤を追加する

参照ドキュメント: `docs/requirements.md`, `docs/db_design.md`, `docs/api_design.md`, `docs/basic_design.md`, `docs/external_api_design.md`, `docs/adr/0007-secret-store-and-audit.md`

目的: Phase 3後続Issueで使うDB、Contracts、共通サービスを先に追加し、業務処理と個別API本体はISSUE-P3-002以降で実装できる状態にする。

範囲:

- [x] Phase 3テーブルを追加する: `rewrite_tasks`, `cannibalization_candidates`, `reports`, `data_imports`, `external_connector_settings`, `external_connector_runs`, `ai_sessions`, `ai_messages`。
- [x] Phase 3 APIのContracts/DTO、ルートグループ、projectIdスコープ検証の土台を追加する。個別エンドポイント本体はISSUE-P3-002からISSUE-P3-006で実装する。
- [x] `IAiContentService` を定義する。
- [x] AIプロンプトからAPIキー、Webhook、認証情報、個人情報を除去する共通処理を実装する。
- [x] 共有URLのトークン生成、ハッシュ化、期限、失効、期限切れ、改ざん拒否の共通処理を実装する。

受入条件:

- [x] Phase 3 migrationが通る。
- [x] Phase 3 API土台がbuildで確認できる。
- [x] AIプロンプト秘匿処理のUnit/Security testが通る。
- [x] 共有URLトークンの有効、期限切れ、失効、改ざんケースを検証できる。
- [x] Secret実値を返さない設計が保たれる。

検証:

- [x] `dotnet build`
- [x] `dotnet test --filter Category=Unit`
- [x] `dotnet test --filter Category=Contract`
- [x] `dotnet test --filter Category=Integration`
- [x] `dotnet test --filter Category=Security`

### ISSUE-P3-002 リライト優先度とカニバリ検出を実装する

参照ドキュメント: `docs/requirements.md`, `docs/basic_design.md`, `docs/api_design.md`, `docs/db_design.md`, `docs/job_design.md`, `docs/screen_design.md`, `docs/test_plan.md`

関連: FR-070, FR-071, AC-018

範囲:

- [x] `RewriteScoringJob` を実装する。
- [x] `CannibalizationDetectionJob` を実装する。
- [x] `GET /api/projects/{projectId}/rewrite/tasks`
- [x] `GET /api/projects/{projectId}/rewrite/tasks/{taskId}`
- [x] `PUT /api/projects/{projectId}/rewrite/tasks/{taskId}` を実装し、`status`, `priority_score`, `assignee_actor`, `memo` を更新できるようにする。
- [x] `GET /api/projects/{projectId}/cannibalization/candidates`
- [x] `POST /api/projects/{projectId}/cannibalization/refresh`
- [x] P3-001で追加したContracts/DTO、ルートグループ、projectIdスコープ検証を使う。

受入条件:

- [x] リライト候補を優先度付きで確認できる。
- [x] 同一キーワードに複数URLがランクインする候補を検出し、根拠と推奨対応を確認できる。

### ISSUE-P3-003 レポート生成、共有URL、監査を実装する

参照ドキュメント: `docs/requirements.md`, `docs/api_design.md`, `docs/db_design.md`, `docs/job_design.md`, `docs/screen_design.md`, `docs/test_plan.md`, `docs/operations_runbook.md`

関連: FR-120, AC-007, AC-017

範囲:

- [x] `MonthlyReportJob` を実装する。
- [x] PDF/Excelレポート生成を実装する。
- [x] `POST /api/projects/{projectId}/reports`
- [x] `GET /api/projects/{projectId}/reports/{reportId}`
- [x] `GET /api/projects/{projectId}/reports/{reportId}/download`
- [x] `POST /api/projects/{projectId}/reports/{reportId}/share`
- [x] `DELETE /api/projects/{projectId}/reports/{reportId}/share`
- [x] `GET /api/report-shares/{token}`
- [x] 共有URLの発行、検証、期限切れ、失効、改ざん拒否はP3-001の共通処理を使う。
- [x] レポート完了通知 `report_completed` を実装する。

受入条件:

- [x] 月次レポートをPDF/Excelまたは共有URLとして出力できる。
- [x] ダウンロード、共有URL発行/失効/期限切れが監査される。

### ISSUE-P3-004 CSV/ExcelインポートとExcelエクスポートを実装する

参照ドキュメント: `docs/requirements.md`, `docs/api_design.md`, `docs/db_design.md`, `docs/job_design.md`, `docs/screen_design.md`, `docs/test_plan.md`

関連: FR-150, AC-016

範囲:

- [x] `DataImportJob` を実装する。
- [x] `POST /api/projects/{projectId}/exports` をCSV/Excel対応で実装する。
- [x] `POST /api/projects/{projectId}/imports/upload-url`
- [x] `POST /api/projects/{projectId}/imports`
- [x] `GET /api/projects/{projectId}/imports/{importId}`
- [x] `GET /api/projects/{projectId}/imports/{importId}/errors`
- [x] 検証エラーと取込履歴を保存する。
- [x] P3-001で追加したContracts/DTO、ルートグループ、projectIdスコープ検証を使う。

受入条件:

- [x] キーワード、順位、競合、ブリーフ、タスクをCSV/Excelで検証付き取込できる。

### ISSUE-P3-005 AIアシスタントを実装する

参照ドキュメント: `docs/requirements.md`, `docs/basic_design.md`, `docs/api_design.md`, `docs/db_design.md`, `docs/external_api_design.md`, `docs/screen_design.md`, `docs/test_plan.md`, `docs/adr/0007-secret-store-and-audit.md`

関連: FR-041, AC-015

範囲:

- [x] `AiAssistantJob` を実装する。
- [x] `POST /api/projects/{projectId}/ai/chat`
- [x] 自然言語から調査ジョブ、ブリーフ生成、差分分析、レポート要約を実行する。
- [x] AIプロンプト秘匿処理と `IAiContentService` はP3-001の共通処理/抽象を使う。
- [x] prompt、response、tool_calls、reference_data、token_usage、review_statusを保存する。
- [x] AI出力を人間レビュー前提の成果物として扱う。

受入条件:

- [x] AI生成で使用したプロンプト、参照データ、出力、実行者、token_usageを保存し、画面で確認できる。

### ISSUE-P3-006 外部連携スタブを実装する

参照ドキュメント: `docs/requirements.md`, `docs/api_design.md`, `docs/db_design.md`, `docs/external_api_design.md`, `docs/screen_design.md`, `docs/test_plan.md`, `docs/adr/0007-secret-store-and-audit.md`

関連: FR-130, AC-020

範囲:

- [x] `GET /api/projects/{projectId}/connectors`
- [x] `POST /api/projects/{projectId}/connectors`
- [x] `PUT /api/projects/{projectId}/connectors/{connectorId}`
- [x] `DELETE /api/projects/{projectId}/connectors/{connectorId}` を `status=disabled` 更新として実装する。
- [x] `POST /api/projects/{projectId}/connectors/{connectorId}/test`
- [x] `GET /api/projects/{projectId}/connectors/{connectorId}/runs`
- [x] GSC/GA4/CMS/BIの設定、Secret参照、接続テスト履歴を保存する。
- [x] 実データ取得は行わない。
- [x] P3-001で追加したContracts/DTO、ルートグループ、projectIdスコープ検証を使う。

受入条件:

- [x] コネクタ設定を作成/更新/無効化できる。
- [x] Secret実値を返さず、接続テストスタブと実行履歴を確認できる。

### ISSUE-P3-007 Phase 3 UIを実装する

参照ドキュメント: `docs/screen_design.md`, `docs/api_design.md`, `docs/requirements.md`, `docs/basic_design.md`, `docs/adr/0002-blazor-web-app.md`

範囲:

- [x] S-080にAI再生成を追加する。
- [x] S-090 リライト管理を実装する。
- [x] S-120 レポートを実装する。
- [x] S-130 AIアシスタントを実装する。
- [x] S-900 管理に外部連携スタブ設定と実行履歴を追加する。
- [x] S-010 ダッシュボードにリライト、カニバリ、レポート、AI関連指標を追加する。
- [x] P3-001で追加したPhase 3用Contracts/DTOとダッシュボードsummary項目を使う。

受入条件:

- [x] Phase 3の主要業務フローが画面から操作できる。

### ISSUE-P3-008 Phase 3受入テストを整備する

参照ドキュメント: `docs/test_plan.md`, `docs/requirements.md`, `docs/api_design.md`, `docs/db_design.md`, `docs/job_design.md`, `docs/external_api_design.md`, `docs/screen_design.md`

範囲:

- [x] P3-001のmigration、Phase 3 API土台build、AIプロンプト秘匿処理、共有URL共通処理のUnit/Security testを通す。
- [x] AC-007 レポート生成、ダウンロード、共有URL、期限切れ/失効制御を通す。
- [x] AC-015 AI prompt/response/reference/token_usage保存とレビュー状態を通す。
- [x] AC-016 CSV/Excelインポート、検証エラー、取込履歴を通す。
- [x] AC-017 レポート形式、file_uri、共有URL発行/失効/ダウンロード監査を通す。
- [x] AC-018 カニバリ候補、根拠データ、推奨対応を通す。
- [x] AC-020 外部連携スタブ、Secret非返却、接続テスト履歴を通す。

受入条件:

- [x] Phase 3完了条件が通る。

## 横断リファクタリング

### ISSUE-REF-001 コードベース保守性リファクタリングを実施する

参照ドキュメント: `docs/basic_design.md`, `docs/test_plan.md`, `docs/requirements.md`, `docs/screen_design.md`

関連: MVP(Phase 1相当)/Phase 2/Phase 3 実装済み機能全般。新規FR/ACは追加しない。

目的:

- [ ] MVP(Phase 1相当)/Phase 2/Phase 3完了後に大きくなったサービス、Blazor画面、DbContext、テストを、挙動を変えずに保守しやすい単位へ分割する。

範囲:

- [ ] `Admin.razor` など大きいBlazor画面をタブ/機能単位の子コンポーネントへ分割する。
- [ ] `DataTransferService`, `ContentAnalysisService`, `RankMonitoringService`, `TopicClusterService`, `AdministrationService`, `ReportService` など高リスクInfrastructureサービスを、既存Application契約を維持したまま責務別の内部協調クラスへ分割する。
- [ ] `SeoIntelligenceDbContext` のモデル設定を `IEntityTypeConfiguration<T>` など既存EF Core方針に沿う形で整理し、DBスキーマやmigrationは変更しない。
- [ ] `MvpServiceContracts` / `SeoIntelligenceApiClient` / 大きいIntegration/E2Eテストのfixtureやbuilderを、機能単位で読みやすく整理する。
- [ ] 既存テストの意味ある検証を維持し、テストを通すだけのハードコードや無意味なアサーションは追加しない。

範囲外:

- [ ] 新機能追加、API contract/URL/レスポンス形式変更、DB schema/migration変更、Secret/.env変更、外部API Real接続、本番依存追加は行わない。

受入条件:

- [ ] MVP(Phase 1相当)/Phase 2/Phase 3の既存機能、API契約、画面導線、DBスキーマが維持されている。
- [ ] `Admin.razor` と、上記InfrastructureサービスまたはDbContextのうち少なくとも1つが、責務名の分かる小さい単位へ分割されている。
- [ ] 分割後も代表BrowserE2Eと非Browserテストが通る。
- [ ] 変更理由と検証結果がIssue/PRに記録されている。

検証:

- [ ] `dotnet build SeoIntelligence.sln`
- [ ] `dotnet test --filter "Category!=BrowserE2E"`
- [ ] `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/smoke-local.ps1 -RunBrowserTests -SkipBuild -SkipMigration`

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
