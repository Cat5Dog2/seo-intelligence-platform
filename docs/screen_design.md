# 画面設計書

**ラッコキーワードAPIを中核にしたSEOインテリジェンス基盤**

_SEO Intelligence Platform / SEOインテリジェンス基盤_

| 項目 | 内容 |
| --- | --- |
| 文書ID | UI-RKSEO-001 |
| 作成日 | 2026-05-30 |
| 対象 | Blazor Web Appの画面、状態、操作、API対応 |
| 関連文書 | requirements.md / basic_design.md / api_design.md / db_design.md |

## 改訂履歴

| 版 | 日付 | 内容 | 作成/更新 |
| --- | --- | --- | --- |
| 1.0 | 2026-05-30 | 初版作成。画面一覧、共通UI、主要画面項目、API対応、状態を定義。 | ChatGPT |

## 1. 目的

本書は、Blazor Web Appで実装する画面の責務、入力項目、表示項目、操作、API対応、状態、バリデーションを定義する。画面IDはrequirements.mdの画面要件と一致させる。

## 2. 共通画面方針

| 方針 | 内容 |
| --- | --- |
| 単一利用者 | ログイン/ユーザー切替/権限別メニューは初期版では持たない。操作主体は`developer`固定。 |
| プロジェクトスコープ | ヘッダーで選択中プロジェクトを保持し、プロジェクト配下APIは必ず`projectId`付きで呼び出す。 |
| 非同期処理 | 外部APIを伴う重い操作はジョブ登録後に進捗表示へ遷移する。 |
| 監査対象操作 | APIキー、外部API実行、CSV/Excel出力、AI実行、共有URL操作は完了/失敗を画面上で確認できる。 |
| 状態表示 | 読込中、空状態、バリデーションエラー、ジョブ進行中、ジョブ失敗、再実行可能状態を共通化する。 |
| 秘密情報 | APIキーやWebhook URLの実値は再表示しない。保存後はマスク値または`key_ref`のみ表示する。 |

## 3. 共通レイアウト

```text
+---------------------------------------------------------------+
| Header: Project Switcher / Location / Language / Credit Status |
+----------------------+----------------------------------------+
| Side Navigation      | Main Content                            |
| - Dashboard          | Toolbar / Filters / Table / Detail      |
| - Keyword            | Job progress / Error / Export actions   |
| - Search Volume      |                                        |
| - Competitors        |                                        |
| - Content            |                                        |
| - Rank Tracking      |                                        |
| - Reports            |                                        |
| - Admin              |                                        |
+----------------------+----------------------------------------+
```

## 4. 共通コンポーネント

| コンポーネント | 用途 | 主な状態 |
| --- | --- | --- |
| ProjectSwitcher | プロジェクト選択、アーカイブ済み非表示 | loading / empty / active |
| LocationLanguageSelector | 地域/言語の既定値表示と変更 | loaded / syncRequired |
| CreditBadge | 日次/月次クレジット消費、402発生状況 | normal / warning / exhausted |
| JobProgressPanel | 非同期ジョブ進捗、再実行、キャンセル | queued / running / waiting_external / succeeded / failed_retryable / failed_fatal / canceled |
| DataTable | ソート、フィルタ、ページング、CSV出力 | loading / empty / error |
| StatusFilter | active/archived/disabled切替 | default active |
| AuditLink | 監査ログ詳細への導線 | available / unavailable |
| ErrorSummary | バリデーション/ジョブ/外部APIエラー表示 | validation / external / fatal |

## 5. 画面一覧

| 画面ID | 画面名 | Phase | 主API |
| --- | --- | --- | --- |
| S-001 | 起動/プロジェクト選択 | MVP | `GET /api/projects` |
| S-010 | ホームダッシュボード | MVP | `GET /api/projects/{projectId}/dashboard` |
| S-020 | キーワード探索 | MVP | `POST /api/projects/{projectId}/keyword-discovery/suggest` |
| S-030 | 一括検索ボリューム | MVP | `POST /api/projects/{projectId}/search-volume/jobs` |
| S-040 | トピッククラスター | Phase 2 | `GET /api/projects/{projectId}/clusters` |
| S-050 | 競合分析 | Phase 2 | `POST /api/projects/{projectId}/competitors/analyze` |
| S-060 | 獲得キーワード/ページ | Phase 2 | `GET /api/projects/{projectId}/influx-keywords`、`GET /api/projects/{projectId}/influx-pages` |
| S-070 | コンテンツ分析 | Phase 2 | `POST /api/projects/{projectId}/content/analyze` |
| S-080 | 記事ブリーフ | Phase 2/3 | `POST /api/projects/{projectId}/briefs/generate` |
| S-090 | リライト管理 | Phase 3 | `GET /api/projects/{projectId}/rewrite/tasks` |
| S-100 | 順位監視 | Phase 2 | `POST /api/projects/{projectId}/rank-check/jobs` |
| S-110 | EC/YouTube/画像企画 | 推奨 | `POST /api/projects/{projectId}/keyword-discovery/suggest` |
| S-120 | レポート | Phase 3 | `POST /api/projects/{projectId}/reports` |
| S-130 | AIアシスタント | Phase 3 | `POST /api/projects/{projectId}/ai/chat` |
| S-900 | 管理 | MVP（Phase 3拡張） | MVPは`/api/admin/*`、Phase 3で`/api/projects/{projectId}/connectors` |

## 6. 画面詳細

### 6.1 S-001 起動/プロジェクト選択

| 項目 | 内容 |
| --- | --- |
| 目的 | 既定ワークスペース内で作業プロジェクトを選択する。 |
| 入力 | プロジェクト検索、statusフィルタ、プロジェクト作成フォーム。 |
| 表示 | プロジェクト名、既定地域/言語、最終更新日、status。 |
| 操作 | 作成、編集、アーカイブ、復元、選択。 |
| API | `GET /api/projects`、`POST /api/projects`、`PUT /api/projects/{projectId}`、`DELETE /api/projects/{projectId}`、`POST /api/projects/{projectId}/restore` |
| バリデーション | name必須、同一ワークスペース内でname重複不可。 |

### 6.2 S-010 ホームダッシュボード

| 項目 | 内容 |
| --- | --- |
| 目的 | Phase別に主要KPI、ジョブ状況、クレジット、次アクションを俯瞰する。 |
| 表示 | キーワード探索件数、一括調査件数、機会スコア上位、クレジット消費、失敗ジョブ、通知失敗。 |
| 操作 | キーワード探索開始、一括調査開始、失敗ジョブ詳細、CSV出力。 |
| API | `GET /api/projects/{projectId}/dashboard`、`GET /api/jobs?project_id={projectId}`、`POST /api/projects/{projectId}/exports/csv`、`GET /api/projects/{projectId}/exports/{exportId}`、`GET /api/projects/{projectId}/exports/{exportId}/download` |
| 空状態 | プロジェクト作成直後はキーワード探索への導線を表示する。 |

### 6.3 S-020 キーワード探索

| 項目 | 内容 |
| --- | --- |
| 目的 | シード語から候補語、FAQ、LSI/PAA、同時ランクイン語を取得し統合する。 |
| 入力 | シードキーワード、検索ソース、limit、フィルタ、sortBy/orderBy、同期希望。 |
| 表示 | keyword、source、suggest_class、volume、difficulty、cpc、competition、first_seen_range、opportunity_score。 |
| 操作 | 調査開始、保存、フィルタ、検索ボリューム調査へ送る、CSV出力。クラスタ生成はPhase 2のS-040で扱う。 |
| API | `POST /api/projects/{projectId}/keyword-discovery/suggest`、`POST /api/projects/{projectId}/search-volume/jobs`、`POST /api/projects/{projectId}/exports/csv`、`GET /api/projects/{projectId}/exports/{exportId}/download` |
| 状態 | 軽量条件は同期表示、重い条件はジョブ進捗表示。 |
| バリデーション | keywordは1文字以上、limitはAPI設計書の範囲、推定クレジットを表示する。予算上限による登録停止は行わない。 |

### 6.4 S-030 一括検索ボリューム

| 項目 | 内容 |
| --- | --- |
| 目的 | 最大50,000語の検索ボリューム、SEO難易度、CPC、月別推移を非同期取得する。 |
| 入力 | キーワード貼付、CSVファイル選択、地域、言語、SEO難易度取得、集計期間。CSVはブラウザ内でパースし、APIへは`keywords` JSON配列として送る。 |
| 表示 | ジョブ進捗、検索ボリューム、difficulty、cpc、competition、月別推移、前年比。 |
| 操作 | ジョブ登録、キャンセル、再実行、結果フィルタ、CSV出力。 |
| API | `POST /api/projects/{projectId}/search-volume/jobs`、`GET /api/projects/{projectId}/search-volume/jobs/{jobId}`、`GET /api/projects/{projectId}/search-volume/jobs/{jobId}/results` |
| バリデーション | ブラウザ内でCSV/貼付テキストを`keywords`へ変換し、1から50,000件、重複除外、空行除外、地域/言語必須を検証する。MVPではCSVファイル本体をAPIへアップロードしない。 |

### 6.5 S-040 トピッククラスター

| 項目 | 内容 |
| --- | --- |
| 目的 | 同時ランクイン度、語彙類似度、FAQ、検索意図から記事単位のクラスタを作る。 |
| 表示 | クラスタ名、代表語、親子関係、keyword数、機会スコア、検索意図。 |
| 操作 | クラスタ生成、手動移動、代表語変更、ブリーフ作成。 |
| API | `GET /api/projects/{projectId}/clusters`、`GET /api/projects/{projectId}/clusters/{clusterId}`、`POST /api/projects/{projectId}/clusters/generate` |
| Phase | Phase 2必須。 |

### 6.6 S-050 競合分析

| 項目 | 内容 |
| --- | --- |
| 目的 | 自社ドメインから競合サイトを抽出し、重複率、流入、集客価値を比較する。 |
| 入力 | 対象サイト、競合候補、sortBy/orderBy。 |
| 表示 | domain、duplicate_rate、estimated_traffic、traffic_value、unique keyword count。 |
| 操作 | 競合抽出ジョブ登録、競合保存、獲得語/ページ分析へ遷移。 |
| API | `POST /api/projects/{projectId}/competitors/analyze`、`GET /api/projects/{projectId}/competitors` |

### 6.7 S-060 獲得キーワード/ページ

| 項目 | 内容 |
| --- | --- |
| 目的 | 自社/競合の獲得語と獲得ページを分析し、ギャップを抽出する。 |
| 入力 | target domain/url、match type、limit、filter。 |
| 表示 | keyword、rank、ranked_url、estimated_traffic、page_url、keyword_count、traffic_value。 |
| 操作 | ギャップ抽出、リライト候補化、CSV出力。 |
| API | `GET /api/projects/{projectId}/competitors`、`POST /api/projects/{projectId}/competitors/analyze`、`GET /api/projects/{projectId}/influx-keywords`、`GET /api/projects/{projectId}/influx-pages` |

### 6.8 S-070 コンテンツ分析

| 項目 | 内容 |
| --- | --- |
| 目的 | 集客コンテンツ、SERP見出し、共起語を取得し、ブリーフ材料を保存する。 |
| 入力 | キーワード、対象分析種別、limit、見出し取得オプション。 |
| 表示 | 上位URL、title、description、見出し構造、共起語、URL別詳細。 |
| 操作 | 分析ジョブ登録、ブリーフ生成、CSV出力。 |
| API | `POST /api/projects/{projectId}/content/analyze`、`GET /api/projects/{projectId}/content-analyses` |

### 6.9 S-080 記事ブリーフ

| 項目 | 内容 |
| --- | --- |
| 目的 | 検索意図、見出し、共起語、FAQ、競合URLをもとに記事構成書を作成する。 |
| 入力 | target keyword、cluster、競合URL、構成テンプレート、レビュー状態。 |
| 表示 | タイトル案、想定検索意図、H2/H3、必須語彙、FAQ、内部リンク候補、根拠データ。 |
| 操作 | 生成、編集、保存、版履歴、Markdown/CSV出力、Phase 3でAI再生成。 |
| API | `POST /api/projects/{projectId}/briefs/generate`、`GET /api/projects/{projectId}/briefs/{briefId}`、`PUT /api/projects/{projectId}/briefs/{briefId}`、`GET /api/projects/{projectId}/briefs/{briefId}/versions`、`POST /api/projects/{projectId}/briefs/{briefId}/export` |
| 状態 | draft / active / archived、review_statusはpending/reviewed/rejected等を画面表示する。 |

### 6.10 S-090 リライト管理

| 項目 | 内容 |
| --- | --- |
| 目的 | 順位、流入価値、不足見出し、共起語不足からリライト候補を管理する。 |
| 表示 | target_url、priority_score、position、estimated_traffic、reason、status、assignee_actor。 |
| 操作 | ステータス更新、優先度調整、詳細確認、カニバリ候補表示。 |
| API | `GET /api/projects/{projectId}/rewrite/tasks`、`GET /api/projects/{projectId}/rewrite/tasks/{taskId}`、`PUT /api/projects/{projectId}/rewrite/tasks/{taskId}` |
| Phase | Phase 3必須。 |

### 6.11 S-100 順位監視

| 項目 | 内容 |
| --- | --- |
| 目的 | キーワードとURL/ドメインの順位チェックを登録し、履歴とアラートを確認する。 |
| 入力 | keywords、targets、matchType、depth、withMetrics、deduplicate、アラート条件。 |
| 表示 | position、ranked_url、checked_at、順位分布、前回差分、アラート履歴。 |
| 操作 | 順位チェック登録、再実行、アラート作成/無効化、CSV出力。 |
| API | `POST /api/projects/{projectId}/rank-check/jobs`、`GET /api/projects/{projectId}/rank-check/jobs/{jobId}/results`、`GET /api/projects/{projectId}/rank-results`、`GET /api/projects/{projectId}/alerts`、`POST /api/projects/{projectId}/alerts`、`PUT /api/projects/{projectId}/alerts/{alertId}`、`DELETE /api/projects/{projectId}/alerts/{alertId}`、`POST /api/projects/{projectId}/alerts/{alertId}/enable`、`GET /api/projects/{projectId}/alert-events` |
| バリデーション | targetsは1から50件。各targetはURLまたはドメインとし、depthは30から100の許可値。 |

### 6.12 S-110 EC/YouTube/画像企画

| 項目 | 内容 |
| --- | --- |
| 目的 | Amazon、楽天、YouTube、Shopping、Imageのサジェストから企画語を抽出する。 |
| 入力 | シード語、検索ソース、用途タグ。 |
| 表示 | 商品名候補、動画タイトル候補、alt候補、タグ候補、季節性、商業性。 |
| API | `POST /api/projects/{projectId}/keyword-discovery/suggest` |
| Phase | 推奨バックログ。 |

### 6.13 S-120 レポート

| 項目 | 内容 |
| --- | --- |
| 目的 | 月次SEOレポート、競合ギャップ、順位レポートを出力する。 |
| 入力 | report_type、period、format、共有期限。 |
| 表示 | 生成状態、ファイル、共有URL状態、通知履歴、監査ログ。 |
| 操作 | レポート生成、PDF/Excel出力、共有URL発行/失効、ダウンロード。 |
| API | `POST /api/projects/{projectId}/reports`、`GET /api/projects/{projectId}/reports/{reportId}`、`GET /api/projects/{projectId}/reports/{reportId}/download`、`POST /api/projects/{projectId}/reports/{reportId}/share`、`DELETE /api/projects/{projectId}/reports/{reportId}/share` |
| Phase | Phase 3必須。 |

### 6.14 S-130 AIアシスタント

| 項目 | 内容 |
| --- | --- |
| 目的 | 自然言語から調査、要約、構成案、リライト指示、レポート要約を実行する。 |
| 入力 | message、参照範囲、許可ツール。 |
| 表示 | 応答、実行ツール、参照データ、生成物、token_usage、レビュー状態。 |
| 操作 | 送信、生成物保存、ブリーフ化、再生成、履歴参照。 |
| API | `POST /api/projects/{projectId}/ai/chat` |
| 注意 | APIキー、Webhook、秘密情報をプロンプトへ含めない。 |

### 6.15 S-900 管理

| 項目 | 内容 |
| --- | --- |
| 目的 | ワークスペース、APIキー、クレジット消費、通知、ジョブ、監査ログを管理する。Phase 3で外部連携スタブ設定を扱う。 |
| 表示 | MVPは設定値、API認証情報、クレジット消費、通知チャンネル、送信履歴、ジョブ一覧、監査ログ。Phase 3で外部連携スタブ設定と実行履歴を追加する。 |
| 操作 | MVPはAPIキー登録/無効化/ローテーション、通知テスト、ジョブ再実行、監査検索。Phase 3で外部連携スタブ接続テストを追加する。 |
| API | MVPは`/api/admin/workspace`、`/api/admin/api-credentials`、`/api/admin/notification-channels`、`/api/jobs`、`/api/admin/audit-logs`。Phase 3で`/api/projects/{projectId}/connectors`を追加する。 |
| セキュリティ | 秘密値は保存直後も再表示しない。 |

## 7. 共通バリデーション

| 対象 | ルール |
| --- | --- |
| キーワード | 1文字以上、前後空白除去、空行除外、Unicode正規化。 |
| URL | http/httpsのみ、ドメイン抽出可能であること。 |
| プロジェクト名 | 必須、同一ワークスペース内で重複不可。 |
| ファイル出力 | export_type、format、filter必須。 |
| ジョブ操作 | queued/waiting_externalはキャンセル可。runningはキャンセル不可。failed_retryableは再実行可、failed_fatal/canceledは再実行不可。waiting_externalのキャンセルは以後のポーリング/結果取込を停止する。 |

## 8. 実装優先度

| 優先 | 対象画面 | 理由 |
| --- | --- | --- |
| 1 | S-001、S-900管理の設定系 | プロジェクト、APIキー、通知設定、監査導線が外部API実行前提になる。 |
| 2 | S-020、S-030、S-010 | MVPの主要価値である調査、検索ボリューム、ダッシュボードを実現する。 |
| 3 | ジョブ進捗、監査、CSV出力 | 運用品質と受入基準に直結する。 |
| 4 | S-040以降 | Phase 2/3のSEO実務拡張として段階実装する。 |
