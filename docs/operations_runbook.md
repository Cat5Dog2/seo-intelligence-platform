# 運用Runbook

**ラッコキーワードAPIを中核にしたSEOインテリジェンス基盤**

_SEO Intelligence Platform / SEOインテリジェンス基盤_

| 項目 | 内容 |
| --- | --- |
| 文書ID | OPS-RKSEO-001 |
| 作成日 | 2026-05-30 |
| 対象 | API/Worker/DB/Redis/Storage/Secret/外部API/通知の運用 |
| 関連文書 | requirements.md / basic_design.md / job_design.md / external_api_design.md / environment_setup.md / docker_deployment.md |

## 改訂履歴

| 版 | 日付 | 内容 | 作成/更新 |
| --- | --- | --- | --- |
| 1.0 | 2026-05-30 | 初版作成。日次確認、障害対応、クレジット、外部API、バックアップ復元を定義。 | ChatGPT |
| 1.1 | 2026-06-02 | MVP運用メトリクス、管理画面/API確認導線、Runbookスモークコマンドを追記。 | Codex |
| 1.2 | 2026-07-11 | Docker ComposeによるVPSデプロイ、更新、再起動、永続Volume運用を追記。 | Codex |
| 1.3 | 2026-07-12 | レビュー反映。VPS手順を`docker_deployment.md`へ一本化し、Compose overlay構成、`/readyz`の未適用Migration検知、`container-smoke.sh`を反映。 | Claude |

## 1. 目的

本書は、開発者本人が運用するための監視、確認、障害対応、復旧、メンテナンス手順を定義する。秘密情報の実値は本書に記載しない。

## 2. 定常確認

| 頻度 | 確認項目 | 確認先 |
| --- | --- | --- |
| 毎日 | ジョブ失敗、キュー滞留、402/403、429急増、クレジット消費量 | 管理画面、ログ、メトリクス |
| 毎週 | ラッコAPI仕様更新、マスタ同期、DBサイズ、外部API失敗率 | 管理画面、OpenAPI差分 |
| 毎月 | 月次レポート生成、クレジット使用量 | レポート画面、DB運用画面 |
| 四半期 | リストア手順、Secretローテーション、不要ローデータ削除 | Runbook、Storage、Key Vault |

### 2.1 MVP確認導線

| 確認項目 | 画面/API | コマンド例 |
| --- | --- | --- |
| ジョブ失敗/滞留 | 管理画面 S-900 ジョブ、`GET /api/jobs` | `Invoke-WebRequest -UseBasicParsing "http://localhost:5251/api/jobs?status=all&page=1&pageSize=25"` |
| 402/403/429/クレジット | 管理画面 S-900 クレジット、`GET /api/admin/external-api-calls` | `Invoke-WebRequest -UseBasicParsing "http://localhost:5251/api/admin/external-api-calls?page=1&pageSize=50"` |
| 監査ログ | 管理画面 S-900 監査ログ、`GET /api/admin/audit-logs` | `Invoke-WebRequest -UseBasicParsing "http://localhost:5251/api/admin/audit-logs?page=1&pageSize=50"` |
| 通知失敗 | 管理画面 S-900 Discord通知、`GET /api/admin/notification-deliveries` | `Invoke-WebRequest -UseBasicParsing "http://localhost:5251/api/admin/notification-deliveries?status=all&page=1&pageSize=50"` |
| CSV出力 | S-010/S-020/S-030 のCSV出力、`POST /api/projects/{projectId}/exports/csv` | `Invoke-WebRequest -UseBasicParsing -Method Post -ContentType "application/json" -Body '{"exportType":"external_api_calls","filter":{},"columns":["provider","endpoint","statusCode","consumedCredit","cacheHit","errorCode","createdAt"]}' "http://localhost:5251/api/projects/{projectId}/exports/csv"` |

## 3. 主要アラート

| アラート | 閾値 | 初動 |
| --- | --- | --- |
| ジョブ失敗率高 | 直近1時間で5%超 | 失敗ジョブ一覧、エラー分類、外部API状態確認。 |
| API 402 | 1件以上 | 契約側のクレジット残量、API契約、対象ジョブの消費量を確認。 |
| API 403 | 連続3件 | APIキー状態、Secret参照、契約スコープを確認。 |
| API 429急増 | 直近10分で通常比3倍 | Worker同時実行数、バックオフ、キュー滞留を確認。 |
| キュー滞留 | 30分以上増加 | Worker稼働、DB接続、外部API遅延を確認。 |
| DB接続エラー | 連続発生 | DB稼働、接続文字列、コネクション枯渇を確認。 |
| 通知失敗 | retrying/failed増加 | Discord Webhook、レート制限、Secret参照を確認。 |

## 4. MVPメトリクス

OpenTelemetry Meter名は `SeoIntelligence`。MVPで記録する運用メトリクスは以下を正本にする。

| メトリクス | 種別 | 確認内容 |
| --- | --- | --- |
| `job_success_rate` | ObservableGauge | 直近1時間の成功/失敗/キャンセル状態ジョブに対する成功率。 |
| `job_queue_depth` | ObservableGauge | `queued` と `waiting_external` の滞留ジョブ数。 |
| `job_duration_p95` | Histogram | ジョブ完了/失敗/キャンセルまでの処理時間。p95はOTelバックエンド側で算出する。 |
| `external_api_429_count` | Counter | ラッコキーワードAPI等の429発生回数。 |
| `external_api_402_count` | Counter | クレジット不足402発生回数。 |
| `external_api_credit_consumed` | Counter | 外部APIレスポンスの `consumedCredit` 合計。 |
| `notification_failure_count` | Counter | Discord通知のretrying/failed発生回数。 |
| `retry_count_by_job_type` | Counter | 自動/手動再試行回数。`job_type` と `source` で確認する。 |

## 5. 障害対応手順

### 5.1 ジョブ失敗

1. 管理画面または`GET /api/jobs`で失敗ジョブを確認する。
2. `job_type`、`status`、`error_json`、`correlation_id`を確認する。
3. `external_api_calls`に紐付くHTTP status、error_code、consumed_creditを確認する。
4. `failed_retryable`なら原因が一時的であることを確認して手動再実行する。
5. `failed_fatal`なら入力、APIキー、契約側クレジット、契約スコープを修正して新規ジョブを登録する。

### 5.2 クレジット不足 402

1. `external_api_calls`で402発生ジョブとAPIキーを確認する。
2. `external_api_calls`で実消費、対象エンドポイント、対象ジョブ、APIキーを確認する。
3. 契約側のクレジット残量を確認する。
4. 大量ジョブは分割数、対象キーワード、重複除外を見直す。
5. 再実行する場合はfailed_fatalの同一ジョブを直接再実行せず、新しい条件で登録する。

### 5.3 APIキー無効 403

1. `api_credentials.status`がactiveであることを確認する。
2. `key_ref`がSecret Store上の正しいSecret名を指していることを確認する。
3. Secretの有効期限、ローテーション履歴、参照権限を確認する。
4. 必要なら`POST /api/admin/api-credentials/{credentialId}/rotate`でローテーションする。
5. テスト通知または軽量APIで疎通確認する。

### 5.4 レート制限 429

1. 直近の`external_api_calls`で429のendpointと頻度を確認する。
2. Workerの`Hangfire__WorkerCount`または`external-api`キューの起動数を一時的に下げる。
3. キュー滞留が増える場合は優先度の低いジョブを停止または延期する。
4. 429が収束したら同時実行数を段階的に戻す。

### 5.5 Discord通知失敗

1. `notification_deliveries`のstatus、error_message、retry_countを確認する。
2. Webhook Secret参照が正しいことを確認する。
3. Discord側のWebhook削除、レート制限、権限変更を確認する。
4. 修正後、手動再送APIを実行する。

## 6. バックアップ・復元

| 対象 | 方針 |
| --- | --- |
| PostgreSQL | 日次フルバックアップ、WAL/PITR有効化。 |
| Storage | ローデータ、レポート、CSV/Excelを冗長化。 |
| Web Data Protection keys | `web-data-protection` Volumeを保持し、Web再作成時も継続利用する。 |
| Secret | Key Vault等でバージョン管理。実値はRunbookに書かない。 |
| 復元検証 | 四半期ごと、およびバックアップ手順を変更したときに`bash scripts/verify-production-restore.sh`を実行する。隔離Compose projectへ実際に復元し、成果物がAPI経由でバイト一致で読めること、復元先のWorkerが新しいジョブを完走できることまで確認する（`docs/docker_deployment.md` 5.2）。Linux専用で、実行できない環境では**exit 2**になる。この失敗を「環境の都合」として無視しないこと。無視した時点で、復元検証は実施されていない。 |

復元時は、DB、Storage、Secret参照、アプリ設定の整合性を確認する。ローデータ本体を保持期間で削除済みの場合でも、DB上のハッシュ、ステータス、クレジット、契約スコープは監査用に残す。

## 7. デプロイ・メンテナンス

| 作業 | 手順 |
| --- | --- |
| API仕様更新 | `rakko-keyword-api-docs.json`差分確認、DTO再生成、契約テスト、影響確認。 |
| API契約変更 | 管理画面/APIでは契約スコープを変更しない。SeedDataまたはマイグレーション相当の保守手順で旧`api_contract_scopes`をarchivedにし、新しい`scope_key`を追加する。 |
| DBマイグレーション | dry-run、Web/API/Worker停止、バックアップ確認、適用、新imageで再開、スモークテスト。 |
| Secretローテーション | 新Secret登録、credential rotate、疎通確認、旧Secret無効化。 |
| Worker設定変更 | 同時実行数、キュー、ポーリング間隔を変更し、ジョブ成功率を監視。 |
| 保持期間変更 | `workspaces.retention_settings_json`更新、削除対象確認、監査情報保持確認。 |

### 7.1 単一利用者向け暫定VPSの運用

同居VPS（`web-writing.cloud` と併設）では、デプロイと共通Caddyの操作は `wwt-seo-infra` の entrypoint を使う。`scripts/deploy-production.sh` や `docker compose` を直接呼ばない。

| やりたいこと | 入口 |
| --- | --- |
| 更新デプロイ | `/srv/wwt-seo-infra/scripts/seo update` |
| 単独バックアップ | `/srv/wwt-seo-infra/scripts/seo backup` |
| 共通Caddyの再起動・設定変更 | `/srv/wwt-seo-infra/scripts/caddy-up.sh`（`caddy reload` と `docker compose restart` は使えない） |
| 手順の正本 | `wwt-seo-infra/docs/vps-deploy.md` |

`scripts/seo` は `CADDY_NETWORK` をinfraの決定へ固定する。これを飛ばすと、Caddyとアプリが別ネットワークに居るまま**デプロイは成功し、全リクエストが502**になる。障害調査でこの症状を見たら、まずネットワーク名の一致を確認する。

VPSの初回デプロイ・更新・バックアップの正本手順は `docs/docker_deployment.md` とする（コマンド列は本書へ複製しない）。個人利用向け構成であり、PostgreSQL、Redis、APIのホストポートを公開せず、Web/APIだけを共通Caddyの専用external networkへ接続する。

運用上の注意（正本手順に加えて守ること）:

- `.env.production`と`.env.production.app`は`chmod 600`で権限を制限し、`POSTGRES_PASSWORD`、APIキー、Webhook URL等をGit、Dockerfile、build引数、ログへ含めない。外部APIキーとDiscord Webhookは`.env.production.app`にだけ置く。同ファイルはapiとworkerだけが読み、Webへは渡らない。
- Migration前にDB/Storageのバックアップを確認する。更新時はWeb/API/Worker停止中にバックアップとMigrationを行う（メンテナンス時間）。
- `RakkoKeywordV1120DataBackfill`を含む更新では、停止前に非終端ジョブを確認し、停止後は旧imageのAPI/Workerを再起動しない。Migrationは旧コード値を保持する非終端の検索ボリューム登録ジョブだけを`canceled`へ同期し、`audit_logs`へ`job.canceled`を記録する。`waiting_external`の外部requestId自体は取り消せず、消費済みクレジットは返却されない。
- 開発・CIでは`scripts/verify-rakko-v1120-migration.ps1`が一時DBへ合成データを投入し、対象限定、子request、業務status、監査ログ、既適用環境の補正と再登録可否を検証する。通常は`scripts/smoke-local.ps1`から自動実行される。
- ローカルで`scripts/smoke-local.ps1 -StopDependencies`を指定しても永続ボリュームは保持する。`-RemoveDependencyVolumes`は`-StopDependencies`との併用が必須で、PostgreSQL/Redisのデータを削除するため、使い捨てのCI環境以外では指定しない。
- API `/readyz`は未適用Migrationを検知してunhealthyを返す。apiが`unhealthy`のときは`migrate`の実行有無を最初に確認する。
- `restart worker`は同じimage/設定での再起動、`up -d --force-recreate worker`はCompose環境変数またはimage変更の反映に使う。
- 通常停止は`down`までとし、`down -v`は使用しない。`-v`はPostgreSQL、Redis、共有Storage、Data Protection keysを削除する。アプリimageを戻す場合も、適用済みDB schemaとの互換性を確認し、Migrationを安易に逆適用しない。

### 7.2 公開境界

アプリ内の単一管理者ログイン（ASP.NET Core Identity + Cookie）とAPIサービスキーで保護する。Caddyは`/api/report-shares/*`だけを`seo-api:8080`へ、それ以外は`seo-web:8080`へproxyし、他の`/api/*`は公開しない。Blazorの`/_blazor` WebSocketもCaddy経由とする。Caddy Basic認証、VPN、Cloudflare Access等の外部ゲートは多層防御として併用を推奨する。設定例は `docs/docker_deployment.md` の3.3節を正本とする。

他アプリと同一VPSへ同居させる場合は別サブドメインで公開する。認証Cookieが`__Host-`接頭辞を使うため、同一ホスト名でパス分割するとCookie名が衝突する。

`/readyz`は公開しない。匿名で到達でき、1リクエストごとにDBクエリ、Redis ping、Storageへの実ファイル書込/読込/削除、Secret Storeアクセスを行うため、無認証の負荷増幅点になる。未適用Migrationがある場合はMigration名も応答へ含む。`/healthz`は`self`チェックだけを返すため公開してよい。Readinessの内訳はVPS内部から`docker compose ... exec api curl http://localhost:8080/readyz`で確認する。

### 7.3 コンテナイメージ脆弱性の扱い

スキャンとゲートは `scripts/scan-container-images.sh` に集約し、CIから3モードで呼ぶ。修正版が存在する（`--ignore-unfixed`）HIGH/CRITICALだけを対象にする。

| モード | 対象 | 扱い |
| --- | --- | --- |
| `app` | `seo-intelligence-api` / `web` / `worker` / `migrate` | 自前でre-buildできるため、検出があればCIを失敗させる。 |
| `runtime` | `postgres:16-alpine` / `redis:7-alpine` | 本番で稼働するためゲート対象。下表の除外に該当しない検出があればCIを失敗させる。 |
| `dev` | `minio/minio` / `minio/mc` | 開発専用の任意profileで、本番Composeは起動しない。報告のみでゲートしない。 |

ローカル再確認:

```bash
bash scripts/scan-container-images.sh runtime
```

#### 受容の記録

受容はCVE単位で登録する。`<image>` `<CVE id>` `<Target>` `<PkgName>` の4項目すべてが一致した場合だけ除外され、コンポーネント単位の一括除外は行わない。一括除外にすると、同じバイナリに後から出た**到達可能な**脆弱性まで自動的に隠れるためである。

| 対象 | 受容コンポーネント | 件数 | 判断 | 記録日 |
| --- | --- | --- | --- | --- |
| `postgres:16-alpine` | `usr/local/bin/gosu` の `stdlib` | 22件（Critical 1 / High 21） | **受容**。`gosu`はentrypointが起動時にrootからpostgresへ権限降格するためだけに1回`exec`する補助バイナリで、ネットワーク通信を一切行わない。受容した22件はいずれもGo標準ライブラリのTLS/HTTP/暗号系であり、到達するコードパスが存在しない。CVE IDの一覧は `scripts/scan-container-images.sh` の `RUNTIME_ACCEPTED` を正本とする。 | 2026-08-22 |
| `postgres:16-alpine` | `libcrypto3` / `libssl3` | 2件（High、いずれも CVE-2026-14456） | **受容**。OpenSSLのQUICサーバー実装に限定される脆弱性である。イメージの `libssl3-3.5.7-r0` はQUICのエントリポイント（`SSL_set_quic_tls_cbs` / `SSL_set_quic_tls_early_data_enabled` / `SSL_set_quic_tls_transport_params`）を公開しているが、**イメージ内のバイナリと拡張のいずれもそれらを参照していない**（イメージ内で実測）。`postgres` は `libssl.so.3` / `libcrypto.so.3` をリンクするが、用途はTLSとSCRAM認証で、PostgreSQL 16 はQUICを話さない。加えて本構成では postgres をホストへ公開せず（両Composeに `ports:` が無い）、`backend` ネットワークからのみ到達する。 | 2026-09-07 |
| `postgres:16-alpine` | `libuuid` | 7件（High） | **受容**。util-linux のアドバイザリは同梱サブパッケージすべてに付くが、`libuuid` が提供するのは `usr/lib/libuuid.so.1` だけで、これをリンクするELFはイメージ全体で `/usr/local/lib/postgresql/uuid-ossp.so` の1つに限られる（イメージ内で実測）。本スキーマが作成する拡張は `pg_trgm` のみで（`SeoIntelligenceDbContext`）、`uuid-ossp` を作成しないため、この共有ライブラリは読み込まれない。 | 2026-09-07 |

受容を見直す条件:

- `gosu`の用途がentrypointの権限降格以外へ広がった場合。
- 上流イメージがパッチ済みGoで再ビルドされた場合（受容を解除する）。
- 新しいCVEが検出された場合。**自動的には除外されない**ため、CIが失敗して個別判断を促す。特に`os/exec`、ファイルシステム、引数処理など`gosu`から到達し得る領域の脆弱性は受容しない。
- `uuid-ossp` 拡張を作成した場合。`libuuid` の受容は「読み込まれるELFが無い」ことに依存しており、拡張を作った時点で前提が消える。
- postgres をホストへ公開した場合、またはPostgreSQLがQUICを使うようになった場合。
- OSパッケージの受容は `Target` にAlpineのバージョンを含む（`/scan/image.tar (alpine 3.24.1)`）。ベースイメージが上がると一致しなくなり、受容は**自動的に外れて**CIが失敗する。判断を持ち越さないための性質であり、意図した挙動である。

#### digestの固定

受容を判断したイメージのdigestは `image-digests.lock` を正本とする。同ファイルは `compose.yaml` が起動するイメージ、`scripts/scan-container-images.sh` が検査するイメージ、本節の受容判断の3者を一致させるための単一の定義であり、値を本書へ複製しない（複製すると更新漏れで食い違う）。

`scripts/verify-production-compose.sh` が、Composeの**レンダリング結果**を同ファイルと完全一致で照合する。ソースへのgrepではないため、コメント行に期待値があっても通らない。

更新手順:

1. `image-digests.lock` のdigestを新しい値へ変更する。
2. `bash scripts/scan-container-images.sh runtime` を実行し、新イメージの検出内容を確認する。
3. 本節の受容表を再判断し、`RUNTIME_ACCEPTED` を更新する。
4. `compose.yaml` のimage参照を新digestへ更新する。
5. `bash scripts/verify-production-compose.sh` で3者一致を確認する。

現在の値は次で確認する。

```bash
cat image-digests.lock
docker image inspect --format '{{index .RepoDigests 0}}' postgres:16-alpine
```

#### スキャナへDockerソケットを渡さない

各イメージは `docker save` でtarへ書き出し、`trivy image --input` で読ませる。Dockerソケットを渡すとスキャナのコンテナがDockerデーモンを操作でき、これはホストのroot相当の権限に等しい。開発PCとCI runnerを第三者イメージへ委ねないため、渡すのはtar 1ファイルだけにする。スキャナimageもタグではなくdigestで固定する。

## 8. スモークテスト

Runbookスモークは依存サービスReady、DB migration適用、API/Worker/Web起動、プロジェクト一覧、監査ログ検索、マスタ同期ジョブ完了、CSV出力ジョブ完了を確認する。Discordテスト通知はSecret参照が設定済みの場合だけ実行する。

### 8.1 ローカル/CIスモーク

`scripts/smoke-local.ps1`は開発用Composeとホストの.NET SDK/PowerShellを使う。Docker-only VPS上や、VPS用Composeが稼働中の状態では実行しない。コンテナ版スタックのスモークはCIと同一の `bash scripts/container-smoke.sh` を使う（隔離Compose projectで実行され、開発スタックへ影響しない）。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/smoke-local.ps1
```

既存プロジェクトでCSV出力を確認する場合:

```powershell
$env:SMOKE_PROJECT_ID = "<project-guid>"
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/smoke-local.ps1
```

Discordテスト通知まで含める場合:

```powershell
$env:SMOKE_DISCORD_CHANNEL_ID = "<notification-channel-guid>"
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/smoke-local.ps1
```

Playwrightで画面操作まで含める場合:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/smoke-local.ps1 -RunBrowserTests -InstallPlaywrightBrowsers
```

### 8.2 VPSスモーク

業務APIはCaddyから公開していないため、VPSスモークではヘルスエンドポイントとWebのサインイン導線を確認する。Caddy Basic認証を併用している場合は`curl --user admin`の対話入力を使い、passwordをコマンドへ直接書かない。

```bash
curl --fail --silent --show-error https://seo.example.com/healthz
curl --fail --silent --show-error https://seo.example.com/api-healthz
# /readyz は公開しない。無認証でDB/Redis/Storage/Secret Storeへ実アクセスするため、内部から確認する。
docker compose --project-name seo-intelligence-prod --env-file .env.production -f compose.yaml -f compose.production.yaml exec api curl --fail --silent http://localhost:8080/readyz
# 未サインインのページ要求はサインイン画面へリダイレクトされる。
curl --silent --output /dev/null --write-out '%{http_code}\n' https://seo.example.com/dashboard
curl --fail --silent --show-error https://seo.example.com/login > /dev/null
docker compose --project-name seo-intelligence-prod --env-file .env.production -f compose.yaml -f compose.production.yaml ps
docker compose --project-name seo-intelligence-prod --env-file .env.production -f compose.yaml -f compose.production.yaml logs --tail 200 web api worker
```

`/dashboard`が`302`を返し、`/login`が`200`を返すことを確認する。続けてブラウザで管理者アカウントによりサインインし、管理画面またはAPIから小さいMockジョブを1件登録して、Workerにより`succeeded`へ進むことを確認する。Real APIモードやDiscord通知のスモークはクレジット、契約スコープ、Secretを確認した場合だけ行う。

| 対象 | 確認 |
| --- | --- |
| API | `/healthz`、`/readyz`が成功する。 |
| DB | プロジェクト一覧、監査ログ検索が成功する。 |
| Worker | マスタ同期ジョブとCSV出力ジョブが`succeeded`になる。 |
| ラッコAPI | 地域/言語マスタまたは軽量APIが成功する。 |
| Discord | テスト通知が成功し履歴が残る。 |
| CSV出力 | 小規模データを出力でき、監査ログが残る。 |
| BrowserE2E | プロジェクト選択、キーワード探索、検索ボリューム登録、CSV出力ボタン操作が成功する。 |

## 9. インシデント記録

障害発生時は以下を残す。

| 項目 | 内容 |
| --- | --- |
| 発生日時 | Asia/TokyoとUTCを併記する。 |
| 影響範囲 | 画面、API、Worker、外部API、データ。 |
| correlation_id | 関連するID。 |
| job_id | 関連ジョブ。 |
| external_request_id | 外部API requestId。 |
| 原因 | 入力、外部API、クレジット、認証、DB、実装不具合など。 |
| 対応 | 実施した操作、再実行、設定変更。 |
| 再発防止 | テスト追加、監視追加、Runbook更新。 |
