# 外部API連携設計書

**ラッコキーワードAPIを中核にしたSEOインテリジェンス基盤**

_SEO Intelligence Platform / SEOインテリジェンス基盤_

| 項目 | 内容 |
| --- | --- |
| 文書ID | EXTAPI-RKSEO-001 |
| 作成日 | 2026-05-30 |
| 対象 | ラッコキーワードAPI、Discord、AI、将来GSC/GA4/CMS/BIコネクタ |
| 関連文書 | api_design.md / job_design.md / db_design.md / operations_runbook.md |
| 外部仕様 | rakko-keyword-api-docs.json（OpenAPI 3.1、API v1.12.0） |

## 改訂履歴

| 版 | 日付 | 内容 | 作成/更新 |
| --- | --- | --- | --- |
| 1.0 | 2026-05-30 | 初版作成。外部API認証、クライアント、クレジット、キャッシュ、契約テストを定義。 | ChatGPT |
| 1.1 | 2026-07-26 | ラッコキーワードAPI v1.12.0対応。地域/言語マスタを`/v1/metadata/*`へ移行、消費クレジット料率を反映。 | Claude |
| 1.2 | 2026-08-17 | ラッコキーワードAPI v1.14.0対応。SERP詳細取得エンドポイントを追加し、よくある質問検索の料率変更(3→1.5)を反映。 | Claude |

## 1. 目的

本書は、外部API連携の境界、認証、DTO生成、エラー処理、クレジット管理、キャッシュ、監査、契約テスト、将来コネクタの差し込み口を定義する。

## 2. 連携先一覧

| 連携先 | Phase | 用途 | 認証 |
| --- | --- | --- | --- |
| ラッコキーワードAPI | MVP以降 | SEOデータ取得、検索ボリューム、順位チェック | `X-API-Key` |
| Discord Webhook | MVP以降 | ジョブ失敗、クレジット不足402、順位、レポート通知 | Webhook URL |
| AI API | Phase 3 | ブリーフ、リライト、レポート要約、AIアシスタント | Provider別APIキーまたはManaged Identity |
| Google Search Console | 拡張 | 実クリック/表示/CTR/平均順位 | OAuth |
| GA4 | 拡張 | セッション/CV/収益 | OAuth |
| CMS | 拡張 | 記事投稿/同期 | APIキー/OAuth |
| BI/DWH | 拡張 | データ出力 | サービスアカウント等 |

## 3. ラッコキーワードAPIクライアント

| 項目 | 設計 |
| --- | --- |
| 実装場所 | Infrastructure層の`IRakkoKeywordClient`実装。 |
| DTO | `rakko-keyword-api-docs.json`から生成し、Application層では業務DTOへ変換する。 |
| Base URL | 設定値`RakkoKeyword__BaseUrl`。 |
| APIキー | Secret Storeから取得し、DBには`api_credentials.key_ref`のみ保存する。 |
| タイムアウト | 通常30秒、非同期登録/結果取得は60秒を初期値とする。 |
| User-Agent | アプリ名、バージョン、環境を含める。 |
| Correlation | 内部`correlation_id`をログとDBに保存する。 |

## 4. エンドポイント用途

| Path | 用途 | 主な保存先 |
| --- | --- | --- |
| `/v1/suggest-keywords` | 検索ソース別サジェスト | `keyword_suggestions`、`keywords` |
| `/v1/related-keywords` | 関連語探索 | `related_keywords`、`keywords` |
| `/v1/other-keywords` | LSI/PAA | `lsi_paa_items` |
| `/v1/question-search` | FAQ（v1.14.0で相対需要`relativeDemand`・出現時期`firstSeenRange`を返す） | `questions` |
| `/v1/ranking-keywords` | 同時ランクイン | `ranking_keywords` |
| `/v1/search-volume` | 一括検索ボリューム登録 | `search_volume_jobs`、`job_external_requests` |
| `/v1/search-volume/{requestId}/status` | 調査ステータス | `job_external_requests` |
| `/v1/search-volume/{requestId}/results` | 検索ボリューム結果 | `search_volume_results`、`keyword_metrics`、`keyword_monthly_volumes` |
| `/v1/metadata/locations` | 地域マスタ（クレジット消費なし・認証不要） | `locations` |
| `/v1/metadata/languages` | 言語マスタ（クレジット消費なし・認証不要） | `languages` |
| `/v1/influx-keywords` | 獲得キーワード | `influx_keyword_results` |
| `/v1/influx-pages` | 獲得ページ | `influx_page_results` |
| `/v1/competitive` | 競合サイト | `competitive_results`、`competitor_sites` |
| `/v1/content-search` | 集客コンテンツ | `content_search_results` |
| `/v1/headline` | SERP見出し | `serp_headline_pages`、`serp_headlines` |
| `/v1/co-occurrence` | 共起語 | `co_occurrence_words`、`co_occurrence_page_details` |
| `/v1/search-rank` | 順位チェック登録 | `rank_check_jobs`、`job_external_requests` |
| `/v1/search-rank/{requestId}/status` | 順位チェックステータス | `job_external_requests` |
| `/v1/search-rank/{requestId}/results` | 順位結果（v1.14.0で`entryNo`が必須項目として追加） | `rank_results` |
| `/v1/search-rank/{requestId}/results/{entryNo}/serp` | 順位チェック時に取得したSERP詳細（クレジット消費なし） | 取込先は未定。現状はクライアント経由の取得のみ |

## 5. クレジット消費監視

| 項目 | 設計 |
| --- | --- |
| 消費記録 | 全外部APIレスポンスの`meta.consumedCredit`を`external_api_calls.consumed_credit`へ保存する。レスポンス受信後に解析・変換で失敗した場合も、`status_code`と`consumed_credit`には外部APIが実際に返した値を記録し、内部の失敗分類は`error_code`と呼び出し結果で表す。 |
| 集計単位 | 全体、プロジェクト、APIキー、ジョブ、日次、月次。 |
| 集計境界 | 日次はAsia/Tokyo 0:00、月次はAsia/Tokyo 毎月1日0:00で区切る。 |
| 予算管理 | 日次/月次予算、予算上限、承認制、予算超過による事前停止はアプリ内では管理しない。 |
| 実行前表示 | 推定消費クレジットを表示・監査できるようにするが、アプリ内の予算上限設定は持たない。検索ボリューム登録は0.03/キーワード（seoDifficulty有効時は追加0.75/キーワード）、外部リクエスト単位で最低15クレジットとして見積る。よくある質問検索はv1.14.0で1リクエスト1.5クレジット（v1.12.0は3）。実績値は`meta.consumedCredit`をそのまま記録するため、料率変更はコード変更なしで追随する。 |
| 402処理 | 再試行せずfailed_fatal。クレジット不足としてDiscord通知し、契約側の残量確認を運用手順へ誘導する。 |

## 6. キャッシュ・再利用

| 対象 | 方針 |
| --- | --- |
| 検索指標 | `keyword_metrics`、`keyword_monthly_volumes`を契約スコープ、地域、言語、取得日時で再利用する。 |
| 契約スコープ | `api_contract_scopes.scope_key`が一致する場合のみ共有キャッシュとして扱う。契約スコープは管理画面/APIでは管理しない。 |
| 画面返却 | キャッシュを使っても、画面/APIではプロジェクトスコープの結果として返す。 |
| ローデータ | request/responseの圧縮JSONはStorage、DBはURIとハッシュのみ。 |
| 無効化 | API契約変更時は運用手順またはマイグレーション/SeedDataで旧`api_contract_scopes`をarchivedにし、新しい`scope_key`を発行する。 |

## 7. HTTPエラー処理

| HTTP | 分類 | 処理 |
| --- | --- | --- |
| 200/201 | 成功 | レスポンス保存、クレジット記録、後続ジョブ起動。 |
| 200だが解析不能 | 致命的 | 契約違反(`invalid_response`)としてfailed_fatal。再試行しても解消せず、課金される呼び出しを繰り返すだけのため再試行しない。監査には実際のHTTPステータスと消費クレジットを記録する。 |
| 400 | 致命的 | 内部バリデーション不足としてfailed_fatal。再試行しない。 |
| 402 | 致命的 | クレジット不足。再試行せず通知。 |
| 403 | 致命的 | APIキー無効/権限不足。再試行せず通知。 |
| 429 | 一時的 | 指数バックオフ + ジッター、同時実行数を一時低下。 |
| 500 | 一時的 | 短期リトライ後にfailed_retryable。 |
| 503 | 一時的 | 長めのバックオフで再キュー。 |
| timeout | 一時的 | リトライ対象。リクエストハッシュで重複を追跡。 |

## 8. DTO生成・仕様更新

| 項目 | 方針 |
| --- | --- |
| vendor仕様 | `docs/rakko-keyword-api-docs.json`を保存し、更新時は差分レビューする。 |
| 生成物 | Infrastructure層に外部DTOを生成する。外部DTOを内部API/画面公開用のContractsへ直接配置しない。 |
| 業務DTO | Application層で内部DTOへ変換し、外部仕様変更の影響を閉じ込める。 |
| 互換性 | 追加フィールドは許容、必須フィールド削除/型変更は破壊的変更として扱う。 |
| 契約テスト | 主要DTO、必須項目、エラー形式、requestId型、consumedCreditを検証する。生成DTOの形状はスキーマと再帰照合し、プロパティの削除/改名とrequired欠落を検知する。 |

## 9. Discord連携

| 項目 | 設計 |
| --- | --- |
| Secret | Webhook URLはSecret Storeへ保存し、DBは`webhook_secret_ref`のみ保持する。 |
| 通知種別 | job_failed、credit_low、rank_alert、report_completed。 |
| 履歴 | `notification_deliveries`へpayload_hash、status、error、retry_countを保存する。 |
| 再送 | 429/5xx/timeoutはretrying。最大再送後failed。 |
| テスト送信 | 管理画面から`POST /api/admin/notification-channels/{channelId}/test`で実行する。 |

## 10. AI API連携

| 項目 | 設計 |
| --- | --- |
| 抽象化 | `IAiContentService`でProviderを差し替え可能にする。 |
| 秘密情報除去 | プロンプト生成前にAPIキー、Webhook、認証情報、個人情報を除去する。 |
| 参照データ | `reference_data_json`へ根拠データIDと要約を保存する。 |
| 出力 | 下書きとして保存し、人間レビュー前提にする。 |
| 監査 | prompt、response、tool_calls、token_usageを`ai_messages`へ保存する。 |

## 11. 将来コネクタ設計

| コネクタ | 最小インターフェース |
| --- | --- |
| GSC | site、query、page、date rangeでクリック/表示/CTR/平均順位を返す。 |
| GA4 | property、date range、landing pageでsession/CV/revenueを返す。 |
| CMS | draft作成、記事更新、公開状態取得、URL同期を行う。 |
| BI/DWH | dataset/tableまたはfile exportへ分析結果を出力する。 |

初期版では設定、インターフェース、スタブまでをPhase 3対象とし、実データ連携は推奨バックログとして扱う。Phase 3では`external_connector_settings`にconnector_type、Secret参照、settings_json、statusを保存し、`external_connector_runs`に接続テスト/スタブ実行の履歴を保存する。OAuthトークンやAPIキー実値はSecret Store参照のみ保持し、DB/APIレスポンスには返さない。

## 12. テスト観点

| 観点 | 確認 |
| --- | --- |
| 認証 | APIキー実値がログ/DB/APIレスポンスに出ない。 |
| 契約 | OpenAPI由来DTOの必須項目と型が一致する。 |
| エラー | 429/402/403/500/503でジョブ状態と通知が仕様通り。 |
| クレジット | consumedCreditが全体/プロジェクト/APIキー/ジョブ単位で集計され、402時にfailed_fatalと通知へ分岐する。 |
| キャッシュ | 契約スコープ不一致のデータを再利用しない。 |
| ローデータ | request/response URIとハッシュが保存される。 |
| 外部連携スタブ | GSC/GA4/CMS/BIの設定、Secret参照、接続テスト履歴が保存され、実データ取得は行わない。 |
