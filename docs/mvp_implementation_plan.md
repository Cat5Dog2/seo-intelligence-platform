# MVP実装計画

**SEOインテリジェンス基盤**

## 1. 目的

本書はPhase 1: MVPの実装順、対象範囲、完了条件をCodex実装時に参照しやすい粒度でまとめる。
要件の正本は`requirements.md`、方式の正本は`basic_design.md`、API契約の正本は`api_design.md`、DB論理設計の正本は`db_design.md`、ジョブ設計の正本は`job_design.md`とする。

## 2. MVPスコープ

| 区分 | 対象 |
| --- | --- |
| 管理 | 単一ワークスペース、プロジェクト、サイト、API認証情報、Discord通知設定、監査ログ |
| キーワード調査 | サジェスト、関連語、LSI/PAA、FAQ、同時ランクイン語の統合取得 |
| 検索ボリューム | 最大50,000語の非同期調査、進捗確認、結果保存 |
| スコアリング | 検索ボリューム、難易度、CPC、競合性、トレンド、関連度による機会スコア |
| ダッシュボード | キーワード探索、一括調査、機会スコア、APIクレジット、失敗ジョブ、通知失敗 |
| CSV出力 | Phase 1対象データのCSV出力、状態確認、ダウンロード、監査 |
| 運用 | ジョブ失敗、402クレジット不足、429/500/503リトライ、Discord通知 |

## 3. 対象外

| 区分 | 扱い |
| --- | --- |
| 複数ユーザー/RBAC/SSO | Phase 4以降。操作主体は固定値`developer`。 |
| 競合分析/コンテンツ分析/順位監視 | Phase 2。DBテーブルは作成してもUI/APIの受入対象外。 |
| AI/リライト/カニバリ/レポート/Excel/インポート | Phase 3。 |
| EC/YouTube/画像企画、広告/LP判断 | 推奨バックログ。 |
| 外部APIのReal利用前提テスト | 既定はMock。Realは契約スコープとクレジットを確認して限定実行。 |

## 4. 実装順

| 順 | 対象 | 主要成果物 | 主な確認 |
| --- | --- | --- | --- |
| 1 | ソリューション骨格 | `SeoIntelligence.sln`、`src/`、`tests/`、共通設定、DI、ロギング | `dotnet build` |
| 2 | Domain/Application基盤 | エンティティ、値オブジェクト、ユースケース境界、共通結果型 | Unit test |
| 3 | Infrastructure基盤 | EF Core DbContext、migration、PostgreSQL、Redis、Storage抽象 | migration適用、DB接続 |
| 4 | API共通 | レスポンスエンベロープ、エラー、ページング、Correlation ID、OpenAPI | API smoke |
| 5 | 管理API | Workspace、Project、Site、API credentials、Notification channels | CRUD/ソフト削除 |
| 6 | Secret/監査 | Secret Store参照、秘密値非返却、audit_logs | 秘密値がレスポンス/ログに出ない |
| 7 | 外部API Mock/Client | `IRakkoKeywordClient`、Mock、DTO生成/手動境界、external_api_calls | Contract/Integration |
| 8 | ジョブ基盤 | Worker、Hangfire、`jobs`、冪等キー、キャンセル/リトライ | ジョブ状態遷移 |
| 9 | キーワード探索 | `KeywordDiscoveryJob`、候補語保存、同期/非同期分岐 | T-MVP-007/008 |
| 10 | 一括検索ボリューム | 登録、ポーリング、結果取得、分割、重複除外 | T-MVP-009から011 |
| 11 | 機会スコア | `OpportunityScoringJob`、`project_keyword_scores` | スコア再計算 |
| 12 | CSV出力 | `DataExportJob`、Storage、短時間URL、監査 | T-MVP-012 |
| 13 | Discord通知 | テスト通知、job_failed、credit_low、再送 | T-MVP-006/017 |
| 14 | Blazor MVP画面 | S-001、S-900、S-020、S-030、S-010 | UI/E2E |
| 15 | 受入固め | AC-001、AC-002、AC-003、AC-008からAC-014、AC-019 | targeted test |

## 5. MVP内部API

| 領域 | Method | Path |
| --- | --- | --- |
| Workspace | GET/PUT | `/api/admin/workspace` |
| API認証情報 | GET/POST | `/api/admin/api-credentials` |
| API認証情報 | GET/PUT/DELETE | `/api/admin/api-credentials/{credentialId}` |
| API認証情報 | POST | `/api/admin/api-credentials/{credentialId}/enable` |
| API認証情報 | POST | `/api/admin/api-credentials/{credentialId}/rotate` |
| 通知設定 | GET/POST | `/api/admin/notification-channels` |
| 通知設定 | GET/PUT/DELETE | `/api/admin/notification-channels/{channelId}` |
| 通知設定 | POST | `/api/admin/notification-channels/{channelId}/enable` |
| 通知設定 | POST | `/api/admin/notification-channels/{channelId}/test` |
| 通知履歴 | GET | `/api/admin/notification-deliveries` |
| 通知履歴 | GET | `/api/admin/notification-deliveries/{deliveryId}` |
| 通知履歴 | POST | `/api/admin/notification-deliveries/{deliveryId}/retry` |
| マスタ | POST | `/api/admin/master-data/sync` |
| 監査 | GET | `/api/admin/external-api-calls` |
| 監査 | GET | `/api/admin/audit-logs` |
| 監査 | GET | `/api/admin/audit-logs/{auditLogId}` |
| Project | GET/POST | `/api/projects` |
| Project | GET/PUT/DELETE | `/api/projects/{projectId}` |
| Project | POST | `/api/projects/{projectId}/restore` |
| Site | GET/POST | `/api/projects/{projectId}/sites` |
| Site | GET/PUT/DELETE | `/api/projects/{projectId}/sites/{siteId}` |
| Site | POST | `/api/projects/{projectId}/sites/{siteId}/restore` |
| Dashboard | GET | `/api/projects/{projectId}/dashboard` |
| Master data | GET | `/api/master-data/locations` |
| Master data | GET | `/api/master-data/languages` |
| Jobs | GET | `/api/jobs` |
| Jobs | GET | `/api/jobs/{jobId}` |
| Jobs | POST | `/api/jobs/{jobId}/cancel` |
| Jobs | POST | `/api/jobs/{jobId}/retry` |
| Keyword | POST | `/api/projects/{projectId}/keyword-discovery/suggest` |
| Search volume | POST | `/api/projects/{projectId}/search-volume/jobs` |
| Search volume | GET | `/api/projects/{projectId}/search-volume/jobs/{jobId}` |
| Search volume | GET | `/api/projects/{projectId}/search-volume/jobs/{jobId}/results` |
| Export | POST | `/api/projects/{projectId}/exports/csv` |
| Export | GET | `/api/projects/{projectId}/exports/{exportId}` |
| Export | GET | `/api/projects/{projectId}/exports/{exportId}/download` |

## 6. MVPテーブル

| 領域 | テーブル |
| --- | --- |
| Workspace/Project | `workspaces`, `projects`, `sites` |
| Secrets/通知/監査 | `api_credentials`, `api_contract_scopes`, `notification_channels`, `notification_deliveries`, `audit_logs` |
| 外部API/ジョブ | `locations`, `languages`, `external_api_calls`, `jobs`, `job_external_requests` |
| キーワード | `keyword_seeds`, `keywords`, `keyword_suggestions`, `related_keywords`, `questions`, `lsi_paa_items`, `ranking_keywords` |
| 検索ボリューム | `search_volume_jobs`, `search_volume_results`, `keyword_metrics`, `keyword_monthly_volumes`, `project_keyword_scores` |
| 出力 | `data_exports` |

Phase 2/3のテーブルを初回migrationに含める場合でも、MVPの受入は上記テーブルに対するAPI/ジョブ/監査を優先する。

## 7. MVPジョブ

| ジョブ | 起動 | 成果物 |
| --- | --- | --- |
| `MasterDataSyncJob` | 手動/定期 | `locations`, `languages` |
| `KeywordDiscoveryJob` | 手動 | 候補語、FAQ、LSI/PAA、同時ランクイン語 |
| `RegisterSearchVolumeJob` | 手動 | `search_volume_jobs`, `job_external_requests` |
| `PollSearchVolumeStatusJob` | 再スケジュール | `job_external_requests.status` |
| `FetchSearchVolumeResultsJob` | status完了後 | `search_volume_results`, `keyword_metrics`, `keyword_monthly_volumes` |
| `OpportunityScoringJob` | 調査完了後 | `project_keyword_scores` |
| `DataExportJob` | 手動 | `data_exports`, Storage, `audit_logs` |
| `NotificationDeliveryJob` | イベント発生時 | `notification_deliveries` |

## 8. 受入チェック

| ID | 確認 |
| --- | --- |
| AC-001 | Phase 1で使うラッコAPIの正常系と主要エラー系がMock/契約テストで通る。 |
| AC-002 | 1シードから候補語を統合取得し、保存、フィルタできる。 |
| AC-003 | 1,000語以上の検索ボリュームジョブを登録し、完了監視と結果保存ができる。 |
| AC-008 | 別projectIdのデータ混在が起きない。 |
| AC-009 | 429/402/403/500/503の処理分岐、通知、監査が仕様通り。403はfatal、500/503はリトライ上限超過時の失敗保持まで確認する。 |
| AC-010 | 管理系CRUDが通る。 |
| AC-011 | DELETEが物理削除せず状態変更になり、復元/再有効化できる。 |
| AC-012 | Phase 1通知と送信履歴、失敗時再送状態を確認できる。 |
| AC-013 | CSV出力、状態取得、ダウンロード、audit_logs記録が通る。 |
| AC-014 | APIキー操作、外部API実行、CSV出力、ジョブ操作の監査ログを検索できる。 |
| AC-019 | Phase 1 API/DB/ジョブ/監査の対応関係を確認できる。 |

## 9. 実装時の判断ルール

- 正本に差がある場合は、`requirements.md`のスコープ、`api_design.md`のAPI契約、`db_design.md`のDB定義、`job_design.md`の状態遷移を優先する。
- MVPのCSV入力はファイルアップロードではなく、Blazor UIでパースして`keywords` JSON配列として送信する。
- 業務データAPIではbody内の`projectId`を受け付けず、URL上の`projectId`とDB上の`project_id`一致を必ず検証する。
- 秘密値は保存直後も再表示しない。DB、ログ、レスポンス、監査ログにはSecret参照名またはマスク値のみ残す。
- `jobs`テーブルを業務状態の正本にし、Hangfire内部状態は監査・画面表示の正本にしない。
- 外部APIのrequest/response本体はStorageへ置き、DBにはURI、ハッシュ、クレジット、ステータスを残す。
- Real外部APIは明示的に切り替えた場合だけ使う。CIと通常開発はMockを既定にする。
