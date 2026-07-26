# Docker / VPSデプロイ手順

本書はVPSデプロイの正本である。ローカル開発の正本は [`environment_setup.md`](environment_setup.md) とする。

## 1. 対象構成

本書は、Web、API、Worker、PostgreSQL、RedisをDocker Composeで起動し、別Composeの共通Caddyから公開する単一利用者向けの暫定VPS構成を対象とする。アプリ内認証が実装されるまで、正式な本番相当構成ではなく、外部認証ゲートで保護した個人利用構成として扱う。

```text
Common Caddy
  |-- /api/* --> seo-api:8080
  `-- other  --> seo-web:8080

seo-intelligence-prod
  |-- web
  |-- api ------ PostgreSQL / Redis
  |-- worker --- PostgreSQL / Redis
  `-- api + worker share Local Storage volume
```

使用するファイル:

| ファイル | 用途 |
| --- | --- |
| `Dockerfile` | `web`、`api`、`worker`、`migrate`のmulti-stage build。共有buildステージで1回だけコンパイルし、`migrate`はEF migration bundle（SDK非搭載の小型runtime image）。 |
| `compose.yaml` | 開発/本番共通のbase定義。ホストポートは公開しない。 |
| `compose.override.yaml` | 開発専用overlay。`docker compose`が自動読込。`127.0.0.1`bindの公開ポートとMinIOはここだけにある。 |
| `compose.production.yaml` | VPS用overlay。Production環境変数、必須パスワード、network、専用project name（`seo-intelligence-prod`）の差分のみ。 |
| `.env.production.example` | VPS設定の雛形。実値は`.env.production`へ置き、Gitへ追加しない。 |

本番のすべてのコマンドは次の形で実行する（`compose.override.yaml`を含めてはならない）。

```bash
docker compose --env-file .env.production -f compose.yaml -f compose.production.yaml <command>
```

本番projectは`seo-intelligence-prod`という専用project nameを持つため、同一ホストに開発スタック（`seo-intelligence-platform`）があってもVolumeやコンテナを共有しない。

接続文字列はアプリ側が`Database__Host`等の個別環境変数から組み立てる（`DatabaseConnectionStringResolver`）。`POSTGRES_PASSWORD`は任意の文字を安全に使用できる。

現行のMinIO adapterはreadiness確認のみで、オブジェクトの書き込み・読み取りには対応していない。そのためVPS構成は`Storage__Provider=Local`を使用し、MinIOは開発overlayの任意profileとしてのみ存在する。

## 2. ローカルで全スタックを起動する

ローカル手順の正本は [`environment_setup.md`](environment_setup.md) 3.3節。コンテナスモークは `bash scripts/container-smoke.sh`（隔離projectで実行され、開発スタックへ影響しない）。

## 3. VPSの初回デプロイ

### 3.1 設定ファイルと共通network

```bash
cp .env.production.example .env.production
chmod 600 .env.production
```

`.env.production`の`POSTGRES_PASSWORD`を空欄から変更する。値は任意の文字で安全だが、`openssl rand -hex 32` などの生成値を推奨する。Real APIを利用するまでは`RakkoKeyword__Mode=Mock`のままにする。Secret実値は`Secrets__<参照名>`形式でAPIとWorkerの両方へ同じ値が渡るよう、このファイルまたは外部Secret Storeで管理する。`APP_ENV_FILE=.env.production`の行は変更しない（api/workerがこのファイルをenv_fileとして読むための設定）。

共通CaddyのComposeと共有するexternal networkを一度だけ作成する。

```bash
docker network create seo-intelligence-caddy
```

既存network名が異なる場合は`.env.production`の`CADDY_NETWORK`を合わせ、Caddyサービスも同じexternal networkへ接続する。共通Caddyは複数networkへ参加できるため、他アプリ用networkと分け、このstackとCaddyだけが参加するnetworkにする。

共通Caddy側のComposeには、既存networkに加えて次を追加する。

```yaml
services:
  caddy:
    networks:
      - seo-intelligence

networks:
  seo-intelligence:
    external: true
    name: seo-intelligence-caddy
```

### 3.2 build、Migration、起動

Migration前にPostgreSQLとStorageのバックアップを確認する。

```bash
docker compose --env-file .env.production -f compose.yaml -f compose.production.yaml config --quiet
docker compose --env-file .env.production -f compose.yaml -f compose.production.yaml build
docker compose --env-file .env.production -f compose.yaml -f compose.production.yaml up -d postgres redis
docker compose --env-file .env.production -f compose.yaml -f compose.production.yaml --profile tools run --rm migrate
docker compose --env-file .env.production -f compose.yaml -f compose.production.yaml up -d --wait api worker web
docker compose --env-file .env.production -f compose.yaml -f compose.production.yaml ps
```

MigrationはAPI起動時に自動適用しない。`migrate`はEF migration bundleを実行するone-shotコンテナで、適用完了後に終了する。API `/readyz`は未適用Migrationを検知してunhealthyを返すため、`migrate`を飛ばした場合はapiのhealthcheckが成功せず`up --wait`が失敗する。手順としてもMigration→起動の順序を守る。

api/webにはコンテナhealthcheck（`/readyz`・`/healthz`）があり、`up -d --wait`はhealthyになるまで待つ。`ps`のSTATUSが`healthy`であることがデプロイ完了のシグナルである。

### 3.3 Caddy設定例

`seo-web`と`seo-api`は`CADDY_NETWORK`上の一意なaliasである。共有URLなどの`/api/*`を使うため、同一ホスト上でAPIとWebを振り分ける。

```caddyfile
seo.example.com {
    basic_auth {
        admin <CADDY_PASSWORD_HASH>
    }

    handle /api-healthz {
        rewrite * /healthz
        reverse_proxy seo-api:8080
    }

    handle /api-readyz {
        rewrite * /readyz
        reverse_proxy seo-api:8080
    }

    @api path /api /api/*
    handle @api {
        reverse_proxy seo-api:8080
    }

    handle {
        reverse_proxy seo-web:8080
    }
}
```

Caddyの`reverse_proxy`はBlazor Interactive Serverの`/_blazor` WebSocketも中継する。パスワードは平文でCaddyfileへ書かず、Caddyの対話的なpassword hash生成機能で作成したhashを使う。

アプリ内の認証・認可は未実装である。外部公開時は、少なくともCaddyのBasic認証、VPN、Cloudflare Accessなどの認証ゲートを必須とする。これは将来のアプリ内単一管理者ログインを不要にするものではない。

## 4. 更新手順

```bash
git pull
docker compose --env-file .env.production -f compose.yaml -f compose.production.yaml build
docker compose --env-file .env.production -f compose.yaml -f compose.production.yaml stop web api worker
# この停止中に5.1のDB/Storage/keyバックアップを取得する
docker compose --env-file .env.production -f compose.yaml -f compose.production.yaml --profile tools run --rm migrate
docker compose --env-file .env.production -f compose.yaml -f compose.production.yaml up -d --wait --force-recreate api worker web
docker compose --env-file .env.production -f compose.yaml -f compose.production.yaml ps
docker compose --env-file .env.production -f compose.yaml -f compose.production.yaml logs --tail 200 web api worker
```

build成功後にWeb/API/Workerを停止し、書き込みを止めてからバックアップとMigrationを行う。この間はメンテナンス時間となる。停止後からMigration完了まで旧imageのAPI/Workerを起動してはならない。実行中Workerによるjob statusの上書きと、旧APIによる旧形式ジョブの再登録を防ぐため、この順序は`RakkoKeywordV1120DataBackfill`の必須適用条件である。Migrationが失敗した場合は新imageを起動せず、ログとDBバックアップを確認する。`up -d --wait`が成功し`ps`が`healthy`を示し、後述のURLが成功するまでデプロイ完了と判定しない。

Workerだけを再起動する場合:

```bash
docker compose --env-file .env.production -f compose.yaml -f compose.production.yaml restart worker
```

設定またはimage変更をWorkerへ反映する場合は`restart`ではなく再作成する。

```bash
docker compose --env-file .env.production -f compose.yaml -f compose.production.yaml up -d --force-recreate worker
```

## 5. 永続データとバックアップ

| Volume | 内容 | 共有 |
| --- | --- | --- |
| `postgres-data` | 業務DBとHangfire PostgreSQL storage | PostgreSQLのみ |
| `redis-data` | Redis AOF | Redisのみ |
| `seo-storage` | ローデータ、CSV/Excel/PDF、レポート | APIとWorker |
| `web-data-protection` | Blazor/antiforgery用Data Protection keys | Webのみ |

実Volume名には本番project名のprefixが付く（例: `seo-intelligence-prod_seo-storage`）。

通常の停止は`docker compose ... down`までとし、`down -v`は使用しない。`-v`はDB、Storage、鍵を削除する。PostgreSQLの論理バックアップと`seo-storage`のバックアップを同じ復旧時点で取得する。

### 5.1 手動バックアップ

次はメンテナンス時間中の手動取得例である。`compose.production.yaml`の`name: seo-intelligence-prod`を変更した場合はVolume名も合わせる。

```bash
BACKUP_DIR="$(pwd)/backups/$(date -u +%Y%m%dT%H%M%SZ)"
mkdir -p "$BACKUP_DIR"
docker compose --env-file .env.production -f compose.yaml -f compose.production.yaml stop web api worker
docker compose --env-file .env.production -f compose.yaml -f compose.production.yaml exec -T postgres sh -c 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Fc' > "$BACKUP_DIR/postgres.dump"
docker run --rm --entrypoint tar --volume seo-intelligence-prod_seo-storage:/source:ro --volume "$BACKUP_DIR:/backup" postgres:16-alpine -C /source -czf /backup/seo-storage.tar.gz .
docker run --rm --entrypoint tar --volume seo-intelligence-prod_web-data-protection:/source:ro --volume "$BACKUP_DIR:/backup" postgres:16-alpine -C /source -czf /backup/web-data-protection.tar.gz .
```

バックアップだけを行う場合は確認後に`api worker web`を再起動する。更新作業中は停止したままMigrationへ進む。dumpとarchiveのサイズが0でないことを確認し、VPS外の暗号化された保管先へ転送する。

### 5.2 復元検証

復元は最初に空の隔離Compose projectまたはステージング相当で行う。確認済みの空DBへ`pg_restore --no-owner`でdumpを戻し、空のStorage/Data Protection Volumeへ各archiveを展開する。その後、Migration状態、`/api-readyz`、DB利用API、Workerジョブ、成果物読取を確認する。稼働中の本番Volumeへ直接上書きしない。

このComposeだけでは日次スケジュール、WAL/PITR、VPS外冗長化は提供しない。NFR-011を満たすには、ホスティング側のsnapshot/backupまたは外部PostgreSQL/Storageサービスを別途構成する。

## 6. 確認とトラブルシュート

```bash
docker compose --env-file .env.production -f compose.yaml -f compose.production.yaml ps
docker compose --env-file .env.production -f compose.yaml -f compose.production.yaml logs --tail 200 web api worker postgres redis
```

確認観点:

- `ps`でapi/webが`healthy`である（api healthcheckは`/readyz`、webは`/healthz`）。
- Caddy経由のWeb、`/healthz`、`/api-healthz`、`/api-readyz`、`/api`が成功する。
- `GET /api/projects`などDBを使うAPIが成功する。
- WorkerログにHangfire Server起動が記録され、ジョブが`succeeded`へ進む。
- API `/api-readyz`でDB（接続と未適用Migrationなし）、Redis、Storage、Secret Storeが正常である。
- Web/Worker再起動後もData Protection keysとStorage成果物が残る。

apiが`unhealthy`で`/api-readyz`のdbが未適用Migrationを報告する場合は、4章の手順で`migrate`を実行する。

API、DB、Redisにはホスト`ports`を設定していない。調査目的でも安易に公開せず、Composeログ、DBコンテナ内のCLI、または認証済みCaddy経路を使う。

## 7. 現行実装の制約

- アプリ内の単一管理者ログインは未実装であり、VPS公開時は外部の認証ゲートが必要である。
- Local Storageの成果物URLは現時点では`storage://local/...`形式で、期限だけをqueryへ付与する。ブラウザへファイル本体を配信するHTTP adapterは別Issueであり、Docker化だけではこのURLを直接ダウンロードできない。
- MinIO adapterはreadiness確認のみである。署名付きS3 adapterが実装されるまで、`Storage__Provider=MinIO`を成果物保存へ使用しない。
