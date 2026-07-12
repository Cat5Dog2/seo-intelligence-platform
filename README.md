# SEO Intelligence Platform

ラッコキーワードAPIを中核に、キーワード調査、競合・コンテンツ分析、順位監視、レポート、AI支援を統合するSEOインテリジェンス基盤です。

## 実装状況

`main` では、Phase 1（MVP）、Phase 2、Phase 3のIssueと受入テストが完了しています。実装範囲は [`todo.md`](todo.md) と各設計文書の完了条件に対応しています。

| フェーズ | 実装済みの範囲 |
| --- | --- |
| Phase 1: MVP | 単一ワークスペース、プロジェクト/サイト、API認証情報、Discord通知設定、監査ログ、キーワード探索、最大50,000語の検索ボリューム調査、機会スコア、ダッシュボード、CSV出力、クレジット監視、ジョブ失敗/402通知 |
| Phase 2: SEO実務拡張 | トピッククラスター、競合・獲得キーワード/ページ分析、コンテンツ分析、記事ブリーフと版履歴、順位監視/順位分布/アラート、Phase 2ダッシュボードとBlazor UI |
| Phase 3: 自動化/AI/外部連携 | リライト優先度、カニバリ検出、PDF/Excelレポートと共有URL、CSV/Excel入出力、AIアシスタント、GSC/GA4/CMS/BIコネクタ設定・接続テストスタブ、Phase 3ダッシュボードとBlazor UI |

Phase 3のAIは `IAiContentService` 抽象と、根拠データから下書きを生成する決定論的な既定実装です。外部AIプロバイダーとの実通信は未実装です。GSC/GA4/CMS/BIも設定、Secret参照、接続テスト履歴までのスタブであり、実データは取得しません。

Phase 4の複数ユーザー、RBAC、SSO、承認フローと、推奨バックログの専用EC/動画/画像企画、広告/LP提案、GSC/GA4/CMS/BI実データ連携は将来構想です。

## 主な実装機能

- キーワード探索: サジェスト、関連語、LSI/PAA、FAQ、同時ランクイン語を統合し、正規化・重複除外・保存・フィルタリングを行います。
- 検索ボリューム一括調査: 1〜50,000語をJSON配列で登録し、外部requestId、ポーリング、結果、月別推移を保存します。MVPのCSV入力はブラウザ内で解析し、CSVファイル本体はAPIへ送信しません。
- 機会スコア: 検索ボリューム、SEO難易度、トレンド、商業性、関連度を基に算出し、根拠成分を保存します。
- データ出力: Phase 1対象データのCSV出力に加え、Phase 3ではExcel出力、CSV/Excel検証付きインポート、PDF/Excelレポート、期限付き共有URLを実装しています。
- API/UI/Worker: ASP.NET Core Minimal API、Blazor Web App（Interactive Server）、.NET Worker Serviceで構成しています。
- 非同期ジョブ: HangfireとPostgreSQL storageでキュー、ポーリング、再スケジュールを実行し、業務状態は `jobs` テーブルを正本にします。
- PostgreSQL: EF Core、JSONB、Phase 1〜3のMigration、SeedDataを実装しています。
- Redis: 文字列キャッシュ操作と分散ロックの実装があり、ジョブの重複実行防止に分散ロックを使用します。検索指標の契約スコープ別再利用判定はPostgreSQL上のデータを使用します。
- 外部API/運用: ラッコキーワードAPIのMock/Realクライアント、外部API呼び出し・消費クレジット監査、監査ログ、429/500/503の再試行、402/403のfatal分岐、Discord通知と再送を実装しています。

## 技術構成

| 分類 | 現在の実装 |
| --- | --- |
| Runtime | .NET 10 |
| Backend | ASP.NET Core Minimal API、共通レスポンスエンベロープ、Correlation ID、Health/Readiness、OpenAPI JSON |
| Frontend | Blazor Web App、Interactive Server |
| Worker | .NET Worker Service、Hangfire、Hangfire PostgreSQL storage |
| DB | PostgreSQL 16、EF Core 10、Npgsql、JSONB |
| Cache/Coordination | Redis 7、StackExchange.Redis |
| Storage | ローカルストレージ、MinIO接続 |
| External | ラッコキーワードAPI、Discord Webhook、Phase 3外部連携スタブ |
| Test | xUnit、ASP.NET Core Integration Test、Playwright BrowserE2E |

### Observabilityの現在地

構造化ログ、Correlation ID、`ActivitySource`、`Meter`、運用メトリクス、`OpenTelemetryOptions` を実装し、API/Web/Workerへ計装ポイントと設定の導入口を登録しています。

現時点ではOpenTelemetry SDK、OTLP Exporter、Application Insights/Grafana等へのExporter登録はありません。そのため、外部バックエンドまで含む完全なOpenTelemetry連携ではありません。

## アーキテクチャ

```text
Blazor Web App
      |
ASP.NET Core API
      |
Application / Domain
      |
Infrastructure ---- ラッコキーワードAPI / Discord
   |       |
PostgreSQL Redis
   |
Hangfire / Worker
```

- Domainは他プロジェクトへ依存しません。
- ApplicationはDomainとContractsへ依存し、Infrastructureへ依存しません。
- InfrastructureはEF Core、外部API、Redis、Storage、Secret Store、通知の実装を持ちます。
- Api、Web、WorkerがApplicationの契約を利用します。
- 外部API由来DTOはInfrastructureに閉じ込め、内部APIやUIへ直接公開しません。

## ローカル開発

### 前提

- .NET 10 SDK
- Docker DesktopまたはDocker Engine + Compose
- Git
- PowerShell 7（包括スモークテストを実行する場合）

### セットアップと起動

Dockerだけで全スタックを起動する場合は、初回にMigrationを適用してからAPI、Worker、Webを起動します（`compose.override.yaml`が自動で読み込まれ、`127.0.0.1`へポート公開されます）。

```powershell
docker compose up -d postgres redis
docker compose --profile tools run --rm --build migrate
docker compose up -d --build --wait api worker web
docker compose ps
```

既定のHTTP URLはAPIが `http://localhost:5251`、Webが `http://localhost:5295` です。IDEデバッグ・Hot Reload・依存サービスのみの起動を含む詳細な手順の正本は [`docs/environment_setup.md`](docs/environment_setup.md) を参照してください。

## VPSデプロイ（暫定・個人利用）

VPSでは `compose.yaml` に [`compose.production.yaml`](compose.production.yaml) をoverlayとして重ねます。DB、Redis、APIはホストへポート公開せず、Web/APIだけを共通Caddyの専用external networkへ接続します。アプリ内認証が未実装のため、これは外部認証ゲートで保護する単一利用者向けの暫定構成です。

初回デプロイ、更新、バックアップ、Caddy設定を含む手順の正本は [`docs/docker_deployment.md`](docs/docker_deployment.md) を参照してください。`.env.production`の`POSTGRES_PASSWORD`を必ず設定し、Gitへ追加しないでください。

### 外部APIモード

通常開発とCIは `RakkoKeyword:Mode=Mock` が既定で、Real APIを呼びません。Real APIへ切り替える場合は、契約・利用範囲、クレジット、APIキーの有効性、Secret Store参照、`RakkoKeyword` 設定を事前に確認してください。秘密値を設定ファイル、README、ログ、DBへ記録しないでください。

## ビルド、テスト、スモークテスト

PowerShell:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test.ps1 -Filter "Category!=BrowserE2E"
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test.ps1 -Filter Category=Unit
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/migration-dry-run.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/smoke-local.ps1
```

Bash:

```bash
bash scripts/build.sh
bash scripts/test.sh
bash scripts/test.sh --filter "Category!=BrowserE2E"
bash scripts/migration-dry-run.sh
```

包括スモークテストは依存サービス起動、Migration適用、API/Worker/Web起動、`/healthz`、`/readyz`、プロジェクト/監査ログ、マスタ同期、CSV出力ジョブ完了を確認します。実ブラウザの代表フローも確認する場合は次を実行します。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/smoke-local.ps1 -RunBrowserTests -InstallPlaywrightBrowsers
```

BrowserE2EはAPI/Webと依存サービスが起動した状態で実行します。非Browserテストだけを確認する場合は `Category!=BrowserE2E` フィルタを使用してください。テスト区分と実行条件は [`docs/test_plan.md`](docs/test_plan.md) を参照してください。

## CI

[`.github/workflows/ci.yaml`](.github/workflows/ci.yaml) はpush（`main`）とpull requestで次を実行します。

1. .NET 10でrestoreし、`scripts/build.sh` でRelease buildします。
2. `scripts/test.sh` でRelease testを実行します。
3. `scripts/migration-dry-run.sh` でEF Coreの冪等Migration SQLを生成します。
4. 開発用/VPS用Composeの構文を検証し、Web/API/Worker/Migration imageをGitHub Actionsレイヤーキャッシュ付きでbuildして、`scripts/container-smoke.sh`で隔離Compose project上のMigration、HTTP、非root、Storage共有、Data Protection keys永続化をスモーク確認します。同スクリプトはローカルでも `bash scripts/container-smoke.sh` で実行できます。
5. `scripts/smoke-local.ps1` で依存サービス、Migration、API/Worker/Web、Health/Readiness、マスタ同期、CSV出力までを確認します。BrowserE2Eはリポジトリ変数 `RUN_BROWSER_E2E=true` の場合だけ追加実行します。
6. PostgreSQL、Redis、MinIO、MinIO Clientの利用イメージをTrivyでスキャンし、未修正脆弱性を除くHIGH/CRITICALを表示します。現在の `exit-code: "0"` 設定では検出結果はレポートのみで、CIを失敗させません。

## セキュリティと運用上の前提

- 認証・認可は未実装です（Phase 4スコープ）。API/Webは無認証で動作するため、信頼できるネットワーク内でのみ運用してください。
- APIキー、Webhook URL、OAuthトークン等の実値はDB、レスポンス、ログ、監査ログへ出しません。
- DBには `key_ref`、`webhook_secret_ref`、`auth_ref` 等の参照を保存します。
- Secret Storeの既定実装（`SecretStore:Provider=Configuration`）はプロセス内の設定へ保存します。API経由で登録した秘密値はプロセス再起動で失われ、ジョブを実行する別プロセスのWorkerとは共有されません。実運用では環境変数またはUser SecretsでAPIとWorkerの両方へ同じSecret参照名の値を配布してください。
- プロジェクト配下APIはURLの `projectId` と対象データの `project_id` を検証します。
- DELETE系APIは物理削除せず、`archived` または `disabled` へ状態変更します。
- 外部API呼び出しは `external_api_calls`、操作履歴は `audit_logs` に記録します。

## 詳細ドキュメント

| 文書 | 内容 |
| --- | --- |
| [`docs/requirements.md`](docs/requirements.md) | 要件、スコープ、受入基準、フェーズ |
| [`docs/basic_design.md`](docs/basic_design.md) | アーキテクチャ、レイヤ、主要コンポーネント |
| [`docs/api_design.md`](docs/api_design.md) | 内部API、レスポンス、エラー、入力制約 |
| [`docs/db_design.md`](docs/db_design.md) | DBテーブル、制約、インデックス、Migration |
| [`docs/screen_design.md`](docs/screen_design.md) | Blazor画面、状態、操作、API対応 |
| [`docs/job_design.md`](docs/job_design.md) | ジョブ、状態遷移、リトライ、通知 |
| [`docs/test_plan.md`](docs/test_plan.md) | テスト方針、受入テスト、Mock方針 |
| [`docs/external_api_design.md`](docs/external_api_design.md) | 外部API、Secret、クレジット、キャッシュ |
| [`docs/operations_runbook.md`](docs/operations_runbook.md) | 運用、障害対応、スモークテスト |
| [`docs/environment_setup.md`](docs/environment_setup.md) | ローカル環境、設定、起動確認 |
| [`docs/docker_deployment.md`](docs/docker_deployment.md) | Docker Compose、VPS、共通Caddy、更新手順 |
| [`docs/adr/`](docs/adr/) | 技術選定記録 |
| [`todo.md`](todo.md) | Issue単位の実装状況と完了条件 |

開発ルールは [`AGENTS.md`](AGENTS.md) を参照してください。
