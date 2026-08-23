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
- [x] ISSUE-REF-001 コードベース保守性リファクタリングを実施する
- [x] ISSUE-SEC-001 単一管理者ログインとAPIサービス認証を実装する
- [x] ISSUE-EXT-001 ラッコキーワードAPI v1.14.0へ追随する
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

- [x] MVP(Phase 1相当)/Phase 2/Phase 3完了後に大きくなったサービス、Blazor画面、DbContext、テストを、挙動を変えずに保守しやすい単位へ分割する。

範囲:

- [x] `Admin.razor` など大きいBlazor画面をタブ/機能単位の子コンポーネントへ分割する。
- [x] `DataTransferService`, `ContentAnalysisService`, `RankMonitoringService`, `TopicClusterService`, `AdministrationService`, `ReportService` など高リスクInfrastructureサービスを、既存Application契約を維持したまま責務別の内部協調クラスへ分割する。
- [x] `SeoIntelligenceDbContext` のモデル設定を `IEntityTypeConfiguration<T>` など既存EF Core方針に沿う形で整理し、DBスキーマやmigrationは変更しない。
- [x] `MvpServiceContracts` / `SeoIntelligenceApiClient` / 大きいIntegration/E2Eテストのfixtureやbuilderを、機能単位で読みやすく整理する。
- [x] 既存テストの意味ある検証を維持し、テストを通すだけのハードコードや無意味なアサーションは追加しない。

範囲外:

- [x] 新機能追加、API contract/URL/レスポンス形式変更、DB schema/migration変更、Secret/.env変更、外部API Real接続、本番依存追加は行わない。

受入条件:

- [x] MVP(Phase 1相当)/Phase 2/Phase 3の既存機能、API契約、画面導線、DBスキーマが維持されている。
- [x] `Admin.razor` と、上記InfrastructureサービスまたはDbContextのうち少なくとも1つが、責務名の分かる小さい単位へ分割されている。
- [x] 分割後も代表BrowserE2Eと非Browserテストが通る。
- [x] 変更理由と検証結果がIssue/PRに記録されている。

検証:

- [x] `dotnet build SeoIntelligence.sln`
- [x] `dotnet test --filter "Category!=BrowserE2E"`
- [x] `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/smoke-local.ps1 -RunBrowserTests -SkipBuild -SkipMigration`

実施メモ:

- `Admin.razor` の各タブ表示を `Admin*Panel.razor` 子コンポーネントへ分割し、API呼び出しと状態更新ロジックは親に維持した。
- `AdministrationService` のAPI認証情報監査書き込みを `AdministrationAuditRecorder` へ抽出し、既存Application契約を維持した。
- `SeoIntelligenceDbContext` の管理系MVPエンティティ設定を `IEntityTypeConfiguration<T>` 実装へ移し、migration/DBスキーマは変更していない。
- `SeoIntelligenceApiClient` の管理/運用系メソッドを partial ファイルへ分割し、公開インターフェースとURL/レスポンス契約は変更していない。
- 検証結果: `dotnet build SeoIntelligence.sln`、`dotnet test --filter "Category!=BrowserE2E"`、`scripts/smoke-local.ps1 -RunBrowserTests -SkipBuild -SkipMigration` が成功。

## 横断運用基盤

### ISSUE-OPS-001 Web/API/WorkerをDocker Composeで運用可能にする

参照ドキュメント: `docs/basic_design.md`, `docs/environment_setup.md`, `docs/operations_runbook.md`, `docs/docker_deployment.md`, `docs/test_plan.md`

目的:

- [x] VPSへ.NET SDKを直接導入せず、Web、API、Worker、PostgreSQL、RedisをDocker Composeで再現可能に起動・更新する。

範囲:

- [x] .NET 10のmulti-stage DockerfileでWeb/API/Workerを個別imageとしてbuildし、非rootユーザーで実行する。
- [x] ローカル用ComposeへWeb/API/Workerとone-shot Migrationを追加し、従来の依存サービスだけを起動する開発フローも維持する。
- [x] VPS用ComposeでDB/Redisのホストポートを公開せず、共通Caddy用の専用external networkへWeb/APIだけを接続する。
- [x] API/WorkerのLocal StorageとWebのData Protection keysをnamed Volumeへ永続化する。
- [x] MinIOを任意profileにし、現行adapterの対応範囲に合わせてLocal Storageを既定にする。
- [x] `.env`をbuild contextから除外し、VPS用Secret雛形、ログローテーション、認証ゲート、バックアップ注意点を文書化する。
- [x] CIでCompose構文、Web/API/Worker/Migration imageのbuild、隔離Volume上のコンテナ起動を検証する。

受入条件:

- [x] 開発用/VPS用Composeの`config --quiet`が成功する。
- [x] Web/API/Worker/Migrationの全targetがbuildできる。
- [x] 隔離したComposeスタックで3件のMigration、API Health/Readiness、DB利用API、Web表示が成功する。
- [x] API/Workerが同じStorage Volumeを参照し、WebのData Protection keysが再起動後も保持される。
- [x] Web/API/Workerが非rootで起動し、Worker単独再起動後も稼働する。

検証:

- [x] `docker compose -f compose.yaml config --quiet`
- [x] `docker compose --env-file .env.production.example -f compose.yaml -f compose.production.yaml config --quiet`（検証用`POSTGRES_PASSWORD`を環境変数で指定）
- [x] `docker compose -f compose.yaml build api web worker migrate`
- [x] 隔離Compose projectでMigration、Web/API/Worker起動、HTTP 200、Volume共有/永続化、非root実行を確認
- [x] `dotnet build SeoIntelligence.sln --configuration Release`
- [x] `dotnet test SeoIntelligence.sln --configuration Release --no-build --filter "Category!=BrowserE2E"`

レビュー反映（2026-07-12）:

- [x] `/readyz`が未適用Migrationを検知してunhealthyを返す（`InfrastructureReadinessProbe`。InMemoryプロバイダーではskip）。
- [x] 接続文字列をアプリ側で`Database__*`個別キーから組み立てる（`DatabaseConnectionStringResolver`、design-time factory対応、ContractTests追加）。Composeの手書きエスケープを廃止。
- [x] Composeをbase（`compose.yaml`）+開発overlay（`compose.override.yaml`）+VPS overlay（差分のみ、project name `seo-intelligence-prod`）へ再編。環境切替は`DOTNET_ENVIRONMENT`一本化。
- [x] api/webへコンテナhealthcheckを追加（runtime imageへcurl導入）し、webは`service_healthy`でゲート。`up -d --wait`をデプロイ完了シグナルとする。
- [x] Dockerfileを共有buildステージ+NuGetキャッシュmountへ再編（共有プロジェクトのコンパイルを1回に）。`migrate`をEF migration bundleの小型runtime imageへ変更。
- [x] CIのコンテナスモークを`scripts/container-smoke.sh`へ切り出し（隔離Compose project、リトライ関数統合、失敗時ログダンプ共通化）。image buildへGitHub Actionsレイヤーキャッシュ（docker/bake-action + type=gha）を導入。
- [x] WebのData Protection keysパスを`DataProtection__KeysPath`設定キーで指定可能にし、Composeのvolume targetと同一ソース化。
- [x] `smoke-local.ps1`のpg_isreadyをコンテナ内`$POSTGRES_USER`/`$POSTGRES_DB`参照へ修正。
- [x] VPS手順を`docker_deployment.md`へ一本化し、README/runbookは参照化（`chmod 600`のdrift解消）。

## 横断セキュリティ

### ISSUE-SEC-001 単一管理者ログインとAPIサービス認証を実装する

参照ドキュメント: `docs/basic_design.md`, `docs/api_design.md`, `docs/db_design.md`, `docs/docker_deployment.md`, `docs/operations_runbook.md`, `docs/environment_setup.md`, `docs/test_plan.md`, `docs/adr/0007-secret-store-and-audit.md`

関連: NFR-007, AC-014, AC-019。新規FRは追加しない。

目的:

- [x] `basic_design.md` と `api_design.md` が予告している「単一管理者ログイン + Cookie/BFF構成」をアプリ内に実装し、VPS公開時に外部認証ゲートだけへ依存している現状を解消する。

前提と方針:

- [x] 認証基盤はASP.NET Core Identityとし、同一VPSへ同居予定の `web-writing-tool` と実装方式を揃える。
- [x] `web-writing-tool` とは独立した認証とする。DB、Data Protectionキー、Cookie名、ホスト名を分離し、SSOは行わない。
- [x] 想定利用者は単一管理者1名。複数ユーザー管理画面、退会機能、リソース所有者ポリシーは `ISSUE-P4-001` の範囲とし本Issueでは実装しない。
- [x] 監査ログの操作主体は既存データ互換のため固定値 `developer`（`SystemActor.Developer`）を維持する。

範囲（Identity基盤）:

- [x] `SeoIntelligenceDbContext` を `IdentityDbContext<ApplicationUser, IdentityRole, string>` 継承へ変更する。
- [x] `ApplicationUser : IdentityUser` に `DisplayName`, `IsEnabled`, `LastLoginAt`, `CreatedAt`, `UpdatedAt` を追加する。
- [x] ロール定数 `Admin` / `User` を定義する。単一ユーザー運用では `Admin` のみ使用する。
- [x] Identityテーブル用のmigrationを1本追加する。既存業務テーブルのスキーマは変更しない。
- [x] `AdminSeedOptions` と `IdentityDataSeeder` を実装し、既存Adminが存在する場合はシードをスキップする。
- [x] パスワードポリシーを12文字以上、数字/大文字/小文字/記号必須にする。
- [x] ロックアウトを5回失敗/15分にする。

範囲（Web Cookie認証）:

- [x] Cookie認証を構成する。本番 `__Host-SeoIntelligence.Auth`、HttpOnly、`SecurePolicy=Always`、`SameSite=Lax`、8時間スライディング、`LoginPath=/login`、`AccessDeniedPath=/forbidden`。
- [x] `Login.razor` をSSRフォーム + `AntiforgeryToken` で実装し、`POST /login` へ送る。
- [x] ログイン時に `IsEnabled` を検証し、`lockoutOnFailure: true` でサインインし、`LastLoginAt` を更新する。
- [x] `returnUrl` のオープンリダイレクトを拒否する。
- [x] `POST /logout` を実装する。
- [x] 本人パスワード変更を実装する。
- [x] `AddCascadingAuthenticationState()` と `Routes.razor` の `AuthorizeRouteView` + `RedirectToLogin` を実装する。
- [x] 既存の全ページへ `@attribute [Authorize]` を付け、`/login` のみ `[AllowAnonymous]` にする。
- [x] CSRFトークン検証フィルタを実装する。
- [x] ログインとパスワード変更へレート制限を適用する。

範囲（APIサービス認証）:

- [x] `X-Service-Key` を検証する認証ハンドラーを実装し、定数時間比較を使う。
- [x] サービスキーはSecret Storeから取得し、実値をログ、レスポンス、監査ログへ出さない。
- [x] fallback policyで全エンドポイントを要認証にする。
- [x] 匿名許可は `/healthz`, `/readyz`, `GET /api/report-shares/{token}` のみとする。
- [x] 401を共通レスポンスエンベロープと共通エラー形式で返す。
- [x] Web側のAPIクライアントへ `X-Service-Key` を付与する `DelegatingHandler` を追加する。

範囲（運用・配備）:

- [x] 共通Caddyの公開面を縮小し、`/api/report-shares/*` だけを `seo-api` へ通し、それ以外の `/api/*` は公開しない。
- [x] `compose.yaml`, `compose.production.yaml`, `.env.production.example` へ認証関連の環境変数を追加する。
- [x] `scripts/smoke-test.ps1`, `scripts/smoke-test.sh`, `scripts/smoke-local.ps1`, `scripts/container-smoke.sh` をサービスキー付きで動作するよう更新する。

範囲（ドキュメント）:

- [x] `docs/adr/0008-aspnet-core-identity-auth.md` を追加する。
- [x] `basic_design.md` の「初期版はusers、roles、user_rolesを持たない」記述と認証/認可方針を更新する。
- [x] `api_design.md` の認証・認可章と401の扱いを更新する。
- [x] `db_design.md` へIdentityテーブルの扱いを追記する。
- [x] `docker_deployment.md` と `operations_runbook.md` の「アプリ内認証は未実装」記述を更新する。
- [x] `environment_setup.md` へ初期管理者シードとサービスキーの設定手順を追記する。
- [x] `test_plan.md` へ認証系テスト観点を追記する。

範囲外:

- [x] 複数ユーザー管理、RBAC拡張、SSO、承認フローは `ISSUE-P4-001` で扱う。
- [x] 退会機能、TOTP 2FAは本Issueでは実装しない。
- [x] 業務API契約、URL、レスポンス形式、既存業務テーブルのスキーマは変更しない。

受入条件:

- [x] 未認証でWebへアクセスすると `/login` へリダイレクトされる。
- [x] 初期管理者でログインでき、ログアウトできる。
- [x] パスワードポリシー違反とロックアウトが機能する。
- [x] サービスキーなしのAPI呼び出しが共通エラー形式の401になる。
- [x] `/healthz`, `/readyz`, `GET /api/report-shares/{token}` は匿名で到達できる。
- [x] パスワード、パスワードハッシュ、サービスキーの実値がレスポンス、ログ、監査ログに出ない。
- [x] 監査ログの操作主体が `developer` のまま維持される。
- [x] 既存の業務機能、API契約、画面導線、業務テーブルのスキーマが維持されている。

検証:

- [x] `dotnet build SeoIntelligence.sln`
- [x] `dotnet test --filter "Category!=BrowserE2E"`
- [x] `dotnet ef database update --project src/SeoIntelligence.Infrastructure --startup-project src/SeoIntelligence.Api`
- [x] `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/smoke-local.ps1 -RunBrowserTests -SkipBuild`

`-SkipMigration` は当初の想定コマンドに含めていたが、実際に成功したのは付けずに実行したもの。最終的な検証記録は本Issue末尾の「最終検証（2026-08-02）」を正とする。

実施メモ:

- Identityテーブルは既存 `SeoIntelligenceDbContext` を `IdentityDbContext<ApplicationUser, IdentityRole, string>` 継承へ変更し、migration `20260802064322_SingleAdminIdentity` を追加した。既存業務テーブルのスキーマは変更していない。テーブル名は本リポジトリのsnake_case規約に合わせ `identity_` 接頭辞とした。
- Web が管理者サインインのため `SeoIntelligence.Infrastructure` を参照する構成になった。`ProjectReferenceTests` の期待依存グラフを更新し、理由をコメントで残した。業務データは従来どおりAPI経由。
- Cookie設定、パスワードポリシー、ロックアウト、ログイン画面、CSRF、レート制限は `web-writing-tool` と同一方針に揃えた。退会機能とユーザー管理画面は単一利用者では不要のため実装していない。
- 認証は `web-writing-tool` と完全に独立している。DB、Data Protectionキー（`SetApplicationName`）、Cookie名、ホスト名を分離しSSOは行わない。
- ローカル実機検証: サービスキーなし401/正しいキー200/誤ったキー401、`/healthz`・`/readyz`・`GET /api/report-shares/{token}` の匿名到達、未サインイン時の `/login` リダイレクト、初期管理者シード、サインイン/ログアウト、CSRF欠落400、`returnUrl` オープンリダイレクト拒否、5回失敗ロックアウト、ロックアウト中の正しいパスワード拒否を確認した。
- 当初BrowserE2Eは未実施としていたが、後日Playwrightブラウザが導入済みであることを確認して実行し、完走した（末尾の「最終検証（2026-08-02）」を参照）。

レビュー反映（2026-08-02）:

- [x] `/Error` と `/not-found` に `[Authorize]` を追加した。両画面は共通レイアウト経由でプロジェクト名などを描画するため、匿名表示は業務データ露出になる。`/Error` は障害時にAPIへ依存しないよう `EmptyLayout` へ変更した。
- [x] ルート可能コンポーネントのうち匿名到達可能なのが `Login` だけであることを反射で検証するテストを追加した。
- [x] `IdentityDataSeeder` をfail-closedにした。Adminが0人かつ `AdminSeed` 未設定なら起動を失敗させる。Composeとcontainer-smokeへ既定値を追加した。（※このとき行ったVPS overlayでの `ADMIN_SEED_EMAIL` / `ADMIN_SEED_PASSWORD` 必須化は、2回目レビューで撤回した。下記「レビュー2回目の反映」を参照。）
- [x] シードを冪等にした。ロール付与前に落ちた場合は同一メールアドレスのユーザーを再利用してロール付与を再試行する。
- [x] 業務画面へ `RequireAdmin` ポリシーを適用した。`/account`（本人パスワード変更）、`/forbidden`、`/logout` は本人操作のため認証済みのみを要求する。Adminポリシーを課すと非Adminが自分のパスワードを変更できずログアウトもできなくなるため。
- [x] `GET /api/report-shares/{token}` の `AllowAnonymous` をグループからエンドポイントへ移した。
- [x] `requirements.md` の API認証・認証・認可・個人情報・FR-003 と `basic_design.md` の技術スタック表を実装へ整合させた。
- [x] Web認証のIntegrationテストを20件追加した（匿名許可ルート、Adminポリシー境界、サインイン/ログアウト、CSRF欠落・改ざん、ロックアウト、オープンリダイレクト、シードfail-closed、シード復旧）。APIの匿名許可メタデータが3件だけであることの検証も追加した。
- [x] 副次バグを修正した。`UseStatusCodePagesWithReExecute` が CSRF拒否の400を保護済み `/not-found` へ再実行し、サインインリダイレクトへ置き換えていた。アカウント系エンドポイントを対象外にした。
- [x] 再検証: `dotnet build`、`dotnet test --filter "Category!=BrowserE2E"`（212件成功）、Compose `config --quiet`（開発/VPS）、実機で `/Error`・`/not-found` を含む全ページの匿名リダイレクト、CSRF欠落400、シード未設定時の起動失敗を確認。

レビュー2回目の反映（2026-08-02）:

- [x] Production ComposeのAdminシードを`:?`必須から`${ADMIN_SEED_EMAIL:-}`へ変更した。初回サインイン後に資格情報を空にしても更新・再起動・Migrationが失敗しない。空値で明示的に上書きすることで、`compose.yaml`の開発用既定値が本番へ流入する経路も塞いでいる。
- [x] CIのCompose検証stepへ`API_SERVICE_KEY`を追加した。CIと同一コマンドで失敗を再現し、修正後の成功を確認した。前回はCIと同じコマンドを実行せず、必須変数を手で渡して検証していたのが原因。
- [x] 認可失敗の遷移先を認証状態で分岐させた。`RedirectToLogin` を `RedirectToSignInOrForbidden` へ置き換え、認証済みなら `/forbidden`、未認証なら `/login` へ送る。フルページ遷移はCookieの`AccessDeniedPath`が同じ分岐を行っており、本コンポーネントはBlazor回路内の遷移を担当する。
- [x] `/healthz` と `/readyz` に `HttpMethodMetadata` でGET制約を付け、`api_design.md` の契約と一致させた。匿名到達可能なエンドポイントの検証テストも `GET` 期待へ更新した。
- [x] `screen_design.md` の `/Error` レイアウト記述を実装（`EmptyLayout`）へ合わせ、認可失敗の遷移仕様を追記した。
- [x] テストを14件追加した（合計107件）。全業務ページのAdminポリシー網羅、Userロールでの`/forbidden`遷移、Userの`/account`・ログアウト利用、パスワード変更成功と新旧パスワードの入れ替わり、現在パスワード不一致・確認不一致・長さ不足・大文字なし・現在と同一の5種、`/logout`と`/account/password`のCSRF欠落・改ざん、資格情報を空にした既存DBでの再起動。
- [x] 再検証: `dotnet build`、`dotnet test --filter "Category!=BrowserE2E"`（226件成功）、CIと同一のCompose検証コマンド、`git diff --check`。

レビュー3回目の反映（2026-08-02）:

- [x] `/account` と `/forbidden` を `SelfServiceLayout` へ変更した。両画面はレイアウト未指定のため `MainLayout` が適用され、`ProjectSwitcher`（プロジェクト一覧取得）、`CreditBadge`（クレジット集計取得）、`LocationLanguageSelector`（`UpdateProjectAsync` によるプロジェクト設定更新）が動作していた。Web→APIは共有サービスキー認証でロールを判別できないため、非Adminがこれらの画面から業務データを参照・更新できる権限昇格だった。
- [x] 非Adminで `/account`・`/forbidden` を開いたとき、業務コンポーネントが描画されず業務APIも1件も呼ばれないことを検証するテストを追加した。テストファクトリへAPI呼び出し記録用のハンドラーを追加している。レイアウト指定を外すとこのテストが失敗することも確認した。
- [x] `scripts/verify-production-compose.sh` を追加し、CIのCompose検証stepから実行するようにした。`ADMIN_SEED_*` 未指定時に本番レンダリング結果の `AdminSeed__Email` / `AdminSeed__Password` が空であることをアサートする。`compose.production.yaml` の上書きを削除すると検知して失敗することを確認した。
- [x] `todo.md` のAdminSeed必須化に関する旧記述へ、2回目レビューで撤回した旨を追記した。
- [x] `screen_design.md` と ADR 0008 へ、セルフサービス画面が専用レイアウトを使う理由（サービスキー認証ではAPI側でロールを判別できない）を追記した。
- [x] 再検証: `dotnet build`、`dotnet test --filter "Category!=BrowserE2E"`（Unit 50 / Contract 59 / E2E 10 / Integration 109 = 228件成功）、`bash scripts/verify-production-compose.sh`、CIと同一のCompose検証コマンド、`git diff --check`。

レビュー4回目の反映（2026-08-02）:

- [x] `/forbidden` の「戻る」リンクと `SelfServiceLayout` のブランドリンクを、`RequireAdmin` ポリシーの有無で切り替えるようにした。従来はどちらも `/` を指しており、`/` は `RequireAdmin` のため非Adminがクリックすると `/forbidden` へ戻るループになっていた。非Adminには `/account` を提示する。
- [x] 業務コンポーネント非描画・業務API非呼び出しのテストへ `/Error` を追加した。ADRが `RequireAdmin` を持たない画面として `/account`、`/forbidden`、`/Error` の3つを挙げているため、将来 `EmptyLayout` 指定が外れた場合も検知できるようにした。
- [x] 非Adminへ到達不能なリンクを出さないこと、Adminには `/` を提示することのテストを追加した。分岐を戻すとテストが失敗することも確認した。
- [x] 前回の記述「229件成功」を実測値へ訂正した。実際は228件で、単純な計上誤り。
- [x] 再検証: `dotnet build`、`dotnet test --filter "Category!=BrowserE2E"`（Unit 50 / Contract 59 / E2E 10 / Integration 112 = 231件成功）、`bash scripts/verify-production-compose.sh`、CIと同一のCompose検証コマンド、`git diff --check`。

レビュー5回目の反映（2026-08-02）:

- [x] リンク分岐テストの偽陽性を解消した。`/forbidden` にはレイアウトのブランドリンク、アカウントリンク、ページ本体の戻るリンクが並ぶため、全アンカーの集合に対する検証では1つの正しいリンクが別の誤りを隠せていた。`self-service-brand-link` と `forbidden-back-link` の `data-testid` を付け、Admin／非Adminそれぞれで各リンクの `href` を個別に検証する。
- [x] 偽陽性シナリオでの検知を確認した。(1) ブランドリンクを正しいまま戻るリンクだけ `/account` に変更 → 失敗、(2) 戻るリンクを丸ごと削除 → Admin・非Admin両テストが失敗。
- [x] 再検証: `dotnet build`、`dotnet test --filter "Category!=BrowserE2E"`（Unit 50 / Contract 59 / E2E 10 / Integration 112 = 231件成功）、`bash scripts/verify-production-compose.sh`、CIと同一のCompose検証コマンド、`git diff --check`。

BrowserE2Eと回路内遷移の検証（2026-08-02）:

- [x] `RedirectToSignInOrForbidden` のBlazor回路内遷移を実機確認した。`User` ロールでサインインし、回路内クリックで `RequireAdmin` 画面へ遷移させると `/forbidden` へ送られる（`/login` ではない）。フルページ遷移との判別は遷移先で行える。フルページは Cookie の `AccessDeniedPath` により `/forbidden?ReturnUrl=%2Fdashboard`、回路内は本コンポーネントによりクエリなしの `/forbidden` になる。
- [x] BrowserE2E が完走した（`smoke-local.ps1 -RunBrowserTests -SkipBuild`）。サインイン、プロジェクト選択、キーワード探索、検索ボリューム、管理APIキー、順位監視、レポートの全フローが通過。

検索ボリューム既存バグの修正（2026-08-02、ユーザー指示により本Issueに含める）:

- [x] `POST /api/projects/{projectId}/search-volume/jobs` が必ず500になる既存バグを修正した。`main`（294a4ad）を別worktreeでビルドして同一リクエストを投げ、同じ500と同じ例外が出ることを確認済みで、認証追加に起因しないバグだった。
- [x] 原因は `MasterNameEntry` への positional constructor 射影の後にその射影結果へフィルタを適用していたこと。EF Core はコンストラクタ越しにメンバーを列へ戻せず、SQLへ変換できなかった。object initializer（member-init）射影へ変更して解消した。
- [x] クエリ組み立てを `MasterNameQuery` へ抽出し、述語も含めて1箇所に集約した。
- [x] ContractTests に変換可否をピン留めするテストを4件追加した。`ToQueryString()` はDB接続を開かずにクエリをコンパイルするため、依存サービスなしで変換失敗を検出できる。既存のAPI Integrationテストは接続文字列なしで動くため relational provider を経由せず、このバグを検出できなかった。
- [x] 修正前のクエリ形が実際に変換不能であることを一時テストで確認してから削除した。
- [x] 未同期マスタ判定（`entries.AnyAsync()`）は実PostgreSQLで確認した。未知のlocationを指定すると全ルックアップを通過して`AnyAsync`へ到達し、`SELECT EXISTS (SELECT 1 FROM locations ...)` が発行されて400（`location must be a name from the synchronized location master data.`）が返る。変換エラーは発生しない。
- [x] `AnyAsync` は終端演算子のため `ToQueryString()` では固定できない。自動テストは未整備であり、テストファイルのコメントに明記した。relational providerを使うテスト基盤（Testcontainers等）を入れる際に対象とする。

最終検証（2026-08-02）:

- [x] `dotnet build SeoIntelligence.sln`（警告0、エラー0）
- [x] `dotnet test --filter "Category!=BrowserE2E"`（Unit 50 / Contract 63 / E2E 10 / Integration 112 = 235件成功）
- [x] `scripts/smoke-local.ps1 -RunBrowserTests -SkipBuild`（BrowserE2E 1件成功、Comprehensive local smoke test succeeded）
- [x] `bash scripts/verify-production-compose.sh`
- [x] CIと同一のCompose検証コマンド
- [x] `git diff --check`

補足:

- スモークの Discord テスト通知は任意ステップで、ローカル `.env` の `SMOKE_DISCORD_CHANNEL_ID` が現在のDBに存在しないため除外した。Secret参照が設定済みの環境でのみ実行する既存仕様どおり。

### ISSUE-EXT-001 ラッコキーワードAPI v1.14.0へ追随する

参照ドキュメント: `docs/rakko-keyword-api-docs.json`, `docs/rakko-keyword-api-docs.md`, `docs/external_api_design.md`, `docs/api_design.md`, `docs/db_design.md`, `docs/adr/0006-openapi-dto-generation.md`

関連: FR-010, FR-011, AC-001

目的: ラッコキーワードAPIのvendor仕様がv1.12.0からv1.14.0へ更新されたため、生成DTO、クライアント、永続化、設計ドキュメントを追随させる。

vendor仕様の構造差分(説明文を除く16件):

- よくある質問検索: `filter`(質問文/相対需要/出現時期)、`sortBy`(relativeDemand|firstSeenRange)、`orderBy`を追加。`limit`上限が200→1,000。返却順が出現頻度順→相対需要順。消費クレジットが3→1.5。
- よくある質問検索レスポンス: `items[].metrics`(`relativeDemand` 1〜100、`firstSeenRange` enum|null)がrequiredに追加。`data.query`のrequiredに`sortBy`/`orderBy`/`limit`が追加。
- 検索順位チェック結果: `items[].entryNo`がrequiredに追加。
- 新規エンドポイント `GET /v1/search-rank/{requestId}/results/{entryNo}/serp` と `SearchRankSerpCacheResponseDto`(クレジット消費なし)。
- `info.version` 1.12.0 → 1.14.0。

範囲:

- [x] `docs/rakko-keyword-api-docs.md`(vendor提供のMarkdown版)をリポジトリの追跡対象に加える。
- [x] `QuestionItemDto.metrics` と `QuestionMetricsDto` を生成DTOへ追加する。
- [x] `SearchQuestionDto` に `filter` / `sortBy` / `orderBy` を追加する。
- [x] `SearchRankSerpCacheResponseDto` を生成DTOと生成スクリプトの必須スキーマ一覧へ追加する。
- [x] `scripts/generate-rakko-keyword-dtos.ps1` で `OpenApiVersion` / `SourceSha256` を更新する。
- [x] `RakkoQuestion` に相対需要/出現時期、`RakkoQuestionSearchRequest` にソート/フィルタ、`RakkoExternalSearchResultItem` に `EntryNo` を追加する。
- [x] `IRakkoKeywordClient.GetSearchRankSerpAsync` をReal/Mock双方に実装する。
- [x] `questions.first_seen_range` と `rank_results.entry_no` を追加するmigrationを作成する。
- [x] 相対需要(1〜100)を0〜1へ正規化して `questions.importance` に保存する(相対需要が無い場合は既定値0.5)。
- [x] 検索順位チェック結果の `entryNo` を `rank_results.entry_no` へ保存する。
- [x] 設計ドキュメントの版表記、外部APIマッピング、エンドポイント表、クレジット料率、DB列を更新する。

受入条件:

- [x] `RakkoKeywordDtoShapeContractTests` と `GeneratedDtoMetadataMatchesVendorOpenApiSpec` が通る。
- [x] SERP詳細取得がMockクライアントで成功し、消費クレジットが0で記録される。
- [x] 質問の相対需要と出現時期がDBへ保存される。
- [x] 順位チェック結果の `entryNo` がDBへ保存される。
- [x] 契約スコープ(`scope_key`)の世代交代は行わない(プラン・データ利用範囲・検索指標の意味論が不変のため)。

検証:

- [x] `powershell -File scripts/generate-rakko-keyword-dtos.ps1 -ValidateOnly`
- [x] `dotnet build`
- [x] `dotnet test --filter Category=Contract`
- [x] `dotnet test --filter Category=Integration`

レビュー指摘への対応(2026-08-17):

- [x] 監査(`external_api_calls`)とrequest hashへ実リクエストpathを含める。テンプレートendpointだけを記録していたため、`requestId`/`entryNo`違いの呼び出しが同一ハッシュになり対象を特定できなかった。SERP取得だけでなく、search-volume status/results、search-rank status/resultsの既存4エンドポイントにも同じ問題があったため一括で修正した。
- [x] Real clientを通したSERP契約テストを追加する(HTTP GETのpath、`X-API-Key`、`X-Correlation-Id`、レスポンス変換、監査pathを検証)。
- [x] `ToEntryNo` の境界テスト(非整数、0、負数、`int.MaxValue`超過)を追加する。
- [x] 正本ドキュメントの旧版表記(`requirements.md`/`basic_design.md`/`external_api_design.md`のヘッダーと付録)、`SearchQuestionDto.limit`の旧上限200、エンドポイント一覧のSERP GET欠落を修正する。
- [x] `basic_design.md`付録の外部DTO制約表に残っていた`SearchQuestionDto`の`limit: 1〜200`を1〜1,000へ修正し、`filter`/`sortBy`/`orderBy`を追記する。付録表の全DTOのlimitを仕様JSONと機械照合し、他に差分がないことを確認済み。

補足:

- 内部API `/keyword-discovery/suggest` の `limit` 上限は1〜100のまま据え置いた。vendorの上限拡大(1,000)は上限のみの変更で、内部契約を変える必要がないため。
- SERP詳細の業務テーブルへの取込(見出し/競合分析への活用)は本Issueの範囲外とし、クライアント層までの実装に留めた。

### ISSUE-FIX-001 成果物をブラウザからダウンロードできるようにする

参照ドキュメント: `docs/api_design.md`, `docs/requirements.md`, `docs/screen_design.md`, `docs/docker_deployment.md`

関連: FR-120, FR-121, AC-007, AC-013

背景:

- `.../download` が `storage://local/...` を返すだけで、どのHTTPクライアントも解決できなかった。CSV/Excel/PDFの生成と監査は成功する一方、通常利用者がファイルを取得できず、MVPのAC-013とPhase 3のAC-007を満たしていなかった。
- `docker_deployment.md` は「HTTP adapterは別Issue」と記していたが、該当Issueは存在しなかった。VPSデプロイ前レビューで検出し、本Issueとして起票した。

目的:

- [x] 生成済みのCSV/Excel/PDF/Markdownを、ブラウザから認証付きで取得できるようにする。

範囲:

- [x] API に `.../exports/{exportId}/content` と `.../reports/{reportId}/content` を追加し、`IObjectStorage.OpenReadAsync` の内容をストリーミング配信する。
- [x] 共有URL経由の `/api/report-shares/{token}/content` を追加し、共有トークンを再検証したうえで匿名配信する。
- [x] `.../download` の戻り値を `storage://` から到達可能なAPIパスへ変更する。
- [x] Webホストへ `/downloads/projects/{projectId}/exports|reports/{id}` を追加し、管理者Cookieで認可してサービスキー付きでAPIを呼び中継する。
- [x] 監査を発行と取得へ分離する。`*_url_issued` は `download`、`*_downloaded` は `content` が記録し、`via` で経路を残す。
- [x] `Reports.razor` の壊れたリンクを差し替え、`JobProgressPanel` へ完了ジョブのダウンロード導線を追加する。

受入条件:

- [x] `download` が `/api/projects/{projectId}/exports|reports/{id}/content` を返す。
- [x] `content` が200、正しい`Content-Type`、`Content-Disposition: attachment`、Storage上の内容と一致する本体を返す。
- [x] 未完了のexportは409、他プロジェクトのIDでは404になる。
- [x] 共有URLの `content` はサービスキーなしで200を返し、失効後は`Gone`になる。
- [x] Webの `/downloads/...` は未サインインを`/login`、非Adminを`/forbidden`へ送り、いずれもAPIを呼ばない。
- [x] APIのステータスコードがWeb経由でも保たれる（存在しないexportは404のまま）。
- [x] 匿名エンドポイントは4件（`/healthz`、`/readyz`、share 2件）に限定され、Security testで固定される。

検証:

- [x] `dotnet build SeoIntelligence.sln -c Release -warnaserror`
- [x] `dotnet test SeoIntelligence.sln -c Release`（BrowserE2Eを除く258件成功）
- [x] `bash scripts/container-smoke.sh`

補足:

- `article_brief_export` も `data_exports` に格納されるため同じ経路で配信できる。`markdown` 形式の拡張子とContent-Typeを追加した。
- ファイル本体は常にAPI経由で配信し、Storage Volumeは公開しない。MinIO署名付きURLは引き続き未対応。

レビュー指摘による追加是正:

- [x] `expiresAt` を `DataExportDownload` / `ReportDownload` から削除した。返すURLは認証必須のAPIパスであり、署名付きの期限付きURLではない。期限を持たせても毎リクエストの認証がアクセス制御であるため何も制御せず、旧実装では実際に未強制のまま表示だけされていた。期限が意味を持つ共有URLは、共有トークンの `share_expires_at` / `share_revoked_at` を毎回検証して強制する（実装済み・テスト済み）。
- [x] `ReportShareAccessDetails.DownloadExpiresAt` を、無関係な15分値から共有トークンの実際の期限へ変更した。受信者へ強制されない期限を示さない。
- [x] `*_downloaded` 監査をStorageのオープン成功後に移した。存在確認後の削除や権限エラーで「ダウンロード済み」が残らない。監査保存に失敗した場合はストリームを破棄する。
- [x] OpenAPIで `content` 系の200応答を `type: string, format: binary` として公開し、エラー応答のみ共通エンベロープにした。JSONとして公開したままでは生成クライアントが壊れる。
- [x] Trivyを `scripts/scan-container-images.sh` へ集約し、`postgres`/`redis` をゲート対象にした。除外はCVE ID列挙ではなくコンポーネント（`usr/local/bin/gosu` の `stdlib`）で指定し、同イメージの別箇所の新規CVEは検出できる形にした。MinIOは本番非使用のため報告のみ。
- [x] `basic_design.md` / `api_design.md` / `domain_glossary.md` / `mvp_implementation_plan.md` の「短時間URL」記述を実装に合わせた。
- [x] `JobProgressPanel` のリンク生成を `ArtifactDownloadLinks.ForJob` へ抽出し、成功/未完了/対象外リソースの分岐をテストで固定した。

判断の記録:

- `*_url_issued` は `download` を呼んだ事実の記録に留め、取得の正本は `*_downloaded` とした。画面のジョブ一覧は `download` を経由せず直接ダウンロードへ遷移するため対で残らないが、実取得は経路によらず必ず記録される。

2次レビューによる追加是正:

- [x] 共有トークンが `audit_logs.before_after_json` へ平文で入る退行を修正した。`report.share_accessed` にはトークンを含む解決済みURLではなくルートテンプレートを保存する。既存の `AssertTokenIsStoredOnlyAsHashAsync` は共有アクセス**前**に呼ばれていたため素通りしていた。アクセス後にも検査するよう追加した。
- [x] APIのHTTPログが `Request.Path` をそのまま記録していたため、共有トークンが全ログシンクへ出ていた。`RouteEndpoint.RoutePattern.RawText` へ変更し、未マッチ要求はパスを記録しない（攻撃者制御かつトークンが現れ得る箇所のため）。
- [x] 匿名共有2経路へレート制限を追加した（IP単位30回/分 + 全体同時実行8）。未知トークンでもDB照会と監査書込が発生するため。認証済みエンドポイントへ波及しないことをテストで固定した。
- [x] TrivyへDockerソケットを渡すのをやめた。`docker save` したtarを `--input` で読ませ、スキャナimageはdigestで固定した。ソケットを渡すとスキャナがDockerデーモンを操作でき、開発PCとCI runnerに対してroot相当になる。
- [x] Trivyの受容をコンポーネント単位からCVE単位（image+CVE+target+package の4項目一致）へ変更した。コンポーネント一括では同じバイナリに後から出た到達可能な脆弱性まで隠れる。受容CVEを1件外すとゲートが発火することを確認済み。
- [x] 受容判断時のdigestを記録し、スキャン時に照合するようにした。タグが動いていればCIが失敗し、判断のやり直しを強制する。発火を確認済み。
- [x] 共有endpointの410（期限切れ/失効）と429（レート制限）をOpenAPIへ追加した。認証済みendpointには付かないことも併せて固定した。
- [x] ファイル消失テストがリクエスト前に削除しており `ExistsAsync` で終了していた。Exists=true かつ OpenReadAsync が例外を投げるfake storageへ置き換えた。監査をオープン前へ戻すと落ちることを確認済み。
- [x] `job_design.md` / `api_design.md` / `basic_design.md` の残存記述（短時間URL、`expiresAt`、API一覧、匿名endpoint数）を実装に合わせた。
- [x] Caddyのcatch-allでWebの `/readyz` は外部到達することを明記し、非公開にすべきなのはAPIの `/readyz` だと書き分けた。

3次レビューによる追加是正:

- [x] `compose.yaml` の `postgres` / `redis` をdigest固定した。スキャンをdigestで行っても、VPSがタグでpullすれば別イメージが起動し得るため、鎖の最後が抜けていた。バックアップ手順の一時コンテナも同じ参照へ揃えた。
- [x] digestが `compose.yaml` / `RUNTIME_REVIEWED_DIGESTS` / Runbookの3箇所で一致することを、`verify-production-compose.sh` と `scan-container-images.sh` の両方で検証するようにした。固定を外すと両方が失敗することを確認済み。
- [x] 429が空ボディだった点を `OnRejected` で共通エンベロープ（`RateLimit.Exceeded`）＋`Retry-After` へ修正した。OpenAPIの公開内容と実応答が一致していなかった。
- [x] Trivy JSON評価が `Results` 欠落を「検出0件」として成功させていた（fail-open）。スキーマを検証してfail-closedにした。
- [x] `requirements.md` / `test_plan.md` / ADR 0008 の匿名許可一覧を4件へ更新した。CIコメントの「コンポーネント単位の除外」という旧説明も実装（CVE単位）に合わせた。
- [x] `Phase3ApplicationContractTests` の共有URLサンプルを新契約へ更新した。
- [x] Caddyの `log_skip @report_shares` を「必須」としてデプロイ手順へ明記し、確認コマンドを添えた。アプリ側を直しても前段がURIを記録すれば漏えいは成立するため。

追加したテスト:

- [x] レート制限の正確な境界（30件目まで通し31件目で429）を、メタデータ経路と `/content` 経路の両方で固定した。
- [x] 429が共通エンベロープを返すことを固定した。
- [x] 共有トークンが**アプリケーションログ**に出ないことを、ログプロバイダーを差し替えて全ログ行に対し検証した。ルートテンプレートは記録されることも併せて確認し、検査対象が正しいことを保証した。生パスのログへ戻すと落ちることを確認済み。

4次レビューによる追加是正:

- [x] デプロイ手順の `docker compose ... build` がサービス名を省いており、`tools` profile配下の `migrate` がビルド対象から外れていた。dry-runで実測して確認。初回はmigrate image不在、更新時は前リリースのbundleでMigrationを実行し得る実バグ。初回・更新の両手順を `build api web worker migrate` へ修正し、手順書のbuildコマンドがサービス名を明示しているかを `verify-production-compose.sh` が検証する（スモークスクリプトは元から明示していたため、手順書とスクリプトが乖離していた）。
- [x] CIが検査したアプリimageと本番が起動するimageが同一である保証がない（VPSは同一コミットから再buildし、base image・apt・NuGetはいずれも可変）。`scripts/scan-container-images.sh app` を初回・更新の両手順へ**buildの直後・起動の前**の必須ステップとして追加した。CIスキャンの限界と、registry経由でdigestを固定する強い代替案も手順書へ明記した。registry導入自体は構成変更のため実施していない。
- [x] digestの「3箇所一致」ガードが実質2点しか見ておらず、grepのためコメント行でも通り得た。`image-digests.lock` を単一の正本にし、`verify-production-compose.sh` がComposeの**レンダリング結果**と完全一致で照合する形へ変更した。Runbookは値を複製せず同ファイルを参照する。lock側を書き換えると失敗することを確認済み。
- [x] Caddyログ確認手順がfalse-passし得た（file/network sink未考慮、Base64URLの `-`/`_` に不一致な正規表現、有効トークンの使用）。一意な無効プローブ文字列でアクセスし、全sinkを `grep -F` の完全一致で検索する手順へ書き換えた。
- [x] `BothAnonymousShareEndpointsStopAtTheThirtieth...` を実際の挙動に合わせ `AllowThirtyRequestsAndRejectTheThirtyFirst` へ改名した。
- [x] 429テストに `Content-Type`、`Retry-After`、`requestId` の検証を追加した。

追加したテスト:

- [x] 同時実行8の上限を、`TaskCompletionSource` でStorage読み取りをブロックする決定的な形で固定した（sleep不使用）。8件を到達させてから9件目の429を確認し、最後に解放する。上限を64へ上げると落ちることを確認済み。
- [x] IP partitionロジックをローカルで固定した（異なるIPは別partition、同一IPは同一partition、アドレス不明は単一partitionへ集約して制限を回避させない）。レート制限対象がルートパターン一致で共有2経路だけであることも併せて固定した。

5次レビューによる追加是正:

- [x] lock照合がservice→imageの対応を固定しておらず、postgresとredisのimageを入れ替えても通っていた。lock形式へCompose service名を加え、レンダリング結果と**辞書全体を完全一致**で比較する形へ変更した。入れ替えを模擬すると失敗することを確認済み。
- [x] デプロイ手順のガードが「不正な行の検出」だったため、行を削除すると通っていた。4サービスbuildとappスキャンが**初回・更新で各2件存在すること**を件数で検証する形へ変更した。削除を模擬すると失敗することを確認済み。
- [x] 同時実行429に`Retry-After`が付かない点を契約として確定した。同時実行制限は権限がいつ空くかを提示できず、値を捏造するのは誤りであるため、**`Retry-After`は任意**とした。固定窓429では付与、同時実行429では非付与を、それぞれテストで固定し、`api_design.md`と`test_plan.md`へ明記した。
- [x] 同時実行テストを `AcrossAllCallers` へ改名した（TestServer上では異なるremote IPを表現していないため）。
- [x] 9件目がStorageへ到達した場合に即失敗するよう、リクエストと到達シグナルを`Task.WhenAny`で競合させる形にした。上限を退行させてもHttpClient timeoutを待たずに落ちる。
- [x] `scan-container-images.sh` に残っていた、廃止済み`RUNTIME_REVIEWED_DIGESTS`への追加を案内するエラー文言を`image-digests.lock`参照へ修正した。

6次レビューによる追加是正:

- [x] 比較対象serviceをlockファイルから導出していたため、lockから行を削除すると両辺から消えて通っていた。必須service集合（`postgres`/`redis`）をコード側に明示し、その集合で比較する形へ変更した。
- [x] コマンド件数の照合が`grep -cF`の部分一致だったため、コメントアウトしても件数に含まれていた。`grep -cxF`の行全体一致へ変更し、実行行の完全形で照合するようにした。
- [x] `NoMoreThanEightShareDownloadsRunAtOnce` へ改名し（異なるremote IPは表現していないため）、匿名HttpClientを1つに集約して破棄するようにした。

追加したテスト:

- [x] `scripts/verify-deployment-guards.sh` を追加し、デプロイ用ガード自体の退行を検出するようにした。lockからのservice削除、digest不一致、appスキャンのコメントアウト、appスキャンの片方削除、buildのコメントアウト、buildのサービス名不足の6ケースすべてで検証が失敗することを確認する。改変はコピー上で行い、リポジトリを変更しない。CIの「Validate Compose files」へ組み込んだ。
- ガードのパスは`DEPLOYMENT_DOC` / `DIGEST_LOCK_FILE`環境変数で差し替え可能にし、この退行テストから改変済みコピーを指せるようにした。

7次レビューによる追加是正:

- [x] コマンド件数の検証が文書全体の合計だったため、初回手順へ2件集約し更新手順から削除すると通っていた。実測で再現（合計2件・更新0件）したうえで、**初回・更新の各節で1件ずつ**を検証する形へ変更した。更新こそstale migrate imageの被害が出る経路である。
- [x] ガード退行テストの作業ディレクトリを一意化した（固定パスは既存成果物を消し、並列実行同士が競合する）。`/tmp`ではなく`artifacts/`配下に置くのは、検証スクリプトがlockパスをPythonへ渡し、Git Bash上ではWindows PythonがMSYSパスを開けないため。
- [x] 「初回側へ2件集約、更新側0件」の退行ケースを追加し、退行テストを7ケースへ拡張した。

8次レビューによる追加是正:

- [x] appスキャンの「存在」は検証していたが「順序」を検証していなかった。起動後へ移動しても通っていた。**build → scan → 起動**の順序を初回・更新の両節で検証する形へ変更した。起動後に走るスキャンは、既にトラフィックを受けているimageを報告するだけで統制にならない。
- [x] 「各節に1件ずつあるが起動後に実行される」退行ケースを追加し、退行テストを8ケースへ拡張した。存在チェックは通り順序で落ちることを実測で確認済み。

9次レビューによる追加是正:

- [x] 起動判定が`up -d`という**表記**に依存しており、`up --detach`へ書き換えると順序違反を検出できなかった。加えて、手順をシェルへ貼り付けた場合スキャンが非0でも次の`up`が実行され、順序は正しくても**ゲートとして機能していなかった**。
- [x] デプロイ手順を`scripts/deploy-production.sh <initial|update>`へ移した。`set -euo pipefail`により、スキャン失敗でデプロイが中断する。順序と中断保証を文書の記述ではなく実行可能なコードへ移したことで、両方の指摘が同時に解消する。
- [x] 更新モードは`--backup-confirmed`を必須にした。5.1のバックアップ取得を明示しない限り実行を拒否する。Migration失敗時の戻り先がなくなるため。
- [x] 起動判定を`up|run|start|restart`のサブコマンドで行う形に変え、表記依存を解消した。
- [x] `verify-production-compose.sh`の`line_number_of`が`pipefail`により未検出時に無言で中断していた（エラーメッセージを出す前にスクリプトが終了）。`|| true`を追加して修正した。退行テストの文言照合がこれを検出した。
- [x] デプロイスクリプトのモード検証を環境ファイル検証より前に移し、引数なし実行でusageが出るようにした。

退行テストの拡張（12ケース）:

- [x] `expect_failure`が任意の非0終了を成功扱いしていたため、**期待するエラー文言との一致**も検証するようにした。これにより上記の`pipefail`バグが露見した。
- [x] 「スキャンがbuild前」「スキャンが起動後」「`up --detach`の後にスキャン」「`set -e`なし」「スキャン欠落」「buildがmigrateを含まない」「初回/更新それぞれでスクリプト呼び出しが消える」を独立ケースにした。
- [x] **スクリプトが実際に中断すること**を、Composeをスタブ化して検証する。スキャン到達を確認したうえで、起動コマンドが1つも実行されていないことを出力から確認する。

10次レビューによる追加是正:

- [x] **High**: `COMPOSE_PROJECT_NAME`が`compose.production.yaml`の`name:`より優先され、外部環境変数で別projectへデプロイできた（実測で`review-wrong-project`として描画されることを確認）。意図しないVolumeへのMigrationやコンテナ停止につながる。全本番Compose操作へ`--project-name seo-intelligence-prod`を明示し、検証は**敵対的な`COMPOSE_PROJECT_NAME`を設定した状態でレンダリング**して本番project名になることを確認する形にした。`--project-name`を外すと失敗することを確認済み。
- [x] バックアップと停止の順序が文書とスクリプトで食い違い、指示どおり実施すると stop→backup→build→scan→stop→migrate となって停止時間が延びていた。バックアップを**スクリプトへ統合**し、build → scan → stop → backup → migrate → recreate の一本道にした。3成果物のいずれかが空ならMigrationへ進まない。`--backup-confirmed`フラグは不要になったため廃止した。
- [x] 検証がソースの正規表現解析だったため、`build_and_scan`の**呼び出しだけを削除**しても関数本体に文字列が残り通っていた。順序検証を**実行トレース**へ置き換えた。Compose・スキャナ・バックアップをスタブ化し、initial/updateそれぞれの完全なコマンド列を照合する。

退行テストの再構成:

- [x] initial/updateの**成功系の完全なコマンドトレース**を固定した。
- [x] 「`build_and_scan`が定義されているが呼ばれない」ケースを追加。ソース解析では通り、トレースでは検出される。
- [x] 「`--project-name`を渡さない」ケースを追加。
- [x] バックアップディレクトリ名はUTC秒を含み、期待値の生成と実行の間で秒が繰り上がり得るため、比較時に正規化する（検証対象は名前ではなく位置であるため）。

11次レビューによる追加是正:

- [x] **High**: バックアップの出力先が相対パス（`backups/<ts>`）で、`docker run -v`は`/`または`./`で始まらないsourceを名前付きVolumeとして扱い、`/`を含む名前は拒否する。**アプリ停止後・バックアップ時点でupdateが中断する**バグだった。絶対パスへ正規化した。
- [x] **High**: 自動デプロイはproject固定したが、手動のバックアップ/再起動/状態確認コマンドが未固定だった。`docker_deployment.md`と`operations_runbook.md`の全本番Composeコマンド11件へ`--project-name`を付け、未固定コマンドが残っていないことを検証するガードを追加した。
- [x] 空ディレクトリのtar.gz自体は非空のため、サイズ検査では空バックアップを検出できなかった。archive内のエントリ数を数える形にした。Data Protection keyは1件以上を必須とし（無ければサインイン中のセッションが復元できない）、Storageは新規デプロイで空があり得るため0件を許容する。あわせて対象Volumeの存在確認と、dumpが`PGDMP`で始まることの検証を追加した。
- [x] `expect_trace`が終了コードを取得しながら確認していなかった。期待どおりのトレースを出した後に失敗しても成功扱いだった。終了コード0を要求する形に修正し、`exit 7`を追加すると落ちることを確認済み。

バックアップの切り出しとテスト:

- [x] バックアップを`scripts/backup-production.sh`へ切り出した。デプロイスクリプトから呼び、手動取得でも同じものを使う。文書へ手順を書き写さないのは、バックアップの失敗が「復元が必要になるまで気づかない」種類のためである。
- [x] `scripts/verify-backup-production.sh`を追加し、`docker`をPATHスタブへ置き換えて本体を検証する。healthy成功、相対パスの絶対化、既存ディレクトリ拒否、Volume不在拒否、不正dump拒否、空DP key拒否、空Storage許容の7ケース。CIへ組み込んだ。

12次レビューによる追加是正:

- [x] **High**: dumpの完全性検証が先頭5バイトの`PGDMP`確認だけだった。切り詰められたcustom dumpも先頭は`PGDMP`である。固定PostgreSQLイメージで`pg_restore --list`まで成功すること、かつ目次が1件以上のオブジェクトを含むことを必須にした。
- [x] **High**: `tar -tzf`の失敗を末尾の`|| true`が握りつぶしていた。Storageは0件を許容するため、壊れたarchiveでもMigrationへ進んでいた。一覧を先に取得して終了コードを確認する形にした。
- [x] バックアップがアプリ停止を検査していなかった。稼働中に直接実行するとdumpとStorage archiveが別時点になる。web/api/workerが停止済みかを検査して拒否する。
- [x] Data Protection keyの判定が「エントリ数」で、空のサブディレクトリだけでも通っていた。`tar -tzv`で**通常ファイル**のみを数え、`.xml`が1件以上あることを要求する。
- [x] デプロイ文書の「3成果物のいずれかが空なら中断」が実装と不一致だった（Storageの空は許容）。検証内容の表へ置き換えた。

CRLF起因のバグ（検証中に発覚）:

- [x] コミット時に`image-digests.lock`がCRLF化し、digest比較が「同じ値に見えるのに不一致」で失敗するようになった。`scan-container-images.sh`と`backup-production.sh`のlock解析でCRを除去した。後者は不正なimage参照を生成していた。
- [x] `.gitattributes`を追加し、`*.sh`・lockファイル・compose/Dockerfileの改行をLFに固定した。CRLFのシェルスクリプトはVPS上で`bad interpreter`になる。パーサ側のCR除去は二重の防御として残す。

テストの強化:

- [x] 相対パステストがDockerの実引数を検査しておらず、正規化を削除しても通る状態だった。スタブが受け取ったargvをトレースへ記録して検証する形にした。
- [x] バックアップテストを7→14ケースへ拡張した。pg_restore不可、目次が空、archive読み戻し失敗（Storage/DP keyの両方）、ディレクトリのみ、key以外のファイルのみ、アプリ稼働中、を追加した。
- [x] 相対パス正規化の削除と`tar`失敗の握りつぶし復活で、いずれも落ちることを確認済み。

13次レビューによる追加是正:

- [x] **High**: `pg_restore --list` は目次しか読まないため、データ領域を切り詰めたdumpを通していた。実測で確認（33KBのdumpを3KBへ切り詰め→`--list` はexit 0で3オブジェクトを報告）。`pg_restore --file=/dev/null` による全走査を追加し、これが検出することを確認した。実PostgreSQLへの復元でも、完全なdumpは5000行を復元でき、切り詰めdumpは失敗する。
- [x] **High**: 成果物のパーミッションが制限されていなかった。`umask 077` を設定し、archiveはbind mountではなくコンテナのstdoutからホストへリダイレクトして作成する（bind mountへ書くとコンテナのrootが644で作る）。Linux上でディレクトリ700・全成果物600を確認した。
- [x] 停止状態確認がfail-openだった。`docker compose ps` 自体の失敗を「稼働なし」として扱っていた。終了コードを確認して拒否する。
- [x] 出力ディレクトリの作成がatomicでなかった。親のみ `mkdir -p` し、末端は通常の `mkdir` で排他的に作成する。
- [x] Data Protection keyの判定が任意の `.xml` を許容していた。`(^|/)key-[^/]+[.]xml$` へ絞った。
- [x] 作業ツリーの `Dockerfile` / `compose*.yaml` / `image-digests.lock` がCRLFのままだった。`.gitattributes` に従って再正規化した。

退行テストの拡張（14→18ケース）:

- [x] 目次は正常だがデータ領域が切り詰められたdump
- [x] `docker compose ps` が非0終了するケース
- [x] 同一出力先への2プロセス同時実行（片方だけが成功すること）
- [x] Linux上でのディレクトリ700・成果物600（Windowsではskip）

実環境での確認:

- [x] 隔離したPostgreSQLへ実際に復元し、5000行・payload長2000が復元されることを確認した。
- [x] 切り詰めdumpは実restoreでも失敗することを確認した。

14次レビューによる追加是正:

- [x] デプロイ全体がsingle-flightでなかった。排他的`mkdir`は同一秒の衝突しか防がず、1秒以上ずれた2つのupdateは別ディレクトリになるため両方Migrationへ進めた。2つ目がdumpを取る間に1つ目がMigrationを開始する状態だった。`deploy-production.sh`冒頭で`flock`を取得し、build〜再作成の全体を排他する。Linuxで実際に2重起動が拒否されることを確認済み。
- [x] バックアップの停止判定に`migrate`が含まれていなかった。Migration進行中はスキーマがdumpの下で変わる。フィルタへ追加した。
- [x] stdout方式へ変更した結果、相対パステストが探す`--volume ...:/backup`が一度も現れず**完全な空振り**になっていた。スクリプトが報告する出力先を直接検証する形に直した。あわせて、bind mount回避という旧来の理由は消えたため、絶対パス化の理由を「スクリプトがリポジトリルートへcdするので、相対パスは呼び出し元の意図した場所に落ちない」へ改めた。相対パスは**呼び出し元のcwd基準**で解決する。
- [x] 並列バックアップテストが「一方成功・一方失敗」しか見ていなかった。敗者が排他的`mkdir`で拒否されたことをエラー文言で固定した。

テストの追加:

- [x] 秒をまたいで開始した2つの`deploy-production.sh update`が排他されること（Linuxで実行、Windowsは`flock`不在のためskip）。
- [x] ガードのトレーステストへ`flock`スタブを用意した。順序の検証は排他とは別の関心事であり、`flock`不在の環境でも実行できる必要がある。

15次レビューによる追加是正:

- [x] ロックが`artifacts/`配下にあり、別cloneやworktreeからの実行を排他できていなかった。排他すべき対象は作業コピーではなくCompose projectである。`scripts/lib/production-lock.sh`へ切り出し、checkout外の`${XDG_RUNTIME_DIR:-/tmp}/<project>.deploy.lock`をロックに使う（XDG_RUNTIME_DIRはユーザー専用0700なので、他ユーザーによるsymlink設置ができない）。
- [x] 手動バックアップ手順が`stop`をロック外で実行し、バックアップスクリプト自体もロックに参加していなかった。`backup-production.sh`を同じロックへ参加させ（デプロイから呼ばれた場合はマーカーで再入可）、`deploy-production.sh backup`モードで停止・取得・再起動をロック内にまとめた。手順書も3コマンドからこの1コマンドへ変更した。
- [x] 絶対パス化の理由がbind mount回避のまま残っていた記述を、実装（呼び出し元cwd基準）へ同期した。
- [x] 停止拒否のエラー文言に`migrate`を追加した。
- [x] `.gitignore`の`backups/`が全階層に効き、`tests/fixtures/backups/`まで無視していた。`/backups/`へアンカーした。

テストの追加:

- [x] 別cwdから相対パスで呼び、出力先が**呼び出し元のcwd配下**になること。従来はリポジトリルートから呼んでいたため、`$invocation_dir`を`$PWD`へ戻しても通っていた。実際に戻して落ちることを確認済み。
- [x] `migrate`単独での拒否。フィルタから`migrate`を外すと落ちることを確認済み。
- [x] 排他的`mkdir`を単独で検証するケース。ロックが効く環境では敗者がロックで止まるため、それぞれ別のロックファイルを与えてmkdir側だけを分離した。ロックとmkdirの二重防御が個別に検証される。

16次レビューによる追加是正:

- [x] **High**: 前回追加した後片付けが、実装から報告された任意のパスを`rm -rf`していた。「想定外の場所への出力を消す」処理そのものが、想定外の場所を無条件に再帰削除する状態だった。削除をやめ、作業ディレクトリ外へ出力された場合はパスを報告するだけにした（正しい経路の出力は`$work`配下でtrapが消す）。
- [x] `backup`モードでバックアップが失敗すると、`set -e`によりアプリが停止したままになっていた。EXIT trapで再起動する。Migrationを伴わないため、旧サービスを戻すのが正しい。`update`モードは逆で、失敗時は停止したままにする（Migrationが触れたかもしれないDBに対して旧コードを起動しない）。
- [x] `PRODUCTION_LOCK_HELD`だけで再入を許していたため、環境変数を設定すればロックを迂回できた。継承FDが同じロックファイルを指していること、かつ実際にロックが保持されていることを検証する。マーカーだけの主張は拒否する。
- [x] ロックの置き場所を明確化した。`XDG_RUNTIME_DIR`（ユーザー専用0700）→`/var/lock`の順で探し、どちらも使えなければ`/tmp`へフォールバックせず**実行を拒否**する。誰でも保持できるロックはロックではない。排他範囲が同一Unixユーザーに限られることを手順書へ明記し、複数ユーザー運用時は`PRODUCTION_LOCK_DIR`の事前構成を求める。
- [x] `deploy-production.sh`の使用例へ`backup`を追加し、`backup-production.sh`は内部用途であることを明記した（直接実行するとスタックが停止したままになる）。

テストの追加:

- [x] 偽の`PRODUCTION_LOCK_HELD`が拒否されること。検証を外すと落ちることを確認済み。
- [x] `backup`モードの成功トレース（stop → backup → up → ps）と、失敗時に再起動されること。trapを外すと落ちることを確認済み。
- [x] 別cwdテストにも`PRODUCTION_LOCK_DIR`を指定した。従来はLinuxで実ホストの本番ロック名を使っていた。

判断の記録（4次）:

- 実VPSでのCaddy/Cloudflare経由のクライアントIP置換確認だけは、環境依存のためローカルで代替できない。partitionロジック自体はテストで固定済み。

判断の記録（2次）:

- `*_downloaded` は**取得の開始**（Storage読み取り成功＋呼び出し元への引き渡し）を意味する。読み取り失敗時は記録しないが、引き渡し後の転送中断・クライアント切断は検知しない。転送完了の保証には使えないことを `api_design.md` に明記した。

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
