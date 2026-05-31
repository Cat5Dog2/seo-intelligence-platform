# テスト計画書

**ラッコキーワードAPIを中核にしたSEOインテリジェンス基盤**

_SEO Intelligence Platform / SEOインテリジェンス基盤_

| 項目 | 内容 |
| --- | --- |
| 文書ID | TEST-RKSEO-001 |
| 作成日 | 2026-05-30 |
| 対象 | API / Blazor UI / Worker / DB / 外部API連携 / 運用機能 |
| 関連文書 | requirements.md / basic_design.md / api_design.md / db_design.md / screen_design.md / job_design.md |

## 改訂履歴

| 版 | 日付 | 内容 | 作成/更新 |
| --- | --- | --- | --- |
| 1.0 | 2026-05-30 | 初版作成。テストレベル、MVP受入、障害系、契約テスト、実行方針を定義。 | ChatGPT |

## 1. 目的

本書は、要件定義書の受入基準を満たすためのテスト方針、対象、優先度、テストデータ、外部APIモック、実行タイミングを定義する。

## 2. テスト方針

| 方針 | 内容 |
| --- | --- |
| MVP優先 | Phase 1の機能、API、DB、ジョブ、監査、運用を最初の自動テスト対象にする。 |
| 外部APIはモック中心 | 通常CIではラッコAPIを直接呼ばず、OpenAPI仕様に基づくモック/契約テストを使う。 |
| 障害系を早期実装 | 429/402/403/500/503、APIキー無効、ジョブ再実行をMVPで確認する。 |
| スコープ混在防止 | `projectId`の不一致、別プロジェクトデータ混在を統合テストで確認する。 |
| 監査必須 | APIキー、外部API、CSV/レポート、AI、共有URL、ジョブ操作は監査ログを検証する。 |

## 3. テストレベル

| レベル | 対象 | 主な確認 |
| --- | --- | --- |
| Unit | Domain/Application | スコアリング、入力検証、DTOマッピング、状態遷移。 |
| Integration | API/DB/Worker/Redis | CRUD、ジョブ登録、DB保存、スコープ検証、監査ログ。 |
| Contract | ラッコAPIクライアント | OpenAPI由来DTO、必須項目、エラー形式、レスポンス変換。 |
| UI Component | Blazorコンポーネント | 入力、バリデーション、空状態、ジョブ進捗表示。 |
| E2E | 主要業務フロー | キーワード探索、一括調査、CSV出力、管理操作。 |
| Load | 大量データ/ジョブ | 50,000語、順位URL50件、キュー滞留、P95。 |
| Security | 秘密情報/スコープ | APIキー非表示、CSRF、プロジェクト分離。 |
| Operational | 障害/復旧 | 再試行、通知、バックアップ復元、API仕様変更検知。 |

## 4. 受入基準トレース

| AC | 対象 | テスト種別 | 確認内容 |
| --- | --- | --- | --- |
| AC-001 | 主要API連携 | Contract/Integration | Phase 1で使用するラッコAPIの正常系と主要エラー系。 |
| AC-002 | キーワード探索 | E2E/Integration | 1シードから候補語統合、保存、フィルタ。 |
| AC-003 | 一括調査 | E2E/Integration | 1,000語以上のジョブ登録、完了監視、結果保存。 |
| AC-004 | 競合分析 | E2E/Integration | 競合抽出、獲得語/ページ、ギャップ表示。 |
| AC-005 | コンテンツ分析 | E2E/Integration | 集客コンテンツ、見出し、共起語、ブリーフ生成。 |
| AC-006 | 順位監視 | E2E/Integration | 順位チェック、順位分布、`alert_events`発火履歴、通知連携を確認。 |
| AC-007 | レポート | E2E/Integration | PDF/Excel生成、ダウンロード、共有URL、期限切れ/失効制御。 |
| AC-008 | スコープ | Integration/Security | 別projectIdのデータを混在表示しない。 |
| AC-009 | 運用 | Operational | 429/402/403/500/503の分岐、通知、監査。 |
| AC-010 | 管理系CRUD | Integration/UI | ワークスペース、プロジェクト、サイト、API認証情報、通知設定。 |
| AC-011 | ソフト削除 | Integration | DELETEが物理削除せずstatus更新する。 |
| AC-012 | Discord Webhook | Integration/Operational | Phase 1通知と送信元ジョブ/リソース、送信履歴、再送状態。 |
| AC-013 | CSVエクスポート | Integration/E2E | CSV出力、状態取得、ダウンロード、audit_logs記録。 |
| AC-014 | 監査ログ | Integration/UI | APIキー操作、外部API、CSV、ジョブ操作の検索/参照。 |
| AC-015 | AI支援 | E2E/Integration | prompt/response/reference/token_usage保存とレビュー状態。 |
| AC-016 | CSV/Excelインポート | E2E/Integration | 検証付き取込、エラー一覧、取込履歴。 |
| AC-017 | レポート監査 | Integration/Security | レポート形式、file_uri、共有URL発行/失効/ダウンロード監査。 |
| AC-018 | カニバリ検出 | E2E/Integration | 複数URL候補、根拠データ、推奨対応。 |
| AC-019 | 設計整合性 | Review | Phase 1 API/DB/ジョブ/監査の対応関係確認。 |
| AC-020 | 外部連携スタブ | Integration/Security | コネクタ設定、Secret非返却、接続テスト履歴。 |

## 5. MVP主要テストケース

| ID | シナリオ | 期待結果 |
| --- | --- | --- |
| T-MVP-001 | プロジェクト作成後に一覧取得 | activeプロジェクトが表示される。 |
| T-MVP-002 | プロジェクトDELETE | `status=archived`になり既定一覧から消える。 |
| T-MVP-003 | サイト作成/アーカイブ/復元 | `sites.status`が仕様通り変わる。 |
| T-MVP-004 | APIキー登録 | DBに`key_ref`のみ保存され、キー値はレスポンスに出ない。 |
| T-MVP-005 | クレジット消費監査 | `external_api_calls.consumed_credit`が全体/プロジェクト/APIキー/ジョブ別に集計できる。 |
| T-MVP-006 | Discord通知設定テスト | `notification_deliveries`に履歴が残る。 |
| T-MVP-007 | キーワード探索同期条件 | 軽量条件では200、重い条件では202とjobId/statusUrlを返す。 |
| T-MVP-008 | キーワード探索保存 | `keywords`に正規化、各結果テーブルに保存される。 |
| T-MVP-009 | 一括検索ボリューム登録 | `jobs`と`search_volume_jobs`が作成される。 |
| T-MVP-010 | 外部requestId保存 | `job_external_requests.external_request_id`が保存される。 |
| T-MVP-011 | 結果取得 | `search_volume_results`、`keyword_metrics`、`keyword_monthly_volumes`、`project_keyword_scores`が更新される。 |
| T-MVP-012 | CSV出力 | `data_exports`とStorageファイル、`audit_logs`が作成される。 |
| T-MVP-013 | CSV入力 | ブラウザ内でCSVをパースし、APIへは`keywords` JSON配列だけが送信される。 |
| T-MVP-014 | Idempotency-Key重複登録 | 同一スコープ・同一request hashでは既存ジョブが返り、二重登録されない。 |
| T-MVP-015 | waiting_externalジョブのキャンセル | ポーリング/結果取込が停止し、後続結果が業務テーブルへ保存されない。 |
| T-MVP-016 | 429モック | failed_retryableで再キューされる。 |
| T-MVP-017 | 402モック | failed_fatal、Discord通知、監査ログ記録の扱いになる。 |
| T-MVP-018 | 403モック | failed_fatal、APIキー/権限/契約スコープ確認誘導、Discord通知、監査ログ記録の扱いになる。 |
| T-MVP-019 | 500/503モック | 最大リトライまではfailed_retryableで再キューされ、上限超過時は失敗ジョブとして保持され通知される。 |
| T-MVP-020 | 別プロジェクト参照 | 404または403で拒否される。 |

## 6. 外部APIモック

| 対象 | モック内容 |
| --- | --- |
| suggest/related/other/question/ranking | 正常レスポンス、空データ、エラー配列あり、429、500、503。 |
| search-volume register | requestId返却、402、403、400。 |
| search-volume status | 未完了、完了、外部失敗。 |
| search-volume results | 1,000件以上、monthlySearchVolumeあり、limit境界。 |
| locations/languages | 正常、空、仕様変更で項目追加。 |
| Discord Webhook | 204成功、429、5xx、タイムアウト。 |

## 7. テストデータ

| データ | 内容 |
| --- | --- |
| workspace | 既定ワークスペース1件。 |
| projects | active 2件、archived 1件。 |
| sites | 自社サイト、競合候補、無効URL候補。 |
| keywords | 日本語、英語、前後空白、表記ゆれ、重複。 |
| search volume | 1件、1,000件、50,000件、重複混在。 |
| external responses | consumedCreditあり、errorsあり、requestIdあり。 |
| audit | APIキー操作、CSV出力、ジョブ再実行。 |

## 8. 性能・負荷テスト

| 対象 | 目標 |
| --- | --- |
| 通常API | P95 2.5秒以内。 |
| 一覧API | 10万行相当データでページングが安定する。 |
| 一括調査 | 50,000語を分割登録できる。 |
| 順位チェック | URL 50件指定時に分割登録できる。 |
| Worker | キュー滞留、リトライ、ポーリングが過負荷にならない。 |
| DB | 主要インデックスが利用され、全表スキャンを避ける。 |

## 9. セキュリティテスト

| 観点 | 確認内容 |
| --- | --- |
| 秘密情報 | APIキー/Webhook/AIキーがログ、レスポンス、画面に出ない。 |
| プロジェクト分離 | URL上のprojectIdと対象リソースのproject_id不一致を拒否する。 |
| CSV/レポート出力 | 出力条件、発行、ダウンロードが監査される。 |
| 共有URL | 期限切れ、失効済み、改ざんトークンを拒否する。 |
| AI | プロンプトから秘密情報を除去し、出力はレビュー前提で保存する。 |

## 10. 実行コマンド方針

現時点では実装コードがないため、具体的なテストコマンドはソリューション作成後に確定する。想定は以下。

```text
dotnet test
dotnet test --filter Category=Unit
dotnet test --filter Category=Integration
dotnet test --filter Category=Contract
```

リポジトリに`tests/`とCIが追加された時点で、本書に正式なコマンド、必要な環境変数、テストDB起動手順を追記する。

## 11. 完了条件

| フェーズ | 完了条件 |
| --- | --- |
| MVP | AC-001、AC-002、AC-003、AC-008からAC-014、AC-019の対象テストが通過し、主要障害系が確認済み。 |
| Phase 2 | 競合、コンテンツ、記事ブリーフ、順位監視のE2Eと契約テストが通過。 |
| Phase 3 | AI、リライト、カニバリ、レポート、CSV/Excelインポート、外部連携スタブのE2E/統合テストと監査が通過。 |
