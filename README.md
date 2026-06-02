# SEO Intelligence Platform

ラッコキーワードAPIを中核にしたSEO・コンテンツ・競合分析・順位監視プラットフォームです。

キーワード調査、検索ボリューム分析、競合分析、記事ブリーフ生成、順位監視、レポート、AI支援を、Web UI、内部API、非同期ジョブ、PostgreSQL、Redis、外部API連携で統合することを目的にしています。

## 現在の状態

このリポジトリは設計ドキュメントと実装バックログを整備し、.NET 10 / Clean Architecture の最小ソリューション骨格を作成済みです。機能実装は今後 `todo.md` のIssue順に追加します。

## 主なスコープ

Phase 1: MVP

- 単一ワークスペース、プロジェクト、サイト、API認証情報、Discord通知設定、監査ログ
- サジェスト、関連語、LSI/PAA、FAQ、同時ランクイン語の統合取得
- 最大50,000語の検索ボリューム非同期調査
- 機会スコア算出
- キーワード探索、一括調査、APIクレジット、失敗ジョブのダッシュボード
- Phase 1対象データのCSV出力
- 429/500/503リトライ、402クレジット不足通知、ジョブ失敗通知

Phase 2以降

- 競合分析、獲得キーワード/ページ分析、コンテンツ分析、記事ブリーフ
- 順位監視、順位アラート、トピッククラスター
- AIアシスタント、リライト優先度、カニバリ検出、レポート、外部連携スタブ
- 複数ユーザー、RBAC、SSOなどのエンタープライズ拡張

## 想定技術スタック

| 分類 | 技術 |
| --- | --- |
| Runtime | .NET 10 LTS |
| Backend | ASP.NET Core Web API / Minimal APIs |
| Frontend | Blazor Web App |
| Worker | .NET Worker Service + Hangfire |
| DB | PostgreSQL + EF Core + JSONB |
| Job Storage | Hangfire PostgreSQL storage |
| Cache/Lock/Rate Limit | Redis |
| Observability | OpenTelemetry、構造化ログ、メトリクス |
| External API | ラッコキーワードAPI、Discord Webhook、AI API、将来GSC/GA4/CMS/BI |

## 想定ディレクトリ構成

```text
SeoIntelligence.sln
src/
  SeoIntelligence.Web/
  SeoIntelligence.Api/
  SeoIntelligence.Application/
  SeoIntelligence.Domain/
  SeoIntelligence.Infrastructure/
  SeoIntelligence.Worker/
  SeoIntelligence.Contracts/
tests/
  UnitTests/
  IntegrationTests/
  ContractTests/
  E2ETests/
docs/
```

## アーキテクチャ方針

- Domainは他プロジェクトへ依存しない。
- ApplicationはDomainとContractsへ依存し、Infrastructureへ依存しない。
- InfrastructureはEF Core、外部APIクライアント、Secret Store、Storage、Redis、通知連携を実装する。
- Api、Web、WorkerはApplicationのユースケースを呼び出す。
- 重い外部API連携は非同期ジョブ化し、業務上のジョブ状態は `jobs` テーブルを正本にする。
- 外部API DTOは `docs/rakko-keyword-api-docs.json` から生成し、Infrastructure層に閉じ込める。

## 開発の始め方

実装は `todo.md` のIssue順に進めます。

```text
ISSUE-P0-001 ソリューション骨格を作成する
ISSUE-P0-002 ローカル開発基盤とCI雛形を作成する
ISSUE-MVP-001 Domain/Application共通基盤を実装する
```

代表コマンド:

```text
dotnet build
dotnet test
dotnet test --filter Category=Unit
dotnet test --filter Category=Integration
dotnet test --filter Category=Contract
dotnet ef database update --project src/SeoIntelligence.Infrastructure --startup-project src/SeoIntelligence.Api
dotnet run --project src/SeoIntelligence.Api
dotnet run --project src/SeoIntelligence.Web
dotnet run --project src/SeoIntelligence.Worker
```

## ローカル環境の前提

- .NET 10 SDK
- Docker Desktop
- PostgreSQL Client
- Git
- PowerShell

ローカル依存サービスは `compose.yaml` で起動します。Web/API/Worker本体は、MVP開発中はローカル `dotnet run` を標準にします。

```powershell
docker compose up -d postgres redis minio minio-init
docker compose ps
dotnet run --project src/SeoIntelligence.Api
dotnet run --project src/SeoIntelligence.Web
dotnet run --project src/SeoIntelligence.Worker
```

詳細な起動、停止、ログ確認、データ初期化手順は `docs/environment_setup.md` を参照してください。

外部APIは通常開発とCIではMockを既定にします。Real APIを使う場合は、契約スコープ、APIキー状態、クレジット消費を確認してから明示的に切り替えます。

## 設計ドキュメント

| 文書 | 位置づけ |
| --- | --- |
| `docs/requirements.md` | 要件、スコープ、機能要件、受入基準 |
| `docs/basic_design.md` | アーキテクチャ、レイヤ構成、主要コンポーネント |
| `docs/api_design.md` | 内部REST API、レスポンス、エラー、入力制約 |
| `docs/db_design.md` | DBテーブル、制約、インデックス、ステータス |
| `docs/screen_design.md` | Blazor画面、状態、操作、API対応 |
| `docs/job_design.md` | 非同期ジョブ、状態遷移、リトライ、通知 |
| `docs/test_plan.md` | テスト方針、受入テスト、障害系 |
| `docs/external_api_design.md` | 外部API、Secret、クレジット、キャッシュ |
| `docs/operations_runbook.md` | 運用、障害対応、スモークテスト |
| `docs/environment_setup.md` | ローカル環境、Secret、起動確認 |
| `docs/adr/` | 技術選定のADR |
| `todo.md` | 実装Issueと作業順 |

正本が競合する場合は、スコープは `requirements.md`、API契約は `api_design.md`、DB定義は `db_design.md`、ジョブ状態は `job_design.md`、画面仕様は `screen_design.md` を優先します。

## セキュリティと運用ルール

- APIキー、Webhook URL、OAuthトークン、AI APIキーの実値はDB、ログ、レスポンス、監査ログに出さない。
- DBには `key_ref`、`webhook_secret_ref`、`auth_ref` などのSecret参照のみ保存する。
- DELETE系APIは物理削除せず、`archived` または `disabled` へ状態変更する。
- 業務データAPIはURL上の `projectId` とDB上の `project_id` 一致を必ず検証する。
- 外部API呼び出しは `external_api_calls`、操作履歴は `audit_logs` に記録する。
- 429/500/503はリトライ対象、400/402/403は原則fatalとして扱う。

## Codex向け作業ルール

Codexや自動実装エージェントで作業する場合は、`AGENTS.md` を必ず参照してください。

実装依頼は原則として `todo.md` の1 Issue単位に限定し、完了時に変更ファイル、検証コマンド、未実行テストと理由を報告します。
