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
| 1.1 | 2026-05-31 | ソリューション骨格作成後の基本ビルド、テスト、起動コマンドを追記。 | Codex |
| 1.2 | 2026-05-31 | Docker Compose、CI雛形、共通設定/診断の起動手順を追記。 | Codex |
| 1.3 | 2026-05-31 | Infrastructure共通基盤のDI、Storage/Secret/Redis/Hangfire、Readiness疎通確認を追記。 | Codex |

## 1. 目的

本書は、SEOインテリジェンス基盤をローカルまたはテスト環境で起動するための前提、設定、Secret、DB、Storage、確認手順を定義する。

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
  -> MinIO or Local Storage
  -> External API mocks or Rakko Keyword API
```

ローカル依存サービスは `compose.yaml` で起動する。ここに含まれるユーザー名とパスワードはローカル開発専用の固定値であり、実Secretとして扱わない。本番相当環境や外部公開環境ではKey Vault/User Secrets等で別管理する。

```powershell
docker compose up -d postgres redis minio minio-init
docker compose ps
```

| サービス | ローカルURL/ポート | 用途 |
| --- | --- | --- |
| PostgreSQL | `localhost:5432` | 業務DB、Hangfire PostgreSQL storage。 |
| Redis | `localhost:6379` | キャッシュ、分散ロック、レート制御、一時状態。 |
| MinIO API | `http://localhost:9000` | Storage代替。bucketは`seo-intelligence`。 |
| MinIO Console | `http://localhost:9001` | ローカルStorage確認。 |

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

ソリューション骨格の確認と最小起動は以下を使う。

```text
dotnet build
dotnet test --filter Category=Unit
dotnet run --project src/SeoIntelligence.Api
dotnet run --project src/SeoIntelligence.Web
dotnet run --project src/SeoIntelligence.Worker
```

CIと同じスクリプトで確認する場合は以下を使う。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test.ps1 -Filter Category=Unit
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/migration-dry-run.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/smoke-test.ps1
```

## 5. 環境変数

| 変数 | 用途 | ローカル例 |
| --- | --- | --- |
| `ASPNETCORE_ENVIRONMENT` | 実行環境 | `Development` |
| `ConnectionStrings__Default` | PostgreSQL接続 | `Host=localhost;Port=5432;Database=seo;Username=seo;Password=seo_dev_password` |
| `Redis__ConnectionString` | Redis接続 | `localhost:6379` |
| `Hangfire__Storage` | Hangfire storage | `PostgreSQL` |
| `Storage__Provider` | ローデータ/出力保存先 | MVP既定は`Local`。`MinIO`は疎通確認のみ。 |
| `Storage__BasePath` | ローカル保存先 | `./.data/storage` |
| `Storage__Endpoint` | MinIO API URL | `http://localhost:9000` |
| `Storage__BucketName` | Storage bucket | `seo-intelligence` |
| `SecretStore__Provider` | Secret Store実装 | `Configuration` |
| `SecretStore__ConfigurationPrefix` | User Secrets/環境変数上のSecret prefix | `Secrets` |
| `OpenTelemetry__Enabled` | OTel exporter有効化 | `false` |
| `OpenTelemetry__ServiceName` | OTel service name | `SeoIntelligence.Api` |
| `OpenTelemetry__OtlpEndpoint` | OTLP endpoint | `http://localhost:4317` |
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

ローカルでは.NET User Secrets、開発用Key Vault、または安全なSecret管理を使う。MVPの`Configuration` Secret Storeは `Secrets:{secretName}` を参照する。`.env`や設定ファイルへ実値をコミットしない。

## 7. DB初期化

| 手順 | 内容 |
| --- | --- |
| 1 | PostgreSQLを起動する。 |
| 2 | EF Core migrationsを適用する。 |
| 3 | 初回マイグレーションで`pg_trgm`拡張を有効化する。 |
| 4 | 既定workspaceを1件作成する。 |
| 5 | `api_contract_scopes`へ契約確認結果を初期データとして登録する。管理画面/APIでは更新しない。 |
| 6 | 地域/言語マスタ同期ジョブを実行する。 |

EF Core `DbContext` と migrations 実装後、DB更新は以下を使う。

```text
dotnet ef database update --project src/SeoIntelligence.Infrastructure --startup-project src/SeoIntelligence.Api
dotnet run --project src/SeoIntelligence.Api
dotnet run --project src/SeoIntelligence.Worker
```

現時点のmigration dry-runは `scripts/migration-dry-run.ps1` / `scripts/migration-dry-run.sh` で実行する。`DbContext` が未実装の場合は、雛形としてskipして成功終了する。

## 8. 起動確認

| 確認 | 期待結果 |
| --- | --- |
| API Health | `/healthz`が成功する。 |
| Readiness | `/readyz`でDB、Redis、Storage、Secret Storeの疎通確認ができる。未設定のDB/Redisはskip扱い、設定済みで接続不可の場合はunhealthy。 |
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

## 10. CI/CD雛形

GitHub Actionsは `.github/workflows/ci.yaml` を使う。

| Job | 実行内容 |
| --- | --- |
| `build-test-smoke` | restore、build、test、migration dry-run、Docker Compose依存サービス起動、API smoke test。 |
| `container-scan` | PostgreSQL、Redis、MinIO、MinIO ClientのコンテナイメージをTrivyでscanする。初期雛形では検出結果を表示し、fail条件は後続の運用品質ゲートで調整する。 |

通常CIでは外部API Realを使わず、Mock既定のまま実行する。

## 11. トラブルシュート

| 症状 | 確認 |
| --- | --- |
| DB接続不可 | PostgreSQL起動、接続文字列、Firewall、DB名。 |
| Redis接続不可 | Redis起動、ポート、接続文字列。 |
| MinIO接続不可 | `docker compose ps`、`http://localhost:9001`、bucket `seo-intelligence`。 |
| 外部API 403 | APIキーSecret名、Secret参照権限、契約状態。 |
| 外部API 402 | 契約側のクレジット残量、契約プラン、対象ジョブの消費量。 |
| ジョブが進まない | Worker起動、Hangfire storage、キュー名、`jobs.status`。 |
| 通知されない | Webhook Secret、通知チャンネルstatus、`notification_deliveries`。 |

## 12. 実装後に追記する項目

| 項目 | 追記タイミング |
| --- | --- |
| Integration/Contract/E2Eの正式手順 | 各テスト整備後。 |
| MinIOの署名付きObject Storage adapter | Storage credential参照方式確定後。 |
| CI/CD Secretと品質ゲート | Secret管理、外部API Mock、コンテナポリシー確定後。 |
| デプロイ手順 | ステージング環境作成後。 |
