# 環境構築手順書

**ラッコキーワードAPIを中核にしたSEOインテリジェンス基盤**

_SEO Intelligence Platform / SEOインテリジェンス基盤_

| 項目 | 内容 |
| --- | --- |
| 文書ID | ENV-RKSEO-001 |
| 作成日 | 2026-05-30 |
| 対象 | ローカル開発環境、テスト環境、設定、Secret、起動確認 |
| 関連文書 | basic_design.md / api_design.md / db_design.md / operations_runbook.md |

## 改訂履歴

| 版 | 日付 | 内容 | 作成/更新 |
| --- | --- | --- | --- |
| 1.0 | 2026-05-30 | 初版作成。ローカル開発環境、環境変数、Secret、DB初期化、起動確認を定義。 | ChatGPT |

## 1. 目的

本書は、SEOインテリジェンス基盤をローカルまたはテスト環境で起動するための前提、設定、Secret、DB、Storage、確認手順を定義する。現時点では実装前の設計手順であり、実際のコマンドはソリューション作成後に更新する。

## 2. 前提ツール

| ツール | 用途 |
| --- | --- |
| .NET 10 SDK | API、Blazor、Workerのビルド/実行。 |
| Docker Desktop | PostgreSQL、Redis、Storage代替の起動。 |
| PostgreSQL Client | DB接続確認、手動調査。 |
| Git | ソース管理。 |
| PowerShell | Windowsローカル手順の標準シェル。 |

## 3. ローカル構成

```text
Developer Browser
  -> SeoIntelligence.Web / SeoIntelligence.Api
  -> PostgreSQL
  -> Redis
  -> SeoIntelligence.Worker
  -> Local Storage Emulator
  -> External API mocks or Rakko Keyword API
```

## 4. 想定ディレクトリ

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

## 5. 環境変数

| 変数 | 用途 | ローカル例 |
| --- | --- | --- |
| `ASPNETCORE_ENVIRONMENT` | 実行環境 | `Development` |
| `ConnectionStrings__Default` | PostgreSQL接続 | `Host=localhost;Database=seo;Username=seo;Password=...` |
| `Redis__ConnectionString` | Redis接続 | `localhost:6379` |
| `Hangfire__Storage` | Hangfire storage | `PostgreSQL` |
| `Storage__Provider` | ローデータ/出力保存先 | `Local`または`MinIO` |
| `Storage__BasePath` | ローカル保存先 | `./.data/storage` |
| `RakkoKeyword__BaseUrl` | ラッコAPI URL | `https://api.rakkokeyword.com` |
| `RakkoKeyword__ApiKeySecretName` | APIキーSecret名 | `rakko-keyword-api-key-dev` |
| `RakkoKeyword__MaxConcurrentRequests` | 同時実行数 | `2` |
| `Jobs__SearchVolumePollIntervalSeconds` | 検索ボリュームポーリング | `60` |
| `Jobs__RankCheckPollIntervalSeconds` | 順位チェックポーリング | `60` |
| `Credits__ResetTimeZone` | クレジットリセットTZ | `Asia/Tokyo` |
| `Discord__DefaultWebhookSecretName` | Discord Webhook Secret名 | `discord-webhook-dev` |
| `Ai__Provider` | AI Provider | `Disabled`またはProvider名 |

## 6. Secret管理

| Secret | 用途 | 注意 |
| --- | --- | --- |
| `rakko-keyword-api-key-dev` | ラッコキーワードAPIキー | DBへ実値保存しない。 |
| `discord-webhook-dev` | Discord Webhook URL | DBへ実値保存しない。 |
| `ai-api-key-dev` | AI APIキー | Phase 3。必要時のみ設定。 |

ローカルでは.NET User Secrets、開発用Key Vault、または安全なSecret管理を使う。`.env`や設定ファイルへ実値をコミットしない。

## 7. DB初期化

| 手順 | 内容 |
| --- | --- |
| 1 | PostgreSQLを起動する。 |
| 2 | EF Core migrationsを適用する。 |
| 3 | 初回マイグレーションで`pg_trgm`拡張を有効化する。 |
| 4 | 既定workspaceを1件作成する。 |
| 5 | `api_contract_scopes`へ契約確認結果を初期データとして登録する。管理画面/APIでは更新しない。 |
| 6 | 地域/言語マスタ同期ジョブを実行する。 |

実装後、正式なコマンドを以下の形で追記する。

```text
dotnet ef database update --project src/SeoIntelligence.Infrastructure --startup-project src/SeoIntelligence.Api
dotnet run --project src/SeoIntelligence.Api
dotnet run --project src/SeoIntelligence.Worker
```

## 8. 起動確認

| 確認 | 期待結果 |
| --- | --- |
| API Health | `/healthz`が成功する。 |
| Readiness | `/readyz`がDB/Redis接続成功を返す。 |
| OpenAPI | `/openapi/v1.json`が取得できる。 |
| プロジェクト一覧 | `GET /api/projects`が成功する。 |
| Worker | ジョブ一覧にWorker処理結果が反映される。 |
| Discord | テスト通知が送信され履歴が残る。 |

## 9. 外部APIモード

| モード | 用途 |
| --- | --- |
| Mock | CI、通常開発、障害系テスト。クレジットを消費しない。 |
| Sandbox相当 | 軽量疎通。利用可能な場合のみ。 |
| Real | 実データ確認。クレジット消費、契約スコープ、APIキー状態を確認してから使う。 |

既定はMockにする。Realモードでは、外部API呼び出しが`external_api_calls`と`audit_logs`に保存されることを確認する。

## 10. トラブルシュート

| 症状 | 確認 |
| --- | --- |
| DB接続不可 | PostgreSQL起動、接続文字列、Firewall、DB名。 |
| Redis接続不可 | Redis起動、ポート、接続文字列。 |
| 外部API 403 | APIキーSecret名、Secret参照権限、契約状態。 |
| 外部API 402 | 契約側のクレジット残量、契約プラン、対象ジョブの消費量。 |
| ジョブが進まない | Worker起動、Hangfire storage、キュー名、`jobs.status`。 |
| 通知されない | Webhook Secret、通知チャンネルstatus、`notification_deliveries`。 |

## 11. 実装後に追記する項目

| 項目 | 追記タイミング |
| --- | --- |
| 正式なDocker Compose | インフラ雛形作成後。 |
| 正式な起動コマンド | ソリューション作成後。 |
| テストコマンド | testsプロジェクト作成後。 |
| CI/CD環境変数 | CI定義作成後。 |
| デプロイ手順 | ステージング環境作成後。 |
