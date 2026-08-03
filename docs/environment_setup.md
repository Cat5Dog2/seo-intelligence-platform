# 環境構築手順書

**ラッコキーワードAPIを中核にしたSEOインテリジェンス基盤**

_SEO Intelligence Platform / SEOインテリジェンス基盤_

| 項目 | 内容 |
| --- | --- |
| 文書ID | ENV-RKSEO-001 |
| 作成日 | 2026-05-30 |
| 対象 | ローカル開発環境、テスト環境、小規模VPS、設定、Secret、起動確認 |
| 関連文書 | basic_design.md / api_design.md / db_design.md / operations_runbook.md / docker_deployment.md |

## 改訂履歴

| 版 | 日付 | 内容 | 作成/更新 |
| --- | --- | --- | --- |
| 1.0 | 2026-05-30 | 初版作成。ローカル開発環境、環境変数、Secret、DB初期化、起動確認を定義。 | ChatGPT |
| 1.1 | 2026-05-31 | ソリューション骨格作成後の基本ビルド、テスト、起動コマンドを追記。 | Codex |
| 1.2 | 2026-05-31 | Docker Compose、CI雛形、共通設定/診断の起動手順を追記。 | Codex |
| 1.3 | 2026-05-31 | Infrastructure共通基盤のDI、Storage/Secret/Redis/Hangfire、Readiness疎通確認を追記。 | Codex |
| 1.4 | 2026-07-11 | Web/API/Workerのコンテナ、VPS用Compose、Migration、永続Volumeの手順を追記。 | Codex |
| 1.5 | 2026-07-12 | レビュー反映。Compose overlay構成、`Database__*`個別キー、healthcheckと`--wait`、`container-smoke.sh`を反映。 | Claude |

## 1. 目的

本書は、SEOインテリジェンス基盤をローカルまたはテスト環境で起動するための前提、設定、Secret、DB、Storage、確認手順を定義する。

## 2. 前提ツール

| ツール | 用途 |
| --- | --- |
| .NET 10 SDK | API、Blazor、Workerのビルド/実行。 |
| Docker DesktopまたはDocker Engine + Compose | Web/API/Worker、PostgreSQL、Redis、任意のMinIO疎通環境の起動。 |
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

Composeは3ファイル構成である。`compose.yaml`（base、ホストポート非公開）、`compose.override.yaml`（開発専用overlay。`docker compose`が自動読込し、`127.0.0.1`bindの公開ポートとMinIOを持つ）、`compose.production.yaml`（VPS用overlay。差分のみ。正本は `docker_deployment.md`）。既定ユーザー名とパスワードはローカル開発専用であり、実Secretとして扱わない。開発では従来どおり `docker compose <command>` だけでよい。

```powershell
docker compose up -d postgres redis minio minio-init
docker compose ps
```

上記はIDEデバッグ用に依存サービスだけを起動するコマンドである。Dockerだけで全スタックを起動する場合は、後述のone-shot Migrationを適用してから `api`、`worker`、`web` を起動する。

| サービス | ローカルURL/ポート | 用途 |
| --- | --- | --- |
| PostgreSQL | `localhost:5432` | 業務DB、Hangfire PostgreSQL storage。 |
| Redis | `localhost:6379` | キャッシュ、分散ロック、レート制御、一時状態。 |
| API | `http://localhost:5251` | Minimal API、Health/Readiness、OpenAPI。 |
| Web | `http://localhost:5295` | Blazor Web App。APIはCompose内部の`http://api:8080`を使用。 |
| MinIO API | `http://localhost:9000` | adapterの疎通確認用。bucketは`seo-intelligence`。成果物read/writeには使わない。 |
| MinIO Console | `http://localhost:9001` | ローカルMinIO疎通確認。 |

ローカル公開ポートはすべて`127.0.0.1`へbindする。同じVPS上の別ComposeとDBポートを共有しない本番構成では、PostgreSQL、Redis、APIのホストポート自体を公開しない。

### 3.1 よく使うCompose操作

| 目的 | コマンド | 備考 |
| --- | --- | --- |
| 依存サービスを起動 | `docker compose up -d postgres redis minio minio-init` | ホストでWeb/API/Workerをデバッグする場合。MinIOは明示指定時だけ起動する。 |
| 全スタックを起動 | `docker compose up -d --build --wait api worker web` | 初回は事前に`migrate`を実行する。依存DB/Redisも起動し、api/webのhealthcheckがhealthyになるまで待つ。 |
| 状態確認 | `docker compose ps` | PostgreSQL/Redis/api/webが`healthy`であることを確認する。 |
| コンテナスモーク | `bash scripts/container-smoke.sh` | CIと同一のコンテナ起動スモーク。隔離projectで実行され開発スタックへ影響しない。 |
| ログ確認 | `docker compose logs -f web api worker` | アプリ別にログを分離して確認する。 |
| Worker再起動 | `docker compose restart worker` | Web/APIを止めずにWorkerだけ再起動する。 |
| 停止 | `docker compose stop` | データvolumeは保持する。 |
| 停止とコンテナ削除 | `docker compose down` | データvolumeは保持する。通常はこちらで十分。 |
| データ初期化 | `docker compose down -v` | DB/Redis/Storage/Data Protection keysを削除する。必要時だけ実行する。 |

### 3.2 依存サービス起動後のアプリ起動

依存サービスを起動してから、必要なアプリを別ターミナルで起動する。

```powershell
dotnet run --project src/SeoIntelligence.Api
dotnet run --project src/SeoIntelligence.Web
dotnet run --project src/SeoIntelligence.Worker
```

既定のローカル接続先は各 `appsettings.Development.json` と一致させる。

| アプリ | 既定URL/接続先 |
| --- | --- |
| API | `http://localhost:5251` |
| Web | `http://localhost:5295`、API接続先 `http://localhost:5251` |
| Worker | PostgreSQL `localhost:5432`、Redis `localhost:6379` |

Webだけを起動すると、API未起動時に画面へ `localhost:5251` 接続エラーが表示される。Web画面を確認する場合は、先にAPIを起動する。

### 3.3 Dockerでのアプリ起動

```powershell
docker compose up -d postgres redis
docker compose --profile tools run --rm --build migrate
docker compose up -d --build --wait api worker web
docker compose ps
```

`migrate`はEF migration bundleを実行するone-shot target（SDK非搭載の小型runtime image）である。API/Workerは起動時にMigrationを自動適用しないため、初回とMigration追加後に明示実行する。API `/readyz`は未適用Migrationを検知してunhealthyを返すため、`migrate`を飛ばすとapiのhealthcheckが成功せず`up --wait`が失敗する。APIとWorkerは`seo-storage` Volumeを`/data/storage`へ共有mountし、Webは`web-data-protection` VolumeへData Protection keysを保存する（保存先は`DataProtection__KeysPath`で指定）。

### 3.4 接続確認コマンド

```powershell
docker compose ps
Invoke-WebRequest -UseBasicParsing http://localhost:5251/healthz
Invoke-WebRequest -UseBasicParsing http://localhost:5251/readyz
Invoke-WebRequest -UseBasicParsing "http://localhost:5251/api/projects?page=1&pageSize=5"
```

上記は3.3でコンテナ版APIを起動済みの確認手順であり、同時に`dotnet run`しない。ホスト版APIを確認する場合はコンテナ版`api`を停止してから起動する。`/healthz` はAPIホストの起動確認、`/readyz` はDB（接続と未適用Migrationの有無）、Redis、Storage、Secret Storeを含む依存サービス確認に使う。`/readyz` がunhealthyの場合は、`docker compose ps` と `docker compose logs` で対象サービスを確認し、未適用Migrationが報告された場合は`migrate`を実行する。

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
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/smoke-local.ps1
```

`scripts/smoke-test.ps1` はAPI単体のRunbookスモーク、`scripts/smoke-local.ps1` はDocker Compose依存サービス、DB migration、API/Worker/Web起動、マスタ同期ジョブ、CSV出力ジョブまで含めた包括スモークとして使う。
`scripts/smoke-local.ps1 -StopDependencies` はCI向けに `docker compose down --volumes --remove-orphans` 相当まで行うため、ローカルDB/MinIOデータを残したい通常開発では付けない。

実ブラウザ操作まで確認する場合は、PlaywrightのChromiumをインストールしてからBrowserE2Eを有効化する。`dotnet test` で直接BrowserE2Eを実行する場合も、E2Eテストはリポジトリルートの `.env` を自動読み込みする。既にプロセス環境変数が設定されている場合は、プロセス環境変数を優先する。

```powershell
dotnet build tests/E2ETests/E2ETests.csproj
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/smoke-local.ps1 -RunBrowserTests -InstallPlaywrightBrowsers
```

`-InstallPlaywrightBrowsers` はChromiumをインストールする。Windows PowerShell 5.1で `playwright.ps1` が起動できない場合は、同梱Node CLIへフォールバックする。

## 5. 環境変数

| 変数 | 用途 | ローカル例 |
| --- | --- | --- |
| `DOTNET_ENVIRONMENT` | 実行環境（Composeはこの1変数で全サービスを切り替える） | `Development` |
| `ASPNETCORE_ENVIRONMENT` | 実行環境（ホスト`dotnet run`時。未設定なら`DOTNET_ENVIRONMENT`を使用） | `Development` |
| `Database__Host` / `Database__Port` / `Database__Name` / `Database__Username` / `Database__Password` | PostgreSQL接続の個別指定。`Database__Host`設定時は`ConnectionStrings__Default`より優先され、アプリが接続文字列を組み立てるためパスワードに任意の文字を安全に使える。Composeはこちらを使用 | `postgres` / `5432` / `seo` / `seo` / `seo_dev_password` |
| `ConnectionStrings__Default` | PostgreSQL接続（完全な接続文字列。`Database__Host`未設定時に使用され、ホスト開発のappsettingsが該当） | `Host=localhost;Port=5432;Database=seo;Username=seo;Password=seo_dev_password` |
| `Database__GssEncryptionMode` | Npgsql GSS暗号化モード（任意GSS探索ログの抑止） | `Disable` |
| `Redis__ConnectionString` | Redis接続 | `localhost:6379` |
| `Api__BaseUrl` | WebからAPIへの接続先 | ホスト起動は`http://localhost:5251`、Composeは`http://api:8080` |
| `DataProtection__KeysPath` | WebのData Protection keys保存先（未設定時はContentRoot配下`.data/data-protection-keys`） | Composeは`/app/.data/data-protection-keys` |
| `Hangfire__Storage` | Hangfire storage | `PostgreSQL` |
| `Storage__Provider` | ローデータ/出力保存先 | MVP既定は`Local`。`MinIO`は疎通確認のみ。 |
| `Storage__BasePath` | ローカル保存先 | `./.data/storage` |
| `Storage__Endpoint` | MinIO API URL | `http://localhost:9000` |
| `Storage__BucketName` | Storage bucket | `seo-intelligence` |
| `SecretStore__Provider` | Secret Store実装 | `Configuration` |
| `SecretStore__ConfigurationPrefix` | User Secrets/環境変数上のSecret prefix | `Secrets` |
| `ServiceAuthentication__ServiceKeyRef` | APIサービスキーのSecret参照名 | `ApiServiceKey` |
| `Secrets__ApiServiceKey` | APIサービスキーの実値。APIとWebへ同じ値を設定する | `local-development-service-key`（Development既定） |
| `AdminSeed__Email` | 初期管理者のメールアドレス。Adminが存在しない場合だけ作成 | `developer@localhost` |
| `AdminSeed__Password` | 初期管理者のパスワード。12文字以上、大文字/小文字/数字/記号必須 | `LocalDev!Passw0rd` |
| `AdminSeed__DisplayName` | 初期管理者の表示名 | `Developer` |
| `OpenTelemetry__Enabled` | OTel exporter有効化 | `false` |
| `OpenTelemetry__ServiceName` | OTel service name | `SeoIntelligence.Api` |
| `OpenTelemetry__OtlpEndpoint` | OTLP endpoint | `http://localhost:4317` |
| `RakkoKeyword__Mode` | ラッコAPI連携モード | 通常開発/CIは`Mock`。実API接続時のみ`Real` |
| `RakkoKeyword__BaseUrl` | ラッコAPI URL | `https://api.rakkokeyword.com` |
| `RakkoKeyword__ApiKeySecretRef` | APIキーSecret参照名 | `rakko-keyword-api-key-dev` |
| `RakkoKeyword__TimeoutSeconds` | 通常APIタイムアウト | `30` |
| `RakkoKeyword__LongTimeoutSeconds` | 長時間APIタイムアウト | `60` |
| `Jobs__SearchVolumePollIntervalSeconds` | 検索ボリュームポーリング | `60` |
| `Jobs__RankCheckPollIntervalSeconds` | 順位チェックポーリング | `60` |
| `Credits__ResetTimeZone` | クレジットリセットTZ | `Asia/Tokyo` |
| `Discord__DefaultWebhookSecretName` | Discord Webhook Secret名 | `discord-webhook-dev` |
| `Ai__Provider` | AI Provider | `Disabled`またはProvider名 |
| `E2E_BROWSER_ENABLED` | BrowserE2E実行フラグ | `true` |
| `E2E_WEB_URL` | BrowserE2E対象Web URL | `http://localhost:5295` |
| `E2E_API_URL` | BrowserE2E対象API URL | `http://localhost:5251` |
| `E2E_API_SERVICE_KEY` | BrowserE2EがAPIへ提示するサービスキー | `Secrets__ApiServiceKey`と同じ値 |
| `E2E_ADMIN_EMAIL` | BrowserE2Eのサインインに使うメールアドレス | `AdminSeed__Email`と同じ値 |
| `E2E_ADMIN_PASSWORD` | BrowserE2Eのサインインに使うパスワード | `AdminSeed__Password`と同じ値 |
| `E2E_HEADLESS` | BrowserE2Eのheadless切替 | 通常は未設定。画面表示時のみ`false` |

Compose内のAPI/WorkerはPostgreSQLを`postgres:5432`、Redisを`redis:6379`、Local Storageを`/data/storage`で参照する。VPS用の環境変数とCaddy networkは `.env.production.example` と `docs/docker_deployment.md` を正本とする。

## 6. Secret管理

ローカル開発では `.env.example` を `.env` にコピーし、Discord Webhook URLなどの実値は `.env` にだけ置く。`.env` はGit管理対象外で、PowerShellのスモークスクリプトとE2Eテストは起動時に自動読み込みする。既にプロセス環境変数が設定されている場合は、そちらを優先する。

Compose起動時は `.env` をAPI/Workerの任意`env_file`としても読み込む。VPSでは `.env.production.example` を `.env.production` へコピーし、ファイル権限を制限する。Dockerfileのbuild引数やimage layerへSecretを含めない。

`SecretStore__Provider=Configuration` のSecret Storeはプロセス内の設定を読み書きする。API経由で登録した秘密値（`secretValue`）はプロセス再起動で失われ、APIとWorkerの間でも共有されない。継続利用する秘密値は `Secrets__<参照名>` 形式の環境変数またはUser SecretsでAPIとWorkerの両方に設定し、APIへは参照名（`keyRef`等）だけを登録する。

| Secret | 用途 | 注意 |
| --- | --- | --- |
| `rakko-keyword-api-key-dev` | ラッコキーワードAPIキー | DBへ実値保存しない。 |
| `discord-webhook-dev` | Discord Webhook URL | DBへ実値保存しない。 |
| `ai-api-key-dev` | AI APIキー | Phase 3。必要時のみ設定。 |
| `ApiServiceKey` | WebがAPIへ提示するサービスキー | APIとWebへ同じ値を設定する。ずれるとWebのAPI呼び出しが全て401になる。VPSでは`openssl rand -hex 32`で生成する。 |

管理者パスワードはSecret Storeではなく、ASP.NET Core Identityが`identity_users.password_hash`にハッシュとして保存する。`AdminSeed__Password`は初回シード時にだけ読まれるため、初回サインイン後は画面からパスワードを変更し、環境変数の値を空にする。

ローカルでは `.env`、.NET User Secrets、開発用Key Vault、または安全なSecret管理を使う。MVPの`Configuration` Secret Storeは `Secrets:{secretName}` を参照する。たとえば `.env` に `Secrets__discord-webhook-dev=<Webhook URL>` を置くと、`webhook_secret_ref=discord-webhook-dev` から解決される。ラッコキーワードAPIへ実接続する場合は `RakkoKeyword__Mode=Real`、`RakkoKeyword__ApiKeySecretRef=rakko-keyword-api-key-dev`、`Secrets__rakko-keyword-api-key-dev=<API Key>` を設定する。通常開発とCIは既定の`Mock`を使い、実APIキーは不要。APIが`secretValue`を受け取った場合は生成したSecret名へ登録し、DBには参照名だけを保存する。`Configuration`実装のAPI経由登録はプロセス内の設定値として扱い、`.env`や設定ファイルへ実値をコミットしない。

## 7. DB初期化

| 手順 | 内容 |
| --- | --- |
| 1 | PostgreSQLを起動する。 |
| 2 | EF Core migrationsを適用する。 |
| 3 | 初回マイグレーションで`pg_trgm`拡張を有効化する。 |
| 4 | 既定workspaceを1件作成する。 |
| 5 | `api_contract_scopes`へ契約確認結果を初期データとして登録する。管理画面/APIでは更新しない。 |
| 6 | 地域/言語マスタ同期ジョブを実行する。 |

ホストの.NET SDKを使う場合は以下でDBを更新する。

```text
dotnet ef database update --project src/SeoIntelligence.Infrastructure --startup-project src/SeoIntelligence.Api
dotnet run --project src/SeoIntelligence.Api
dotnet run --project src/SeoIntelligence.Worker
```

Dockerだけで更新する場合:

```text
docker compose --profile tools run --rm --build migrate
```

現時点のmigration dry-runは `scripts/migration-dry-run.ps1` / `scripts/migration-dry-run.sh` で実行する。`DbContext` が未実装の場合は、雛形としてskipして成功終了する。

## 8. 起動確認

| 確認 | 期待結果 |
| --- | --- |
| API Health | `/healthz`が成功する（サービスキー不要）。 |
| Readiness | `/readyz`でDB、Redis、Storage、Secret Storeの疎通確認ができる（サービスキー不要）。未設定のDB/Redisはskip扱い、設定済みで接続不可または未適用Migrationがある場合はunhealthy。 |
| API認証 | サービスキーなしの`GET /api/projects`が401を返し、`X-Service-Key`付きで200を返す。 |
| OpenAPI | `X-Service-Key`付きで`/openapi/v1.json`が取得できる。 |
| プロジェクト一覧 | `X-Service-Key`付きの`GET /api/projects`が成功する。 |
| Webサインイン | 未サインインで`/dashboard`が`/login`へリダイレクトされ、初期管理者でサインインできる。 |
| Worker | ジョブ一覧にWorker処理結果が反映される。 |
| Discord | テスト通知が送信され履歴が残る。 |

APIをcurlで確認する例:

```bash
curl --silent --output /dev/null --write-out '%{http_code}\n' http://localhost:5251/api/projects
curl --fail --silent --header "X-Service-Key: local-development-service-key" "http://localhost:5251/api/projects?page=1&pageSize=5"
```

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
| `build-test-smoke` | restore、build、test、migration dry-run、包括スモーク（Docker Compose依存サービス起動、依存サービスready待機、DB migration適用、API/Worker/Web起動、ジョブ完了確認）。リポジトリ変数 `RUN_BROWSER_E2E=true` の場合のみPlaywright BrowserE2Eも実行する。 |
| `container-scan` | PostgreSQL、Redis、MinIO、MinIO Clientのコンテナイメージをリトライ付きでpullし、Trivyでvuln-only scanする。初期雛形では検出結果を表示し、fail条件は後続の運用品質ゲートで調整する。 |

`build-test-smoke`は開発用/VPS用Composeの構文検証、Web/API/Worker/Migration imageのbuild（GitHub Actionsレイヤーキャッシュ使用）、`scripts/container-smoke.sh`による隔離Compose project上のコンテナ起動スモークも行う。スモークはMigration、HTTP、非root UID、Storage共有、Data Protection keys永続化を確認後、テスト用コンテナとVolumeを削除する。同じスモークはローカルでも `bash scripts/container-smoke.sh` で実行できる。

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
| マネージド環境向けデプロイ手順 | Azure等のステージング環境作成後。小規模VPS手順は`docker_deployment.md`へ記載済み。 |
