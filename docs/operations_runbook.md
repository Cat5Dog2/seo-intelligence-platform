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
| 1.4 | 2026-09-13 | CDリリース候補通知（`release-candidate-notify.yaml`）の追加を反映。設定するVariable/Secret、GitHub App権限、有効化手順、通知失敗時の再実行方法を7.4に追記。 | Claude |
| 1.5 | 2026-09-14 | レビュー反映。送信成功（204）と受信側の反応は別であることを明記し、infra側の受信workflowがdefault branch上に必要な旨と、有効化時にinfra側の実行も確認する手順を7.4に追加。 | Claude |

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

スキャンとゲートは `scripts/scan-container-images.sh` に集約し、CIから次のモードで呼ぶ。ゲートは修正版が存在する（`--ignore-unfixed`）HIGH/CRITICALだけを対象にする。

| モード | 対象 | 扱い |
| --- | --- | --- |
| `app` | `seo-intelligence-api` / `web` / `worker` / `migrate` | 自前でre-buildできるため、検出があればCIを失敗させる。 |
| `runtime` | `postgres:16-alpine` / `redis:7-alpine` を `image-digests.lock` のdigestで取得 | 本番で稼働しているイメージそのものを検査するゲート。下表の除外に該当しない検出、またはどの検出にも一致しなくなった受容があればCIを失敗させる。 |
| `unfixed` | 同上 | 修正版の無いCVEも含めて一覧する。報告のみでゲートしない。 |
| `dev` | `rustfs/rustfs:1.0.0-rc.6` | 開発専用の任意profileで、本番Composeは起動しない。報告のみでゲートしない。 |
| `drift` | `postgres:16-alpine` / `redis:7-alpine` のタグ | 上流タグが指すindexと、その中の当該プラットフォーム（linux/amd64）imageを `image-digests.lock` のdigestと比較する。indexだけが動いてimageが同一ならexit 0で報告のみ、imageが変わっていればexit 2。CIはexit 2をwarning annotationとstep summaryに出すだけでゲートしない。レジストリに直接問い合わせ、pullもスキャンもしない。 |

MinIO CommunityのEOLと公式Docker Hubリポジトリ消失を受け、開発用S3互換環境はRustFSへ移行した。RustFSはまだ安定版前のため、レビュー済みの`1.0.0-rc.6`をmanifest digest `sha256:97171b3d72cd47dc81000f92ea84de25608bfc35a94c965501afaeb5d99f6035`で固定する。これは開発・接続確認専用であり、本番ストレージには使用しない。参照を更新する場合はrelease notesと既知のセキュリティ問題を確認し、`compose.override.yaml`と`scripts/scan-container-images.sh`を同時に変更して、`bash scripts/verify-development-image-pins.sh`で一致と固定形式を検証する。

RustFSは非root UID/GID `10001:10001`で実行する。`rustfs-volume-init`はnamed volumeの所有権設定だけを行い、`rustfs-init`は同じ固定image内のSigV4対応curlで`seo-intelligence` bucketを冪等作成する。旧`minio-data` volumeは自動削除も再利用もしない。必要な開発データがある場合は、旧環境を保持したままS3 API経由で手動移行し、確認が終わるまでvolumeを削除しない。

ローカル再確認:

```bash
bash scripts/scan-container-images.sh runtime
```

#### 受容の記録

受容はCVE単位で登録する。`<image>` `<CVE id>` `<Target>` `<PkgName>` の4項目すべてが一致した場合だけ除外され、コンポーネント単位の一括除外は行わない。一括除外にすると、同じバイナリに後から出た**到達可能な**脆弱性まで自動的に隠れるためである。

| 対象 | 受容コンポーネント | 件数 | 判断 | 記録日 |
| --- | --- | --- | --- | --- |
| `postgres:16-alpine` | `usr/local/bin/gosu` の `stdlib` | 22件（Critical 1 / High 21） | **受容**。`gosu`はentrypointが起動時にrootからpostgresへ権限降格するためだけに1回`exec`する補助バイナリで、ネットワーク通信を一切行わない。受容した22件はいずれもGo標準ライブラリのTLS/HTTP/暗号系であり、到達するコードパスが存在しない。CVE IDの一覧は `scripts/scan-container-images.sh` の `RUNTIME_ACCEPTED` を正本とする。 | 2026-08-22 |

2026-09-19: `postgres:16-alpine` のdigestが更新され、Alpine 3.24.1 → 3.24.2相当のパッケージ更新を含んでいたため、以下2項目(計9件)は受容判断ごと不要になった。

- `libcrypto3` / `libssl3`（CVE-2026-14456、旧`libssl3-3.5.7-r0`）: 新イメージでは`libssl3-3.5.8-r0`へ更新され、修正済みのため検出されなくなった。
- `libuuid`（旧7件）: 新イメージでは`libuuid-2.42.3-r1`へ更新され、修正済みのため検出されなくなった。

`RUNTIME_ACCEPTED` から該当9件を削除済み。`gosu`/`stdlib` の22件は対象バイナリ(gosu本体)が変わっていないため据え置き。

受容を見直す条件:

- `gosu`の用途がentrypointの権限降格以外へ広がった場合。
- 上流イメージがパッチ済みGoで再ビルドされ、そのdigestへ更新した場合（受容を解除する）。どの検出にも一致しなくなった受容は `runtime` が失敗として列挙するので、残したままにはできない。
- 新しいCVEが検出された場合。**自動的には除外されない**ため、CIが失敗して個別判断を促す。特に`os/exec`、ファイルシステム、引数処理など`gosu`から到達し得る領域の脆弱性は受容しない。
- `uuid-ossp` 拡張を作成した場合。`libuuid` の受容は「読み込まれるELFが無い」ことに依存しており、拡張を作った時点で前提が消える。
- postgres をホストへ公開した場合、またはPostgreSQLがQUICを使うようになった場合。
- OSパッケージの受容は `Target` にAlpineのバージョンを含む（`/scan/image.tar (alpine 3.24.1)`）。digest更新でベースイメージが上がると一致しなくなり、受容は失敗として列挙され、検出が残っていれば未受容として改めてゲートされる。判断を持ち越さないための性質であり、意図した挙動である。

#### digestの固定

受容を判断したイメージのdigestは `image-digests.lock` を正本とする。同ファイルは `compose.yaml` が起動するイメージ、`scripts/scan-container-images.sh` が検査するイメージ、本節の受容判断の3者を一致させるための単一の定義であり、値を本書へ複製しない（複製すると更新漏れで食い違う）。

`runtime` / `unfixed` モードはタグではなく同ファイルのdigestを `pull` して検査する。したがってゲートが答える問いは「本番で動いているイメージに、未判断の修正可能なHIGH/CRITICALがあるか」だけである。上流が受容済みCVEの修正版を公開すれば脆弱性DBに修正バージョンが載り、タグを見張らなくてもこのスキャンが失敗して更新を促す。

上流タグが動いたこと自体は失敗にしない。lockが固定しているのはマルチプラットフォームのindexのdigestで、他プラットフォームやattestationが再ビルドされるだけで変わる。Alpine系の公式イメージは数日おきに再ビルドされ、その大半はこのスタックが動かすlinux/amd64のimageを1バイトも変えない（2026-09-21の再ビルドはpostgres/redisともlinux/amd64のmanifest digestが旧indexと同一だった）。以前はこれで `container-scan` が失敗し、required checkのため無関係な全PRのマージとリリース候補通知が止まっていた。タグの移動は `drift` モードが報告し、当該プラットフォームのimageが実際に変わった場合だけnightlyのwarning annotationに出る。

`scripts/verify-production-compose.sh` が、Composeの**レンダリング結果**を同ファイルと完全一致で照合する。ソースへのgrepではないため、コメント行に期待値があっても通らない。`scripts/verify-runtime-scan.sh` が、`runtime` / `unfixed` がlockのdigestだけをpullすること、タグの移動では失敗しないこと、一致しなくなった受容で失敗すること、`drift` がレジストリに問い合わせてindexの移動とimageの変更を区別することを fake docker で固定する。

更新手順:

1. `bash scripts/scan-container-images.sh drift` で、上流タグが現在指すdigestと、当該プラットフォームのimageが変わったかを確認する。imageが同一（"identical"）なら更新は不要で、以降の手順は省略してよい。
2. `image-digests.lock` のdigestを新しい値へ変更する。
3. `bash scripts/scan-container-images.sh runtime` を実行し、新イメージの検出内容を確認する。どの検出にも一致しなくなった受容は失敗として列挙されるので、`RUNTIME_ACCEPTED` と本節の受容表から削除する。
4. 新たに検出されたものを本節の受容表で判断し、`RUNTIME_ACCEPTED` を更新する。
5. `compose.yaml` のimage参照を新digestへ更新する。
6. `bash scripts/verify-production-compose.sh` で3者一致を確認する。

現在の値は次で確認する。

```bash
cat image-digests.lock
bash scripts/scan-container-images.sh drift
```

#### スキャナへDockerソケットを渡さない

各イメージは `docker save` でtarへ書き出し、`trivy image --input` で読ませる。Dockerソケットを渡すとスキャナのコンテナがDockerデーモンを操作でき、これはホストのroot相当の権限に等しい。開発PCとCI runnerを第三者イメージへ委ねないため、渡すのはtar 1ファイルだけにする。スキャナimageもタグではなくdigestで固定する。

### 7.4 CDリリース候補通知（release-candidate-notify）

`main`への push で `CI` workflow（`build-test-smoke` と `container-scan` の両方を含むCI全体）が成功した場合に、`.github/workflows/release-candidate-notify.yaml` が `wwt-seo-infra`（Repository Variable `INFRA_REPOSITORY` で指定する `owner/repository`）へ `repository_dispatch`（`event_type: app-release-candidate-v1`）を送る。採用SHAの決定、検証、本番デプロイはinfra側の責務であり、このworkflowは通知だけを行う。**通知が成功したことは、本番へデプロイされたことを意味しない。**

送信する `client_payload`（3リポジトリ共通の契約。フィールド名・型・`component`の値はこのリポジトリ単独では変更しない）:

| フィールド | 型 | 内容 |
| --- | --- | --- |
| `component` | string | 固定値 `"seo"`。受信側はこの値で送信元リポジトリを識別する。 |
| `source_sha` | string | 元CIが検証した40桁の完全なコミットSHA（`workflow_run.head_sha`）。通知workflow自身の `github.sha` は使わない。 |
| `source_run_id` | string | 元CIのrun ID（10進数文字列）。 |
| `source_run_attempt` | number | 元CIの実際の試行番号（整数）。 |

#### 通知対象になる条件

次をすべて満たすCI runの完了だけが通知対象になる（`.github/workflows/release-candidate-notify.yaml` の `jobs.notify.if` が判定する）。

- 元CIの `conclusion` が `success`（`workflow_run` イベントはworkflow全体の完了時にのみ発火するため、`build-test-smoke` だけの成功で `container-scan` の結果を待たずに先行通知することはできない）。
- 元CIの `event` が `push`（pull_request、schedule、workflow_dispatch起点のCI実行は対象外）。
- 元CIの `head_branch` が `main`。
- 元CIの `head_repository` がこのリポジトリ自身（forkからのCI実行を除外する。GitHub公式の `workflow_run` セキュリティガイダンスに沿った確認）。

#### 設定するVariable / Secret（このリポジトリ側）

| 種別 | 名前 | 内容 |
| --- | --- | --- |
| Variable | `CD_ENABLED` | 文字列 `"true"` のときだけ通知を有効化する。未設定または `"true"` 以外なら、Secret取得や外部送信より前にスキップする。 |
| Variable | `INFRA_REPOSITORY` | 送信先。`owner/repository` 形式（例: `<org>/wwt-seo-infra`）。 |
| Variable | `CD_APP_ID` | 通知に使うGitHub AppのApp ID（数値）。Appの設定画面の「App ID」欄の値をそのまま使う。 |
| Secret | `CD_APP_PRIVATE_KEY` | 同AppがGenerateした秘密鍵（.pem）の内容全体。 |

設定はこのリポジトリの Settings → Secrets and variables → Actions で行う。値そのものは本書に記載しない。

#### infra側の準備（GitHub App + 受信workflow。いずれも必須）

このworkflowは `CD_APP_ID` / `CD_APP_PRIVATE_KEY` でGitHub Appとして認証し、`INFRA_REPOSITORY` だけに限定した短命installation tokenを発行して `repository_dispatch` を送る（`actions/create-github-app-token`）。**送信が成功（HTTP 204）しても、それだけでは何も起きない。** 次の2点がinfra側で揃って初めて、通知が候補の検証や採用SHA更新PRにつながる。

1. GitHub App（インストール先はこのリポジトリではなく `wwt-seo-infra` 側）
   1. GitHub Appを作成する（3リポジトリ共通の通知用Appを1つ用意し共用してもよいし、リポジトリごとに分けてもよい）。
   2. Appのrepository permissionsに **Contents: Read and write** を設定する（`repository_dispatch` の送信に必要な最小権限。他の権限は付与しない）。
   3. Appを `wwt-seo-infra` リポジトリにインストールする（"Only select repositories" → `wwt-seo-infra` のみ）。このリポジトリ（`seo-intelligence-platform`）へインストールする必要はない。
   4. AppのApp IDと秘密鍵をこのリポジトリの `CD_APP_ID` / `CD_APP_PRIVATE_KEY` として設定する（次節）。
2. 受信workflow（`wwt-seo-infra` 側の実装・配置が必要。このリポジトリの変更だけでは届かない）
   - `wwt-seo-infra` の**default branch上に**、`repository_dispatch`（`types: [app-release-candidate-v1]` を推奨）を受け取るworkflowが存在し、有効化されていること。
   - GitHubの`repository_dispatch`は「workflowファイルがdefault branch上に存在する場合にのみworkflow runを起動する」（[GitHub公式ドキュメント](https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows#repository_dispatch)）。受信workflowが無い、またはdefault branch上に無い状態でも、GitHub APIへの送信自体（`POST /repos/{owner}/{repo}/dispatches`）はHTTP 204で成功する。**このリポジトリ側の送信成功は、infra側が反応したことを何も保証しない。**

#### 有効化手順

1. 上記のinfra側の準備（GitHub Appのインストール、および受信workflowのdefault branchへの配置・有効化の両方）を完了する。
2. このリポジトリに `INFRA_REPOSITORY`、`CD_APP_ID`、`CD_APP_PRIVATE_KEY` を設定する。
3. `CD_ENABLED` を `"true"` に設定する。
4. `main` へpushしてCIが成功した後、次の**両方**を確認する。送信側jobの成功だけでは有効化完了とみなさない。
   - このリポジトリのActionsタブで `Release Candidate Notify` の `Notify infra of release candidate` ジョブが成功していること。
   - `wwt-seo-infra` 側のActionsタブに、この`repository_dispatch`（`source_sha`が一致するもの）に対応するworkflow runが実際に記録され、想定どおり動作していること。

`CD_ENABLED` を設定しない、または `"true"` 以外にしている間は、Secretの取得も外部送信も発生しない（`scripts/verify-release-candidate-notify.sh` で回帰確認している）。

#### 通知失敗時の対応・再実行

- 通知の送信失敗（GitHub APIのHTTPエラーを含む）はworkflowの失敗として記録され、成功として扱われない。
- 再実行は、Actionsタブでこの `Release Candidate Notify` のrunを「Re-run failed jobs」する。`workflow_run` イベントのペイロード（元CIの `head_sha` / `id` / `run_attempt`）は元CI実行時点の値のまま保持されるため、再実行しても元CIを再実行する必要はなく、同じ内容で再送される。
- 主な失敗原因と確認先:

| 症状 | 確認 |
| --- | --- |
| `Mint GitHub App installation token` で失敗 | `CD_APP_ID` の値、`CD_APP_PRIVATE_KEY` が現在有効な秘密鍵か、Appが `INFRA_REPOSITORY` にインストール済みで `Contents` 権限を持つか。 |
| `Send release candidate notification` で404 | `INFRA_REPOSITORY` の値（`owner/repository` の綴り）。 |
| `Send release candidate notification` で403 | Appのpermissionが `Contents: write` を含むか、インストール範囲が対象リポジトリを含むか。 |
| `Send release candidate notification` は成功（204）するのに `wwt-seo-infra` 側で何も起きない | 送信は「GitHubがAPI呼び出しを受け付けた」ことしか示さない。infra側の受信workflowが**default branch上に**存在し有効化されているか、`types:` フィルタが `app-release-candidate-v1` を含むかを確認する（前節）。 |
| workflowが起動しない、または `skipping` ログのまま終わる | `CD_ENABLED` が `"true"` か、元CIの `event` / `head_branch` / `head_repository` / `conclusion` が対象条件を満たすか。 |

再送してもinfra側で同一の `source_sha` / `source_run_id` / `source_run_attempt` が重複登録されないようにする責務は、受信側（`wwt-seo-infra`）にある。

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
