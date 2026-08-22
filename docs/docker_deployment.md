# Docker / VPSデプロイ手順

本書はVPSデプロイの正本である。ローカル開発の正本は [`environment_setup.md`](environment_setup.md) とする。

## 1. 対象構成

本書は、Web、API、Worker、PostgreSQL、RedisをDocker Composeで起動し、別Composeの共通Caddyから公開する単一利用者向けVPS構成を対象とする。アプリ内の単一管理者ログイン（ASP.NET Core Identity）とAPIサービスキーで保護する。詳細は `docs/adr/0008-aspnet-core-identity-auth.md` を参照する。

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
| `.env.production.example` | VPS設定の雛形。実値は`.env.production`へ置き、Gitへ追加しない。Composeが補間する値だけを持つ。 |
| `.env.production.app.example` | アプリ設定とアプリSecretの雛形。実値は`.env.production.app`へ置き、Gitへ追加しない。api/workerだけが`env_file`として読む。 |

本番のすべてのコマンドは次の形で実行する（`compose.override.yaml`を含めてはならない）。

```bash
docker compose --project-name seo-intelligence-prod --env-file .env.production -f compose.yaml -f compose.production.yaml <command>
```

`--project-name`を必ず付ける。`compose.production.yaml`の`name:`は指定するが、シェルの`COMPOSE_PROJECT_NAME`がそれより優先されるため、環境変数が設定されていると別projectのVolumeへMigrationを実行したり、別のコンテナを停止したりし得る。`scripts/deploy-production.sh`は常に明示する。

本番projectは`seo-intelligence-prod`という専用project nameを持つため、同一ホストに開発スタック（`seo-intelligence-platform`）があってもVolumeやコンテナを共有しない。

接続文字列はアプリ側が`Database__Host`等の個別環境変数から組み立てる（`DatabaseConnectionStringResolver`）。`POSTGRES_PASSWORD`は任意の文字を安全に使用できる。

環境ファイルは2つに分ける。`--env-file`が指す`.env.production`はComposeが補間に使うだけでコンテナへは自動投入されず、`compose.yaml`が明示的にマッピングしたキーだけが各サービスへ届く。アプリ設定とアプリSecretは`.env.production.app`に置き、外部APIを呼ぶapiとworkerだけが`env_file`として読む。Webは`env_file`を持たないため、ラッコキーワードAPIキーとDiscord Webhookはインターネットに面するサービスへ渡らない。この分離は`scripts/verify-production-compose.sh`が検証する。新しいアプリSecretは`.env.production.app`側へ追加する。

現行のMinIO adapterはreadiness確認のみで、オブジェクトの書き込み・読み取りには対応していない。そのためVPS構成は`Storage__Provider=Local`を使用し、MinIOは開発overlayの任意profileとしてのみ存在する。

## 2. ローカルで全スタックを起動する

ローカル手順の正本は [`environment_setup.md`](environment_setup.md) 3.3節。コンテナスモークは `bash scripts/container-smoke.sh`（隔離projectで実行され、開発スタックへ影響しない）。

## 3. VPSの初回デプロイ

### 3.1 設定ファイルと共通network

```bash
cp .env.production.example .env.production
cp .env.production.app.example .env.production.app
chmod 600 .env.production .env.production.app
```

`.env.production`の`POSTGRES_PASSWORD`を空欄から変更する。値は任意の文字で安全だが、`openssl rand -hex 32` などの生成値を推奨する。`APP_ENV_FILE=.env.production.app`の行は変更しない（api/workerがこのファイルをenv_fileとして読むための設定）。

`.env.production.app`側では、Real APIを利用するまで`RakkoKeyword__Mode=Mock`のままにする。Secret実値は`Secrets__<参照名>`形式で置き、APIとWorkerの両方へ同じ値が渡るようにする。Configuration Secret Storeはプロセス内設定を読むだけなので、管理APIから登録した秘密値は再起動で失われ、APIとWorker間でも共有されない。永続させる秘密値は必ずこのファイルか外部Secret Storeで管理する。

あわせて認証まわりの必須値を設定する。

| 変数 | 必須 | 内容 |
| --- | --- | --- |
| `API_SERVICE_KEY` | 必須 | WebがAPIへ提示する共有シークレット。`openssl rand -hex 32` で生成する。未設定だと`config`が失敗する。 |
| `ADMIN_SEED_EMAIL` | 初回のみ | 初期管理者のメールアドレス。Adminユーザーが1件も存在しない場合だけ作成される。 |
| `ADMIN_SEED_PASSWORD` | 初回のみ | 初期管理者のパスワード。12文字以上、大文字/小文字/数字/記号を各1文字以上含める。 |
| `ADMIN_SEED_DISPLAY_NAME` | 任意 | 画面表示名。未設定時は`Admin`。 |

初回サインイン後は画面の「アカウント」からパスワードを変更し、`.env.production`の`ADMIN_SEED_EMAIL`と`ADMIN_SEED_PASSWORD`を空にする。Adminユーザーが既に存在すればシードは実行されないため、空のままで更新・再起動・Migrationを含む以降のCompose操作は成功する。

`compose.production.yaml`のAdminシード指定は削除しないこと。削除すると`compose.yaml`の開発用既定値がProductionへ入り、Adminが失われたDBで既知の資格情報から管理者が再作成されてしまう。空値で上書きする現在の形を維持する。

Adminが1人も存在せず、かつシード資格情報も空の場合、Webコンテナは起動に失敗する。誰もサインインできない状態でhealthyになることを防ぐための意図的な動作である。

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
bash scripts/deploy-production.sh initial
```

手順は個別コマンドの列挙ではなくスクリプトで実行する。**スキャン失敗でデプロイを中断させるため**である。同じコマンドをシェルへ順に貼り付けた場合、スキャンが非0で終了しても次の`up`が実行され、脆弱なimageが起動してしまう。スクリプトは`set -euo pipefail`で中断する。

スクリプトが行うこと:

| 順 | 内容 | 理由 |
| --- | --- | --- |
| 1 | `config --quiet` | Compose定義の検証。 |
| 2 | `build api web worker migrate` | **サービス名を明示する**。`migrate`は`tools` profile配下にあり、省略するとbuild対象から外れる。初回はmigrate imageが存在せず、更新時は前リリースのbundleでMigrationを実行してしまう。 |
| 3 | `scan-container-images.sh app` | **buildの後・起動の前**。前ならば前リリースのimageを、後ならば既に稼働中のimageを検査することになる。 |
| 4 | `up -d postgres redis` | 依存サービスの起動。 |
| 5 | `--profile tools run --rm migrate` | Migration適用。 |
| 6 | `up -d --wait api worker web` | アプリ起動とhealthy待ち。 |
| 7 | `ps` | 状態確認。 |

この順序は`scripts/verify-production-compose.sh`が検証し、`scripts/verify-deployment-guards.sh`が「検証自体が機能しなくなっていないこと」を検証する。

CIも同じスキャンを行うが、VPSは同じコミットから再buildするため、CIが検査したimageと本番が起動するimageは同一とは限らない（`mcr.microsoft.com/dotnet/*`のタグ、`apt-get`、NuGet restoreはいずれも可変である）。**本番で実際に動くimageを保証できるのは手順3の実行だけ**である。より強い保証が必要なら、CIでbuild・スキャンしたimageをregistryへpushし、本番はそのdigestを`pull`して`up --no-build`する構成へ移行する。現構成はregistryを前提にしていない。

MigrationはAPI起動時に自動適用しない。`migrate`はEF migration bundleを実行するone-shotコンテナで、適用完了後に終了する。API `/readyz`は未適用Migrationを検知してunhealthyを返すため、`migrate`を飛ばした場合はapiのhealthcheckが成功せず`up --wait`が失敗する。手順としてもMigration→起動の順序を守る。

api/webにはコンテナhealthcheck（`/readyz`・`/healthz`）があり、`up -d --wait`はhealthyになるまで待つ。`ps`のSTATUSが`healthy`であることがデプロイ完了のシグナルである。

### 3.3 Caddy設定例

`seo-web`と`seo-api`は`CADDY_NETWORK`上の一意なaliasである。

APIの公開面は最小化する。Webは内部network経由でAPIを呼ぶため、`/api/*`をインターネットへ出す必要があるのはレポート共有URLだけである。共有URLは社外へ渡す前提で匿名アクセスを許可しており、共有トークン自体がアクセス制御になる。

```caddyfile
seo.example.com {
    handle /api-healthz {
        rewrite * /healthz
        reverse_proxy seo-api:8080
    }

    # レポート共有URLだけをAPIへ通す。それ以外の /api/* は公開しない。
    @report_shares path /api/report-shares/*
    handle @report_shares {
        reverse_proxy seo-api:8080
    }

    # 必須。共有トークンはBearer資格情報に相当し、パスに含まれる。
    # Caddyのアクセスログが有効な場合、これがないと request.uri としてトークンが平文で残る。
    log_skip @report_shares

    handle {
        reverse_proxy seo-web:8080
    }
}
```

**`log_skip @report_shares` は省略しないこと。** アプリ側はAPIログと監査ログからトークンを除去済みだが、前段のCaddyがURIを記録すれば漏えいは成立する。共有URLを外部へ渡す前に、実際のCaddy設定でアクセスログの有無を確認し、有効なら上記を適用する。Cloudflareなど別のリバースプロキシを前段に置く場合は、そちらのログ設定も同様に確認する。

適用後、実際に記録されないことを確認する。有効な共有トークンではなく、**一意な無効プローブ文字列**でアクセスする（有効なトークンを検証に使うと、その値自体をログや履歴へ広げてしまう）。

```bash
PROBE="logprobe-$(openssl rand -hex 16)"
curl -s -o /dev/null "https://seo.example.com/api/report-shares/${PROBE}"
```

そのうえで、**Caddyに設定されている全てのログ出力先**から `${PROBE}` を完全一致で検索する。Caddyのアクセスログはstdout/stderrだけでなくファイルやネットワークsinkへも出力できるため、`docker compose logs` だけでは不十分である。まず出力先を確認する。

```bash
# Caddyfile / caddy.json で log の output 設定を確認する
grep -n -A 3 'log ' <Caddyfileのパス>
```

確認できた各sinkに対して検索する。

```bash
docker compose -f <caddyのcompose> logs --no-color caddy | grep -F "$PROBE"
grep -rF "$PROBE" <アクセスログのファイルパス>
```

いずれにも一致がなければ`log_skip`が効いている。共有トークンはBase64URLで `-` や `_` を含み得るため、パターンではなく完全一致（`grep -F`）で検索する。

Caddyの`reverse_proxy`はBlazor Interactive Serverの`/_blazor` WebSocketも中継する。

Caddyのcatch-allにより、Webホストの`/readyz`は外部から到達する。これは`self`チェックだけを返す軽量なもので問題ない。公開してはならないのは**API**の`/readyz`である。以下は後者を指す。

APIの`/readyz`はインターネットへ公開しない。匿名で到達でき、1リクエストごとにDBクエリ、Redis ping、Storageへの実ファイル書込/読込/削除、Secret Storeアクセスを実行するため、無認証の負荷増幅点になる。未適用Migrationがある場合はMigration名も応答へ含む。Readinessの確認はVPS内部から行う。

```bash
docker compose --project-name seo-intelligence-prod --env-file .env.production -f compose.yaml -f compose.production.yaml exec api curl --fail --silent http://localhost:8080/readyz
```

`/healthz`は`self`チェックだけを返し外部依存へ触れないため、`/api-healthz`として公開してよい。

APIは`X-Service-Key`ヘッダーが一致しない要求を401で拒否するため、仮に`/api/*`を広く公開してしまってもWeb以外からは利用できない。Caddy側の絞り込みは多層防御である。

`web-writing-tool`など他アプリと同一VPSへ同居させる場合は、**別サブドメインで公開する**こと。認証Cookieは`__Host-`接頭辞を使うため、同一ホスト名でパス分割するとCookie名が衝突する。

アプリ内に単一管理者ログインを実装済みだが、Caddy Basic認証、VPN、Cloudflare Accessなどの外部認証ゲートは多層防御として併用を推奨する。併用する場合、パスワードは平文でCaddyfileへ書かず、Caddyの対話的なpassword hash生成機能で作成したhashを使う。ただし共有URLを社外へ渡す運用では、`/api/report-shares/*`を外部ゲートの対象外にする必要がある。

## 4. 更新手順

デプロイ対象は、検証済みのコミットまたはリリースタグを明示して取得する。`git pull` だけでは検証していないコミットが混入し得る。

```bash
git fetch --tags origin
git -c advice.detachedHead=false checkout <検証済みコミットSHAまたはタグ>
git status --short   # 出力が空であること
bash scripts/deploy-production.sh update
```

実行順は build → スキャン → 停止 → **バックアップ** → Migration → 再作成で、途中で失敗すれば中断する。

**バックアップはスクリプトが取得する。** 手動手順にしない理由は、正しい取得タイミングが「アプリ停止後・Migration前」という狭い窓であり、外すと復旧が必要になったときに初めて判明するためである。取得先は `backups/<UTCタイムスタンプ>/` で、PostgreSQLのdump、`seo-storage`、`web-data-protection`の3点を取る。取得後にすべて読み戻して検証し、1つでも復元できない状態ならMigrationへ進まず中断する（検証内容は5.1の表）。

取得後はVPS外の暗号化された保管先へ転送する。5.1は手動で取得する場合の参照手順として残す。

build成功後にWeb/API/Workerを停止し、書き込みを止めてからバックアップとMigrationを行う。この間はメンテナンス時間となる。停止後からMigration完了まで旧imageのAPI/Workerを起動してはならない。実行中Workerによるjob statusの上書きと、旧APIによる旧形式ジョブの再登録を防ぐため、この順序は`RakkoKeywordV1120DataBackfill`の必須適用条件である。Migrationが失敗した場合は新imageを起動せず、ログとDBバックアップを確認する。`up -d --wait`が成功し`ps`が`healthy`を示し、後述のURLが成功するまでデプロイ完了と判定しない。

Workerだけを再起動する場合:

```bash
docker compose --project-name seo-intelligence-prod --env-file .env.production -f compose.yaml -f compose.production.yaml restart worker
```

設定またはimage変更をWorkerへ反映する場合は`restart`ではなく再作成する。

```bash
docker compose --project-name seo-intelligence-prod --env-file .env.production -f compose.yaml -f compose.production.yaml up -d --force-recreate worker
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

更新時は `scripts/deploy-production.sh update` が停止後・Migration前に自動で取得する。更新以外の目的で取得する場合も、同じスクリプトを直接呼ぶ。

```bash
docker compose --project-name seo-intelligence-prod --env-file .env.production -f compose.yaml -f compose.production.yaml stop web api worker
bash scripts/backup-production.sh
docker compose --project-name seo-intelligence-prod --env-file .env.production -f compose.yaml -f compose.production.yaml up -d --wait api worker web
```

手順を文書へ書き写さずスクリプトへ集約しているのは、バックアップの失敗が「復元が必要になるまで気づかない」種類のものだからである。スクリプトは次を検証し、1つでも満たさなければ失敗する。

| 検証 | 理由 |
| --- | --- |
| web/api/workerが停止している | 稼働中に取得するとdumpとStorage archiveが別時点になる。ジョブが2つの取得の間にファイルを書けば、復元しても整合しない。 |
| 出力先が絶対パスである | `docker run -v` は `/` または `./` で始まらないsourceを名前付きVolumeとして扱い、`/`を含む名前は拒否される。相対パスではバックアップ自体が失敗する。 |
| 出力先が既存でない | 既存のバックアップを上書きしない。 |
| 対象Volumeが存在する | 存在しないVolumeを指定するとDockerが空のVolumeを作り、「空だが妥当な」archiveができてしまう。 |
| `postgres.dump` を `pg_restore --list` で読み戻せる | マジックストリングの確認では不十分。切り詰められたdumpも先頭は `PGDMP` である。読み戻せないdumpは復元できない。 |
| dumpが1件以上のオブジェクトを含む | 目次が空のdumpはDBを取得できていない。 |
| 各archiveを `tar -tzv` で読み戻せる | 読み戻しの失敗を「エントリ0件」と区別する。Storageは0件を許容するため、区別しないと壊れたarchiveが通る。 |
| `web-data-protection.tar.gz` に1件以上の `.xml` **ファイル**がある | 空ディレクトリのtar.gz自体は非空なのでサイズ検査では検出できず、ディレクトリだけでもエントリ数では1件になる。Data Protection keyが無ければサインイン中のセッションは復元できない。 |

`seo-storage` は新規デプロイで空があり得るため0件を許容する。

出力先は `backups/<UTCタイムスタンプ>/`（引数で変更可）。取得後はVPS外の暗号化された保管先へ転送する。同一ホストにある限りバックアップとして機能しない。

### 5.2 復元検証

復元は最初に空の隔離Compose projectまたはステージング相当で行う。確認済みの空DBへ`pg_restore --no-owner`でdumpを戻し、空のStorage/Data Protection Volumeへ各archiveを展開する。その後、Migration状態、コンテナ内部からの`/readyz`、DB利用API、Workerジョブ、成果物読取を確認する。稼働中の本番Volumeへ直接上書きしない。

このComposeだけでは日次スケジュール、WAL/PITR、VPS外冗長化は提供しない。NFR-011を満たすには、ホスティング側のsnapshot/backupまたは外部PostgreSQL/Storageサービスを別途構成する。

## 6. 確認とトラブルシュート

```bash
docker compose --project-name seo-intelligence-prod --env-file .env.production -f compose.yaml -f compose.production.yaml ps
docker compose --project-name seo-intelligence-prod --env-file .env.production -f compose.yaml -f compose.production.yaml logs --tail 200 web api worker postgres redis
```

確認観点:

- `ps`でapi/webが`healthy`である（api healthcheckは`/readyz`、webは`/healthz`）。
- Caddy経由のWeb、`/healthz`、`/api-healthz`が成功する。
- `GET /api/projects`などDBを使うAPIが成功する。
- WorkerログにHangfire Server起動が記録され、ジョブが`succeeded`へ進む。
- コンテナ内部から見たAPI `/readyz`でDB（接続と未適用Migrationなし）、Redis、Storage、Secret Storeが正常である。
- Web/Worker再起動後もData Protection keysとStorage成果物が残る。

apiが`unhealthy`の場合は、次でReadinessの内訳を確認する。`db`が未適用Migrationを報告する場合は、4章の手順で`migrate`を実行する。

```bash
docker compose --project-name seo-intelligence-prod --env-file .env.production -f compose.yaml -f compose.production.yaml exec api curl --silent http://localhost:8080/readyz
```

API、DB、Redisにはホスト`ports`を設定していない。調査目的でも安易に公開せず、Composeログ、DBコンテナ内のCLI、または認証済みCaddy経路を使う。

## 7. 現行実装の制約

- 利用者は単一管理者1名を前提とする。複数ユーザー管理、RBAC、SSOは `ISSUE-P4-001` の範囲であり未実装である。
- 二要素認証は未実装である。
- MinIO adapterはreadiness確認のみである。署名付きS3 adapterが実装されるまで、`Storage__Provider=MinIO`を成果物保存へ使用しない。
- 成果物のダウンロードは、APIの`.../content`とWebホストの`/downloads/...`が担う。Local Storageでも画面からCSV/Excel/PDFを取得できる。ファイル本体は常にAPI経由で配信し、Storage Volumeを直接公開しない。
