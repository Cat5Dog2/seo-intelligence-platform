# API設計書

**ラッコキーワードAPIを中核にしたSEOインテリジェンス基盤**

_SEO Intelligence Platform / SEOインテリジェンス基盤_

| 項目 | 内容 |
| --- | --- |
| 文書ID | API-RKSEO-001 |
| 作成日 | 2026-05-30 |
| 対象システム | SEO・コンテンツ・競合分析・順位監視プラットフォーム |
| 前提技術 | ASP.NET Core Web API / Minimal APIs / OpenAPI / Hangfire / PostgreSQL |
| 関連文書 | requirements.md / basic_design.md / db_design.md |
| 外部仕様 | rakko-keyword-api-docs.json（OpenAPI 3.0、ラッコキーワードAPI v1.4.1） |

## 改訂履歴

| 版 | 日付 | 内容 | 作成/更新 |
| --- | --- | --- | --- |
| 1.0 | 2026-05-30 | 初版作成。内部API、共通仕様、外部API連携、主要DTO制約を定義。 | ChatGPT |

## 1. 目的

本書は、SEOインテリジェンス基盤の内部REST APIと外部API連携境界を定義する。画面、Worker、外部コネクタが同じ契約で動作できるように、URL設計、レスポンス形式、非同期ジョブ、エラー、監査、入力制約を明確化する。

## 2. API設計方針

| 方針 | 内容 |
| --- | --- |
| REST中心 | 画面/BFFからの操作はHTTP APIとして公開し、重い処理はジョブAPIへ分離する。 |
| プロジェクトスコープ | 業務データは`/api/projects/{projectId}/...`を正本とし、body内の`projectId`は受け付けない。 |
| 単一利用者 | 初期版の操作主体は固定の`developer`とし、監査ログ、ジョブ、外部API呼び出しへ保存する。 |
| 非同期優先 | 外部APIを複数回呼ぶ処理、ファイル出力、AI生成、レポート生成は`202 Accepted` + `jobId`で返す。 |
| 秘密情報非返却 | APIキー、Webhook URL、AIキー、OAuthトークンの実値はAPIレスポンスに返さず、参照名やマスク値のみ返す。 |
| ソフト削除 | DELETE系APIは物理削除せず、`archived`または`disabled`へ状態変更する。 |
| 監査可能性 | `correlationId`、`jobId`、`externalRequestId`、`consumedCredit`をログ、DB、レスポンスmetaで追跡する。 |

## 3. 共通仕様

### 3.1 ベースURL

| 種別 | URL |
| --- | --- |
| 内部API | `/api` |
| レポート共有API | `/api/report-shares/{token}` |
| OpenAPI | `/openapi/v1.json` |
| Health Check | `/healthz`、`/readyz` |

### 3.2 HTTPヘッダー

| ヘッダー | 必須 | 内容 |
| --- | --- | --- |
| `Content-Type: application/json` | POST/PUTで必須 | JSONリクエストを表す。 |
| `Accept: application/json` | 推奨 | JSONレスポンスを要求する。 |
| `X-Correlation-Id` | 任意 | 未指定時はサーバーで生成する。 |
| `Idempotency-Key` | 任意 | ジョブ登録、出力、インポートなど重複実行を避けたいPOSTで使用する。サーバーは`jobs.idempotency_key`と`request_hash`で同一スコープの重複登録を抑止する。 |

### 3.3 レスポンスエンベロープ

```json
{
  "requestId": "internal-correlation-id",
  "result": true,
  "data": {},
  "errors": [],
  "meta": {
    "jobId": null,
    "externalRequestId": null,
    "consumedCredit": 0,
    "page": null
  }
}
```

### 3.4 エラー形式

```json
{
  "requestId": "internal-correlation-id",
  "result": false,
  "data": null,
  "errors": [
    {
      "code": "Validation.Keyword.Required",
      "message": "keyword is required.",
      "target": "keyword"
    }
  ],
  "meta": {}
}
```

| HTTP | 用途 | 処理 |
| --- | --- | --- |
| 200 | 同期取得/更新成功 | `result=true`で返す。 |
| 201 | リソース作成成功 | 作成リソースを返す。 |
| 202 | 非同期ジョブ登録 | `jobId`とジョブ状態取得URLを返す。 |
| 400 | 入力エラー | 入力修正可能なバリデーションエラー。 |
| 401 | 未認証 | 外部公開時の単一管理者ログインで使用する。 |
| 403 | 認可/スコープ不一致 | プロジェクト外リソース参照、無効化済み設定など。 |
| 404 | リソースなし | 存在しない、または参照できないリソース。 |
| 409 | 競合 | 重複名、idempotency keyの不一致、状態遷移競合。 |
| 429 | 内部レート制御 | API全体の保護。外部APIの429はジョブ内で再試行する。 |
| 500 | 内部エラー | 予期しない障害。 |
| 503 | 外部API一時障害 | 同期APIで外部依存が復旧待ちの場合。 |

## 4. ページング・検索・ソート

一覧APIは以下のクエリを共通で受け付ける。

| パラメータ | 既定値 | 内容 |
| --- | --- | --- |
| `page` | 1 | 1始まりのページ番号。 |
| `pageSize` | 50 | 1から200。大容量データはエクスポートAPIを使う。 |
| `status` | active | `active`、`archived`、`disabled`、`all`など対象に応じて定義する。 |
| `sortBy` | createdAt | APIごとに許可リストを持つ。 |
| `orderBy` | desc | `asc`または`desc`。 |
| `q` | なし | キーワード、URL、ドメイン、名称などの部分一致。 |

## 5. 認証・認可

初期版は開発者本人のみが利用する前提とし、ローカル実行ではOS/開発環境の保護に委ねる。本番相当環境へ公開する場合は、単一管理者ログイン + Cookie/BFF構成を必須にする。

プロジェクト配下APIは、URL上の`projectId`と対象リソースの`project_id`一致をDBで検証する。不一致はデータ存在を隠す目的で原則404、監査上明示したい管理APIでは403を返す。

## 6. 非同期ジョブAPI

### 6.1 ジョブ状態

| status | 意味 | 再実行 |
| --- | --- | --- |
| queued | 実行待ち | 可 |
| running | 実行中 | 不可 |
| waiting_external | 外部requestIdの完了待ち | 不可 |
| succeeded | 成功 | 手動再実行可 |
| failed_retryable | 再試行可能な失敗 | 可 |
| failed_fatal | 入力、認証、クレジット不足など致命的失敗 | 不可 |
| canceled | 実行前または待機中にキャンセル | 不可 |

`POST /api/jobs/{jobId}/cancel`は`queued`と`waiting_external`で許可する。`waiting_external`のキャンセルは外部API側のrequestIdを取り消すものではなく、以後のポーリング/結果取得を停止し、外部側で後から完了した結果は業務結果へ取り込まない。外部API呼び出しの記録は`external_api_calls`と`audit_logs`に残す。

### 6.2 ジョブレスポンス

```json
{
  "requestId": "internal-correlation-id",
  "result": true,
  "data": {
    "jobId": "018fd8a8-0000-7000-9000-000000000001",
    "jobType": "SearchVolumeJob",
    "status": "running",
    "progress": 45,
    "statusUrl": "/api/jobs/018fd8a8-0000-7000-9000-000000000001",
    "externalRequestId": null,
    "resultResource": null,
    "retryCount": 1,
    "nextRunAt": "2026-05-30T03:00:00Z",
    "error": null
  },
  "errors": [],
  "meta": {
    "jobId": "018fd8a8-0000-7000-9000-000000000001",
    "externalRequestId": null,
    "consumedCredit": 0,
    "page": null
  }
}
```

ジョブ登録APIの202 Accepted、`GET /api/jobs/{jobId}`、個別ジョブ状態取得APIはいずれも共通レスポンスエンベロープで`data`にジョブレスポンスを格納する。`resultResource`は完了後に`resourceType`、`resourceId`、`downloadUrl`、`expiresAt`などを持つ。ダウンロードURLの発行、失効、ダウンロードは`audit_logs`に記録する。

## 7. 内部API一覧

### 7.1 管理API

| Phase | Method | Path | 概要 |
| --- | --- | --- | --- |
| MVP | GET | `/api/admin/workspace` | 単一ワークスペース設定取得 |
| MVP | PUT | `/api/admin/workspace` | ワークスペース設定更新 |
| MVP | GET | `/api/admin/api-credentials` | API認証情報一覧。キー値は返却しない |
| MVP | POST | `/api/admin/api-credentials` | API認証情報作成。`secretValue`指定時はSecret Storeへ保存し、`keyRef`指定時は既存Secret参照としてDBは`key_ref`のみ保持 |
| MVP | GET | `/api/admin/api-credentials/{credentialId}` | API認証情報詳細取得 |
| MVP | PUT | `/api/admin/api-credentials/{credentialId}` | API認証情報更新。provider等のメタ情報を更新し、秘密値変更はrotateを使用 |
| MVP | DELETE | `/api/admin/api-credentials/{credentialId}` | `status=disabled`へ無効化 |
| MVP | POST | `/api/admin/api-credentials/{credentialId}/enable` | `status=active`へ再有効化 |
| MVP | POST | `/api/admin/api-credentials/{credentialId}/rotate` | `newSecretValue`または`newKeyRef`を受け取りキー参照をローテーション |
| MVP | GET | `/api/admin/notification-channels` | Discord通知設定一覧 |
| MVP | POST | `/api/admin/notification-channels` | Discord Webhook通知設定作成 |
| MVP | GET | `/api/admin/notification-channels/{channelId}` | 通知設定詳細取得 |
| MVP | PUT | `/api/admin/notification-channels/{channelId}` | 通知設定更新 |
| MVP | DELETE | `/api/admin/notification-channels/{channelId}` | `status=disabled`へ無効化 |
| MVP | POST | `/api/admin/notification-channels/{channelId}/enable` | `status=active`へ再有効化 |
| MVP | POST | `/api/admin/notification-channels/{channelId}/test` | テスト通知送信 |
| MVP | GET | `/api/admin/notification-deliveries` | 通知送信履歴一覧 |
| MVP | GET | `/api/admin/notification-deliveries/{deliveryId}` | 通知送信履歴詳細 |
| MVP | POST | `/api/admin/notification-deliveries/{deliveryId}/retry` | 通知の手動再送 |
| MVP | POST | `/api/admin/master-data/sync` | 地域/言語マスタ同期ジョブ登録 |
| MVP | GET | `/api/admin/external-api-calls` | API呼び出し・クレジット監査 |
| MVP | GET | `/api/admin/audit-logs` | 監査ログ一覧 |
| MVP | GET | `/api/admin/audit-logs/{auditLogId}` | 監査ログ詳細 |

`api_contract_scopes`は管理画面/APIでは管理しない。初期データとして登録し、契約内容が変わる場合は運用手順またはマイグレーション/SeedDataで既存行を`archived`にし、新しい`scope_key`を追加する。

API認証情報の作成/ローテーションでは、`secretValue`系と`keyRef`系を同時指定不可とする。`secretValue`を受け取る場合はAPIサーバーがSecret Storeへ登録し、生成または指定された参照名だけを`api_credentials.key_ref`へ保存する。`keyRef`を受け取る場合は既存Secret参照として扱い、APIサーバーは秘密値本体を受け取らない。レスポンス、ログ、監査ログには秘密値を出さず、マスク値または`key_ref`のみ返す。

監査ログ一覧は、共通の`page`、`pageSize`、`sortBy`、`orderBy`、`q`に加えて、`actor`、`resourceType`、`resourceId`、`correlationId`（または`correlation_id`）、`from`、`to`で検索できる。`from`と`to`はISO 8601日時として受け取り、`audit_logs.created_at`をUTC比較する。

### 7.2 プロジェクト・サイトAPI

| Phase | Method | Path | 概要 |
| --- | --- | --- | --- |
| MVP | GET | `/api/projects` | プロジェクト一覧 |
| MVP | POST | `/api/projects` | プロジェクト作成 |
| MVP | GET | `/api/projects/{projectId}` | プロジェクト詳細取得 |
| MVP | PUT | `/api/projects/{projectId}` | プロジェクト更新 |
| MVP | DELETE | `/api/projects/{projectId}` | `status=archived`へアーカイブ |
| MVP | POST | `/api/projects/{projectId}/restore` | `status=active`へ復元 |
| MVP | GET | `/api/projects/{projectId}/sites` | サイト一覧 |
| MVP | POST | `/api/projects/{projectId}/sites` | サイト作成 |
| MVP | GET | `/api/projects/{projectId}/sites/{siteId}` | サイト詳細取得 |
| MVP | PUT | `/api/projects/{projectId}/sites/{siteId}` | サイト更新 |
| MVP | DELETE | `/api/projects/{projectId}/sites/{siteId}` | `status=archived`へアーカイブ |
| MVP | POST | `/api/projects/{projectId}/sites/{siteId}/restore` | `status=active`へ復元 |
| MVP | GET | `/api/projects/{projectId}/dashboard` | 段階拡張ダッシュボード指標取得。Phase 1はキーワード探索、一括検索ボリューム、機会スコア、APIクレジット。Phase 2は競合、コンテンツ、記事ブリーフ、順位、アラート指標を追加 |
| MVP | GET | `/api/master-data/locations` | 地域マスタ一覧 |
| MVP | GET | `/api/master-data/languages` | 言語マスタ一覧 |

### 7.3 ジョブ管理API

| Phase | Method | Path | 概要 |
| --- | --- | --- | --- |
| MVP | GET | `/api/jobs` | 管理/監査向けジョブ一覧。status、job_type、project_id、期間で検索 |
| MVP | GET | `/api/jobs/{jobId}` | 管理/監査向けジョブ状態取得。プロジェクト画面ではproject-scopedな個別ジョブAPIを優先する |
| MVP | POST | `/api/jobs/{jobId}/cancel` | 実行前または待機中ジョブのキャンセル |
| MVP | POST | `/api/jobs/{jobId}/retry` | 再試行可能な失敗ジョブの手動再実行 |

### 7.4 キーワード・検索ボリュームAPI

| Phase | Method | Path | 概要 |
| --- | --- | --- | --- |
| MVP | POST | `/api/projects/{projectId}/keyword-discovery/suggest` | サジェスト/関連語/LSI/PAA/FAQ統合調査 |
| MVP | POST | `/api/projects/{projectId}/search-volume/jobs` | 一括検索ボリューム調査ジョブ登録 |
| MVP | GET | `/api/projects/{projectId}/search-volume/jobs/{jobId}` | 検索ボリュームジョブ状態取得 |
| MVP | GET | `/api/projects/{projectId}/search-volume/jobs/{jobId}/results` | 検索ボリューム結果取得 |
| MVP | POST | `/api/projects/{projectId}/exports/csv` | Phase 1対象データのCSVエクスポートジョブ登録 |
| MVP | GET | `/api/projects/{projectId}/exports/{exportId}` | エクスポート状態/ファイル情報取得。MVPはCSVのみ |
| MVP | GET | `/api/projects/{projectId}/exports/{exportId}/download` | CSVファイルダウンロード。発行/ダウンロードを監査する |

`/api/projects/{projectId}/keyword-discovery/suggest` は、`syncPreferred=true` かつ `limit<=50` を軽量条件として同期実行し、同期条件外では `202 Accepted`、`jobId`、`statusUrl` を返す。

### 7.5 SEO分析API

| Phase | Method | Path | 概要 |
| --- | --- | --- | --- |
| Phase 2 | GET | `/api/projects/{projectId}/clusters` | トピッククラスター一覧取得 |
| Phase 2 | GET | `/api/projects/{projectId}/clusters/{clusterId}` | トピッククラスター詳細取得 |
| Phase 2 | POST | `/api/projects/{projectId}/clusters/generate` | トピッククラスター生成 |
| Phase 2 | GET | `/api/projects/{projectId}/competitors` | 競合分析結果一覧取得 |
| Phase 2 | POST | `/api/projects/{projectId}/competitors/analyze` | 競合抽出・獲得語/ページ取得ジョブ登録 |
| Phase 2 | GET | `/api/projects/{projectId}/influx-keywords` | 獲得キーワード結果一覧取得。target、keyword、rank等で検索 |
| Phase 2 | GET | `/api/projects/{projectId}/influx-pages` | 獲得ページ結果一覧取得。target、URL、traffic_value等で検索 |
| Phase 2 | GET | `/api/projects/{projectId}/content-analyses` | コンテンツ分析結果一覧取得 |
| Phase 2 | POST | `/api/projects/{projectId}/content/analyze` | コンテンツ検索・見出し・共起語分析ジョブ登録 |
| Phase 2 | GET | `/api/projects/{projectId}/briefs` | 記事ブリーフ一覧取得 |
| Phase 2 | POST | `/api/projects/{projectId}/briefs/generate` | 記事ブリーフ生成 |
| Phase 2 | GET | `/api/projects/{projectId}/briefs/{briefId}` | 記事ブリーフ詳細取得 |
| Phase 2 | PUT | `/api/projects/{projectId}/briefs/{briefId}` | 記事ブリーフ本文、レビュー状態、ステータス更新 |
| Phase 2 | GET | `/api/projects/{projectId}/briefs/{briefId}/versions` | 記事ブリーフ版履歴取得 |
| Phase 2 | POST | `/api/projects/{projectId}/briefs/{briefId}/export` | 記事ブリーフのMarkdown/CSV出力ジョブ登録 |

`/api/projects/{projectId}/clusters` は `score` 降順を既定とし、`name`、`representativeKeyword`、`score`、`keywordCount`、`childCount`、`createdAt`、`updatedAt` でソートできる。レスポンスはクラスタID、親クラスタID、代表語、keyword数、機会スコア、検索意図、子クラスタ数、記事候補、内部リンク候補を返す。詳細APIは所属keywordごとに `role`、`opportunityScore`、`intentLabel`、語彙類似度、同時ランクイン度、FAQ件数、根拠sourceを返す。生成APIは既存のキーワード探索、同時ランクイン、FAQ、共起語、機会スコアを使う分析キューの `TopicClusterGenerateJob` を登録し、`202 Accepted` と `jobId` を返す。

### 7.6 順位・リライト・レポートAPI

| Phase | Method | Path | 概要 |
| --- | --- | --- | --- |
| Phase 2 | POST | `/api/projects/{projectId}/rank-check/jobs` | 順位チェック登録 |
| Phase 2 | GET | `/api/projects/{projectId}/rank-check/jobs/{jobId}/results` | 順位チェック結果取得 |
| Phase 2 | GET | `/api/projects/{projectId}/rank-results` | 順位履歴・順位分布取得 |
| Phase 2 | GET | `/api/projects/{projectId}/alerts` | アラート定義一覧 |
| Phase 2 | GET | `/api/projects/{projectId}/alert-events` | アラート発火履歴一覧。alert_id、event_type、期間で検索 |
| Phase 2 | POST | `/api/projects/{projectId}/alerts` | アラート条件作成 |
| Phase 2 | PUT | `/api/projects/{projectId}/alerts/{alertId}` | アラート条件更新 |
| Phase 2 | DELETE | `/api/projects/{projectId}/alerts/{alertId}` | `status=disabled`へ無効化 |
| Phase 2 | POST | `/api/projects/{projectId}/alerts/{alertId}/enable` | `status=active`へ再有効化 |
| Phase 3 | GET | `/api/projects/{projectId}/rewrite/tasks` | リライト候補一覧 |
| Phase 3 | GET | `/api/projects/{projectId}/rewrite/tasks/{taskId}` | リライトタスク詳細取得 |
| Phase 3 | PUT | `/api/projects/{projectId}/rewrite/tasks/{taskId}` | リライトタスク更新 |
| Phase 3 | GET | `/api/projects/{projectId}/cannibalization/candidates` | カニバリ候補一覧 |
| Phase 3 | POST | `/api/projects/{projectId}/cannibalization/refresh` | カニバリ候補の再計算ジョブ登録 |
| Phase 3 | POST | `/api/projects/{projectId}/reports` | レポート生成 |
| Phase 3 | GET | `/api/projects/{projectId}/reports/{reportId}` | レポート詳細取得 |
| Phase 3 | GET | `/api/projects/{projectId}/reports/{reportId}/download` | レポートファイルの短時間ダウンロードURL発行。発行操作を監査する |
| Phase 3 | POST | `/api/projects/{projectId}/reports/{reportId}/share` | 共有URL発行 |
| Phase 3 | DELETE | `/api/projects/{projectId}/reports/{reportId}/share` | 共有URL失効 |
| Phase 3 | GET | `/api/report-shares/{token}` | 共有URLによるレポート取得 |
| Phase 3 | POST | `/api/projects/{projectId}/exports` | CSV/Excelエクスポートジョブ登録 |
| Phase 3 | POST | `/api/projects/{projectId}/imports/upload-url` | インポート元ファイル用の期限付きURL発行 |
| Phase 3 | POST | `/api/projects/{projectId}/imports` | CSV/Excelインポートジョブ登録 |
| Phase 3 | GET | `/api/projects/{projectId}/imports/{importId}` | インポート状態/検証結果取得 |
| Phase 3 | GET | `/api/projects/{projectId}/imports/{importId}/errors` | インポート検証エラー一覧 |
| Phase 3 | GET | `/api/projects/{projectId}/connectors` | 外部連携コネクタ設定スタブ一覧 |
| Phase 3 | POST | `/api/projects/{projectId}/connectors` | 外部連携コネクタ設定スタブ作成。Secret実値は保存/返却しない |
| Phase 3 | PUT | `/api/projects/{projectId}/connectors/{connectorId}` | 外部連携コネクタ設定スタブ更新 |
| Phase 3 | DELETE | `/api/projects/{projectId}/connectors/{connectorId}` | `status=disabled`へ無効化 |
| Phase 3 | POST | `/api/projects/{projectId}/connectors/{connectorId}/test` | 実データ取得を伴わない接続テスト/スタブ実行 |
| Phase 3 | GET | `/api/projects/{projectId}/connectors/{connectorId}/runs` | 外部連携スタブ実行履歴取得 |
| Phase 3 | POST | `/api/projects/{projectId}/ai/chat` | AIアシスタント実行 |

ISSUE-P3-001では、上記Phase 3 APIのContracts/DTO、ルートグループ、projectIdスコープ検証の土台までを追加する。リライト、カニバリ、レポート、インポート、外部連携、AIの個別エンドポイント本体とジョブ登録はISSUE-P3-002からISSUE-P3-006で実装する。

## 8. 主要リクエスト/レスポンスモデル

| モデル | 用途 | 主な項目 |
| --- | --- | --- |
| `WorkspaceSettingsRequest` | ワークスペース設定更新 | `name`、`defaultLocation`、`defaultLanguage`、`retentionSettings`、`notificationDefaults` |
| `ProjectRequest` | プロジェクト作成/更新 | `name`、`defaultLocation`、`defaultLanguage`、`kpi`、`memo` |
| `SiteRequest` | サイト作成/更新 | `domain`、`canonicalUrl`、`type`、`memo` |
| `ApiCredentialCreateRequest` | API認証情報作成 | `provider`、`secretValue`または`keyRef`のいずれか一方。レスポンスに`secretValue`は返さない。 |
| `ApiCredentialUpdateRequest` | API認証情報更新 | `provider`、`status`等のメタ情報。秘密値変更はrotateで行う。 |
| `ApiCredentialRotateRequest` | API認証情報ローテーション | `newSecretValue`または`newKeyRef`のいずれか一方。契約スコープはAPI認証情報APIでは管理しない。 |
| `KeywordDiscoveryRequest` | キーワード探索 | `seedKeyword`、`sources`、`limit`、`filter`、`sortBy`、`orderBy`、`syncPreferred` |
| `SearchVolumeJobRequest` | 一括検索ボリューム調査 | `keywords`、`location`、`language`、`seoDifficulty`、`aggregationPeriodMonths`。MVPのCSV入力はブラウザ内でパースし、APIへは`keywords` JSON配列として送る。 |
| `ProjectDashboardResponse` | ダッシュボード | Phase 1項目として`keywordDiscoverySummary`、`searchVolumeSummary`、`opportunityScoreSummary`、`creditUsageSummary`、`jobSummary`、`notificationSummary`を返す。Phase 2では`competitorSummary`、`influxSummary`、`contentAnalysisSummary`、`briefSummary`、`rankSummary`、`rankAlertSummary`を追加する。Phase 3では`rewriteSummary`、`cannibalizationSummary`、`reportSummary`、`aiSummary`を追加する。 |
| `ContentAnalyzeRequest` | コンテンツ分析 | `keyword`、`includeContentSearch`、`includeHeadline`、`includeCoOccurrence`、`limit` |
| `RankCheckJobRequest` | 順位チェック | `keywords`、`targets`、`matchType`、`depth`、`withMetrics`、`deduplicate` |
| `ReportRequest` | レポート生成 | `reportType`、`period`、`format`（pdf/excel）、`sections`、`shareExpiresAt`。生成完了後は`reports.file_uri`を保持し、ダウンロードは専用APIで短時間URLを返す。 |
| `ExportRequest` | エクスポート | `exportType`、`format`（csv/excel）、`filter`、`columns` |
| `ImportRequest` | インポート | `importType`（keywords/rankings/competitors/briefs/tasks）、`format`（csv/excel）、`sourceFileUri`、`validationMode`（初期実装はstrict） |
| `ConnectorSettingsRequest` | 外部連携スタブ設定 | `connectorType`、`name`、`authRef`、`settings`、`status`。Secret/OAuth実値はSecret Store参照のみ |
| `AiChatRequest` | AIアシスタント | `message`、`conversationId`、`allowedTools`、`referenceScope` |

## 9. 入力制約

| 対象 | 制約 |
| --- | --- |
| キーワード | 前後空白を除去し、Unicode正規化後に1文字以上。登録時は言語別`text_hash`で重複排除する。 |
| URL/ドメイン | `http`または`https`を許可し、保存時は正規化URLとドメインを分離する。 |
| 一括検索ボリューム | `keywords`は1から50,000件。重複排除後の件数でクレジット消費見込みを表示し、予算上限による停止は行わない。 |
| 順位チェック | `targets`は1から50件。各targetはURLまたはドメインと`targetType`を持つ。`depth`は30/40/50/60/70/80/90/100。 |
| ページング | `pageSize`は1から200。外部API結果の大容量取得はジョブまたはエクスポートに寄せる。 |
| 共有URL | `shareExpiresAt`は未来日時のみ。共有トークンは十分なランダム値をレスポンスへ一度だけ返し、DBにはハッシュのみ保存する。未知または改ざんトークンは404、期限切れまたは明示失効済みトークンは410を返す。 |
| レポート | `format`はpdfまたはexcel。`period`は月次レポートでは`YYYY-MM`を基本とし、生成済みファイルのダウンロードはスコープ確認後に短時間URLを発行する。 |
| CSV/Excel | インポートはPhase 3。ファイルはStorageへ直接アップロードし、APIサーバーはファイル本体を保持しない。`keywords`は`keyword`、`rankings`は`keyword,target,position`、`competitors`は`domain`、`briefs`は`title`、`tasks`は`targetUrl`を必須列とする。 |

MVPの一括検索ボリューム画面でCSVファイルを選択した場合、ファイル本体はAPIへアップロードしない。Blazor UIがブラウザ内でCSVをパースし、空行除外・重複除外・上限検証後に`SearchVolumeJobRequest.keywords`へJSON配列として設定する。

## 10. ラッコキーワードAPI連携

### 10.1 共通処理

外部API呼び出しは`IRakkoKeywordClient`に集約し、以下を共通処理とする。

- `X-API-Key`はSecret Storeから取得し、ログやレスポンスには出さない。
- `meta.consumedCredit`を`external_api_calls.consumed_credit`へ保存する。
- リクエスト/レスポンスの圧縮JSONはStorageへ保存し、DBにはURIとハッシュを保存する。
- 429/500/503はジョブ状態に応じて指数バックオフ + ジッターで再試行する。
- 400/402/403は再試行せず`failed_fatal`として扱い、必要に応じてDiscord通知する。

### 10.2 外部APIマッピング

| Method | Path | Request DTO | 本システムでの用途 |
| --- | --- | --- | --- |
| POST | `/v1/suggest-keywords` | `SuggestKeywordsDto` | サジェスト収集、検索ソース別候補語取得 |
| POST | `/v1/related-keywords` | `RelatedKeywordsDto` | 関連語、ロングテール、除外語候補 |
| POST | `/v1/other-keywords` | `OtherKeywordsDto` | LSI/PAA、検索意図、見出し候補 |
| POST | `/v1/question-search` | `SearchQuestionDto` | FAQ、FAQPage候補 |
| POST | `/v1/ranking-keywords` | `RankingKeywordsDto` | 同時ランクイン、クラスタリング |
| POST | `/v1/search-volume` | `SearchVolumeHistoryDto` | 一括検索ボリューム調査登録 |
| GET | `/v1/search-volume/{requestId}/status` | なし | 一括調査ステータス確認 |
| POST | `/v1/search-volume/{requestId}/results` | `SearchVolumeResultsDto` | 検索ボリューム、難易度、CPC、月別推移取得 |
| GET | `/v1/search-volume/locations` | なし | 地域マスタ同期 |
| GET | `/v1/search-volume/languages` | なし | 言語マスタ同期 |
| POST | `/v1/influx-keywords` | `InfluxKeywordsKeywordDto` | 獲得キーワード、競合ギャップ |
| POST | `/v1/influx-pages` | `InfluxPagesDto` | 獲得ページ、リライト候補 |
| POST | `/v1/competitive` | `CompetitiveDto` | 競合サイト抽出 |
| POST | `/v1/content-search` | `ContentSearchDto` | 集客コンテンツ検索 |
| POST | `/v1/headline` | `HeadlineDto` | SERP見出し抽出 |
| POST | `/v1/co-occurrence` | `CoOccurrenceDto` | 共起語、語彙不足分析 |
| POST | `/v1/search-rank` | `SearchRankHistoryDto` | 順位チェック登録 |
| GET | `/v1/search-rank/{requestId}/status` | なし | 順位チェック完了待ち |
| POST | `/v1/search-rank/{requestId}/results` | `SearchRankResultsDto` | 順位結果、順位分布、推定流入取得 |

### 10.3 外部DTO制約

| DTO | 必須項目 | 主な制約 |
| --- | --- | --- |
| `SuggestKeywordsDto` | `keyword` | `modes`はgoogle/bing/youtube/googleVideo/amazon/rakuten/googleShopping/googleImage。 |
| `RelatedKeywordsDto` | `keyword` | `matchType`はpartial/phrase/prefix/suffix/word系。`limit`は最大25,000。 |
| `OtherKeywordsDto` | `keyword` | `sortBy`はimportance等。 |
| `SearchQuestionDto` | `keyword` | `limit`は最大200。 |
| `RankingKeywordsDto` | `keyword` | `searchTop`は3/5/10/20/30/50、`searchRange`は10/20/30/50/100。 |
| `SearchVolumeHistoryDto` | `keywords` | 1から50,000語。`aggregationPeriodMonths`は12/24/36/48。 |
| `SearchVolumeResultsDto` | なし | `limit`は最大50,000。 |
| `InfluxKeywordsKeywordDto` | `targets` | `targets`は1から20件。 |
| `InfluxPagesDto` | `targets` | `targets`は1から20件。 |
| `CompetitiveDto` | `url` | 対象ドメインURL。 |
| `ContentSearchDto` | `keyword` | `limit`は最大5,000。 |
| `HeadlineDto` | `keyword` | `limit`は最大20。 |
| `CoOccurrenceDto` | `keyword` | URL別詳細は`getDetails=true`を既定とする。 |
| `SearchRankHistoryDto` | `keywords`, `urls` | `urls`は1から50件。`matchType`はurl/forward_url/domain/sub_domain。 |
| `SearchRankResultsDto` | なし | `withAggregation`は必要時のみtrue。 |

## 11. 監査・ログ

| 操作 | 監査対象 |
| --- | --- |
| APIキー作成/更新/無効化/ローテーション | `audit_logs`、`api_credentials` |
| 外部API呼び出し | 詳細は`external_api_calls`、分割requestIdは`job_external_requests`、操作単位の監査要約は`audit_logs` |
| CSV/Excel出力・ダウンロード | `data_exports`、`audit_logs` |
| レポート出力・ダウンロード | `reports`、`artifact_versions`、`audit_logs` |
| インポート | `data_imports`、`audit_logs` |
| AI実行 | `ai_sessions`、`ai_messages`、`audit_logs` |
| 共有URL発行/失効/アクセス | `reports`、`audit_logs` |

## 12. OpenAPI生成・テスト方針

- 内部APIはASP.NET CoreのOpenAPI出力を正本にし、PR時に差分をレビューする。
- 外部APIは`rakko-keyword-api-docs.json`をvendor仕様として保存し、更新時はDTO生成差分と契約テストを確認する。
- APIテストは、正常系、バリデーションエラー、プロジェクトスコープ不一致、外部API 429/402/403/500/503、ジョブ再実行、監査ログ記録を対象にする。
