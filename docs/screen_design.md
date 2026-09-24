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
| 単一利用者 | 通常データは単一管理者ログインで利用し、操作主体は`developer`固定。アカウント不要のゲストは分離されたMockデモを利用する。 |
| プロジェクトスコープ | ヘッダーで選択中プロジェクトを保持し、プロジェクト配下APIは必ず`projectId`付きで呼び出す。 |
| 非同期処理 | 外部APIを伴う重い操作はジョブ登録後に進捗表示へ遷移する。 |
| 監査対象操作 | APIキー、外部API実行、CSV/Excel出力、AI実行、共有URL操作は完了/失敗を画面上で確認できる。 |
| 状態表示 | 読込中、空状態、バリデーションエラー、ジョブ進行中、ジョブ失敗、再実行可能状態を共通化する。 |
| 秘密情報 | APIキーやWebhook URLの実値は再表示しない。保存後はマスク値または`key_ref`のみ表示する。 |

## 3. 共通レイアウト

```text
+-----------------------------------------------------------------------+
| Header: Project Switcher / Location / Language / Credit / Account      |
+----------------------+------------------------------------------------+
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

### UI/UXの改善（2026-09-24）

- ログイン画面は「SEO Intelligence」と「キーワード発見から記事改善まで」を表示し、ゲスト導線を「登録不要でデモを試す」とする。
- 主操作を紫のアクセント色に統一する。更新・出力は補助操作とし、調査の開始ボタンは入力欄の近くに一つ置く。
- ヘッダーの補助設定・API使用量・アカウント・ログアウトは「設定・アカウント」にまとめる。ゲストの利用範囲と初期化条件は設定内の折りたたみ案内へ集約し、ヘッダーにはゲスト・Mockのバッジを表示する。
- ゲストの競合、獲得語/ページ、コンテンツ分析、クラスター、ブリーフ、順位、リライト、AI画面には「サンプル閲覧専用」と利用範囲を表示する。非対応の生成・保存・再計算・出力操作を隠し、AI画面では記事ブリーフのサンプルへ案内する。ゲストには監査リンク・ジョブ再実行操作を表示しない。権限制御の正本は引き続きサーバー側である。
- キーワード探索はシード入力と開始を先に表示し、取得ソース・エンジン・件数・並べ替え・絞り込みを「詳細条件」にまとめる。ゲストには固定のGoogleサジェスト5件であることを説明する。候補の調査・CSV出力は結果欄に置く。
- 検索ボリュームはキーワード入力→「検索ボリュームを調べる」→進捗→結果の順とする。CSV入力・地域・言語・集計月数等は補助条件へまとめる。待機・実行・外部処理待ちの間は2秒ごとに状態を取得し、完了時に結果を自動取得する。実行中の二重登録を抑止する。失敗・キャンセル・通信エラーでは自動追跡を止め、状態再確認や再試行を案内する。
- 検索ボリュームの入力クリア、プロジェクト切替、画面破棄では追跡を止め、遅延応答を無視する。過去の調査は折りたたみ欄のジョブIDまたは`/search-volume?jobId=...`から開き、プロジェクトとジョブ種別を検証する。CSV受付後には出力一覧を更新して描画する。
- ダッシュボードの主要指標は探索数・検索ボリューム結果数・実行中・失敗の4つにする。次のアクションと直近5件の調査・出力を表示し、その他の指標は折りたたむ。空状態では開始導線を主表示にする。開発フェーズ名を画面見出しに使わず、重複した機能ナビゲーションを置かない。
- ブリーフ一覧はタイトル・対象語・版・更新日時・日本語レビュー状態をカード形式で表示する。選択すると全幅の本文へ切り替わり、一覧へ戻れる。本文は概要・検索意図・見出し構成・関連項目を表示し、JSONは詳細データ内に置く。ゲストの本文は読み取り専用とし、通常ログインの編集・保存・生成は維持する。`pending`の表示名は「確認待ち」とし、API値は変更しない。

### モバイル操作の改善（2026-09-24）

- 幅560px以下の検索ボリューム結果・キーワード候補・共通ジョブ一覧はカード表示にする。先頭3列を主表示、残りを各カードの「詳細」にまとめ、行の操作・ダウンロードはカード内に表示する。PCでは従来の表を使う。同じ操作要素を二重に描画しない。
- 検索ボリュームは完了結果の取得成功後に入力・進捗を折りたたむ。再展開して入力を編集できる。「結果を見る」は結果見出しへスクロールし、フォーカスも移す。自動ポーリングで画面位置は移動しない。失敗・キャンセル・結果取得エラーでは入力を開いたままにし、入力クリア・プロジェクト切替・再調査で完了状態を解除する。
- 結果の検索・並べ替え、ブリーフの検索・レビュー状態は折りたたみ欄にまとめる。ゲストのブリーフでも検索文字とレビュー状態による絞り込みを適用する。
- モバイルでは本文先頭の重複するプロジェクト名を省き、ヘッダーの選択欄を正本にする。更新ボタンは見出し横、閲覧専用の説明は展開できる1行の案内にする。
- 幅920px以下では画面下に「概要・探索・検索数・その他」を固定する。「その他」は全機能のメニューを開く。メニュー選択後は閉じ、本文と固定ナビが重ならない余白とセーフエリアを確保する。
- 設定とメニューにはネイティブのモーダルダイアログを使う。モバイルの設定は画面下から開くシート、PCでは中央のダイアログとする。閉じるボタン・Escape・背景クリックで閉じられ、背景スクロールを止める。
- 地域・言語はマスタに基づく選択欄にする。`jp`/`Japan`は「日本」、`ja`/`Japanese`は「日本語」と表示し、保存・API送信には元の値を使う。未知の保存値も保持し、マスタ取得失敗時は現在値と再取得操作を表示する。

### 操作と表示の共通ルール（2026-09-22）

- モバイルはiOS SafariとAndroid Chromeを対象とし、**375×667 CSS px**（iPhone SE相当）を必須確認サイズとする。Android向けには、より狭い360×640と393×851・412×915 CSS pxも確認する。縦スクロールで全操作へ到達でき、ページ全体の横スクロールを発生させない。多列の一覧は、案内を添えて表の領域内だけを横スクロール可能にする。
- 長い英数字のプロジェクト名・URL・エラー文も画面内で折り返す。幅560px以下では主要ボタンと入力欄の高さを44px以上、入力文字を16px以上とし、アカウント画面のヘッダーも縦に組み替える。詳細設定・メニュー展開時にも同じ幅の条件を満たす。
- 一覧の操作欄はボタン・監査リンクのラベルを1行で表示し、幅が不足する場合は操作要素単位で折り返す。

- サイドメニューを「調査・分析」「コンテンツ」「監視・レポート」「管理」に分ける。プロジェクトとダッシュボードは先頭に置く。幅920px以下では下部ナビの「その他」から開き、遷移時に閉じる。
- ヘッダーは選択中プロジェクトを主表示にする。地域・言語の変更は開閉式の補助設定にまとめ、保存中・成功・失敗を設定欄に表示する。Creditは「API使用量」と表記し、取得失敗を0件として表示しない。集計対象が直近200件であることを補足表示する。
- ホームとプロジェクト未選択時の業務画面は「プロジェクト作成 → キーワード探索 → 検索ボリューム調査」の開始ガイドを表示する。作成前は後続操作の前提条件を示し、選択後のホームと調査未実施のダッシュボードでは調査画面へのリンクを出す。
- ホーム・管理・主要な調査フォームのラベルを日本語で表示する。状態フィルタと共通ジョブ一覧も日本語化するが、APIへ渡す状態値や識別子は変更しない。
- プロジェクト・サイトの保存ボタンは作成・追加・更新を区別する。保存中はフォームを無効化し、連続送信を抑止する。成功は`role="status"`、失敗は`role="alert"`でフォーム付近に表示し、失敗時は入力値を残す。管理設定も同じ通知の区別を使う。
- サイトのドメインと正規URLには必須表示を付ける。正規URLはブラウザでも必須・URL形式・http/httpsスキームを検証する。
- プロジェクトと管理設定の基本項目は2列、幅560px以下は1列にする。KPI、ワークスペースのJSON設定、APIキー参照名、通知/連携の参照設定は「詳細設定」にまとめる。JSON入力は横幅と高さを確保する。秘密値を一覧や保存結果へ表示しない。

### コンポーネント一覧

| コンポーネント | 用途 | 主な状態 |
| --- | --- | --- |
| ProjectSwitcher | プロジェクト選択、アーカイブ済み非表示 | loading / empty / active |
| LocationLanguageSelector | 地域/言語の既定値表示と変更 | loaded / syncRequired |
| CreditBadge | 日次/月次クレジット消費、402発生状況 | normal / warning / exhausted |
| JobProgressPanel | 非同期ジョブ進捗、再実行、キャンセル、成果物ダウンロード | queued / running / waiting_external / succeeded / failed_retryable / failed_fatal / canceled |
| DataTable | ソート、フィルタ、ページング、CSV出力 | loading / empty / error |
| StatusFilter | active/archived/disabled切替 | default active |
| AuditLink | 監査ログ詳細への導線 | available / unavailable |
| ErrorSummary | バリデーション/ジョブ/外部APIエラー表示 | validation / external / fatal |
| GettingStarted | プロジェクト作成と初回調査への案内 | プロジェクト未選択 / 選択済み |
| ActionFeedback | 保存処理の結果を操作付近へ通知 | 保存中 / 成功 / 失敗 |

各画面の「CSV出力」は出力ジョブを登録する操作であり、生成されたファイルの取得は`JobProgressPanel`のダウンロード導線に集約する。ジョブが`succeeded`で成果物（`data_export`、`article_brief_export`、`report`）を持つ場合にリンクを表示する。リンク先はWebホストの`/downloads/projects/{projectId}/exports|reports/{id}`であり、ブラウザはAPIサービスキーを持たないためAPIへ直接リンクしない。

## 5. 画面一覧

| 画面ID | 画面名 | Phase | 主API |
| --- | --- | --- | --- |
| S-000 | サインイン | 横断 | `POST /login` |
| S-001 | 起動/プロジェクト選択 | MVP | `GET /api/projects` |
| S-010 | ホームダッシュボード | MVP（段階拡張） | `GET /api/projects/{projectId}/dashboard` |
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
| S-130 | AIアシスタント | Phase 3 | `POST /api/projects/{projectId}/ai/chat`、`GET /api/projects/{projectId}/ai/messages/{messageId}` |
| S-900 | 管理 | MVP（段階拡張） | MVPは`/api/admin/*`、Phase 2で`rank_alert`/`alert_events`/Phase 2ジョブ導線、Phase 3で`/api/projects/{projectId}/connectors` |

## 6. 画面詳細

### 6.0 S-000 サインイン

| 項目 | 内容 |
| --- | --- |
| 目的 | 管理者としてサインインする、または分離されたゲストデモを試す。 |
| ルート | `/login`。共通レイアウトを使わない専用レイアウト。 |
| 入力 | メールアドレス、パスワード、ログイン状態を保持。 |
| 表示 | 認証失敗、アカウント無効、ロックアウトのメッセージ。 |
| 操作 | 管理者サインインはSSRの`POST /login`、アカウント不要の「ゲストでログイン」は`POST /login/guest`へ送信する。両方でCSRFトークンを必須にする。 |
| バリデーション | メールアドレスとパスワードは必須。認証失敗理由はアカウントの存在を推測させない共通文言にする。 |
| 遷移 | 成功時は`returnUrl`（アプリ内パスのみ許可）または`/`へ。未サインインで保護ページを開くと`/login?ReturnUrl=...`へリダイレクトされる。 |

関連画面として`/account`（表示名確認とパスワード変更）、`/forbidden`（権限不足）を持つ。ヘッダー右端にサインイン中のユーザー名とログアウトボタンを表示する。

`/login`以外の全画面はサインインを必須とする。`/not-found`と`/Error`も例外にしない。`/not-found`は共通レイアウトを経由してプロジェクト名などの業務データを描画するため、匿名表示は不可とする。`/Error`は障害時に共通レイアウト経由でAPIを呼ばないよう専用レイアウトで表示するが、サインインは同様に必須とする。

業務画面とWebダウンロードは`RequireWorkspaceAccess`（AdminまたはMockのGuest）を要求する。管理画面は引き続き`RequireAdmin`を要求する。`/account`、`/forbidden`、`/Error`は認証済みであれば表示できる。ゲストのアカウント画面ではパスワード変更を表示せず、管理メニューも表示しない。

これらのセルフサービス画面は共通レイアウトを使わず、専用レイアウトで表示する。共通レイアウトの`ProjectSwitcher`、`CreditBadge`、`LocationLanguageSelector`は業務データを扱うため、`RequireWorkspaceAccess`を持たない画面に描画しない。Guestの場合はWeb内のデモデータだけを参照し、サービスキー付きAPIへは送信しない。

ゲストのヘッダーには「ゲスト・Mock」、設定内の折りたたみ案内には操作範囲と初期化条件を表示する。プロジェクト・サイト編集、キーワード探索、検索ボリューム調査、候補語/検索ボリュームのCSV出力を操作できる。競合、獲得語/ページ、コンテンツ分析、クラスター、記事ブリーフ、順位、リライトは固定サンプルを閲覧できる。その他の生成・更新、管理設定、通知、AI、共有URL発行はデモ対象外とし、操作前に利用範囲を日本語で案内して操作欄を隠す。詳細は`guest_login.md`を参照する。

ゲストのレポート画面は初期表示から利用範囲を案内し、キーワード探索・検索ボリューム調査へのリンクを表示する。生成・共有の操作欄は表示せず、通知履歴APIも呼び出さない。管理者は従来どおり生成・共有と通知履歴を利用できる。

認可失敗時の遷移先は認証状態で分ける。未認証は`/login`、認証済みで権限不足は`/forbidden`とする。フルページ遷移ではエンドポイントの認可がCookieの`AccessDeniedPath`で処理し、Blazor回路内の遷移では`RedirectToSignInOrForbidden`が同じ分岐を行う。

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
| 表示 | Phase 1はキーワード探索件数、一括調査件数、機会スコア上位、クレジット消費、失敗ジョブ、通知失敗。Phase 2では競合数、獲得語/ページ件数、コンテンツ分析件数、記事ブリーフ件数、順位チェック件数、順位分布、未解決アラート件数を追加する。 |
| 操作 | キーワード探索開始、一括調査開始、失敗ジョブ詳細、CSV出力。Phase 2では競合分析、コンテンツ分析、記事ブリーフ、順位監視への導線を追加する。 |
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

通常ログインの記事ブリーフ画面では追加取得に伴うクレジット消費を案内し、生成ジョブの進捗・再試行導線を表示する。ブリーフ詳細・編集中の内容・版履歴はプロジェクト切替時にクリアし、切替前の非同期応答は反映しない。

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
| API | `POST /api/projects/{projectId}/reports`、`GET /api/projects/{projectId}/reports/{reportId}`、`GET /api/projects/{projectId}/reports/{reportId}/download`、`POST /api/projects/{projectId}/reports/{reportId}/share`、`DELETE /api/projects/{projectId}/reports/{reportId}/share`。ファイル本体はWebホストの `/downloads/projects/{projectId}/reports/{reportId}` 経由で取得する |
| Phase | Phase 3必須。 |

### 6.14 S-130 AIアシスタント

| 項目 | 内容 |
| --- | --- |
| 目的 | 自然言語から調査、要約、構成案、リライト指示、レポート要約を実行する。 |
| 入力 | message、参照範囲、許可ツール。 |
| 表示 | 応答、実行ツール、参照データ、生成物、token_usage、レビュー状態。 |
| 操作 | 送信、生成物保存、ブリーフ化、再生成、履歴参照。 |
| API | `POST /api/projects/{projectId}/ai/chat`、`GET /api/projects/{projectId}/ai/messages/{messageId}` |
| 注意 | APIキー、Webhook、秘密情報をプロンプトへ含めない。 |

送信直後は受付内容とジョブ状態を表示する。「応答を更新」またはAIジョブの「更新」で保存済み応答を読み直し、完了後の本文とtoken_usageを反映する。ジョブ行の「応答を表示」から過去の応答も取得できる。プロジェクトを切り替えると応答とconversation_idをクリアし、切替前の通信結果は表示しない。既定の許可ツールは`keyword-discovery, brief-generation, rewrite-analysis, report-summary`。

キーワード探索と検索ボリューム画面には選択中プロジェクトの「CSV出力ジョブ」を表示する。「更新」で完了を確認し、成功した`csv_export`ジョブの「ダウンロード」からWebホストの`/downloads/projects/{projectId}/exports/{exportId}`を開く。処理中・失敗・キャンセルされたジョブにはダウンロードを表示しない。

キーワード探索の「状態更新」はジョブが完了したら`GET /api/projects/{projectId}/keyword-discovery/jobs/{jobId}/results`で候補語とソース別状態を取得する。外部APIを再実行せず、保存済み候補を検索ボリューム調査へ送れるようにする。プロジェクト切替時は候補と探索ジョブをクリアする。

探索が失敗した場合も保存済みの部分結果とソース別エラーを表示する。再試行可能な場合は「失敗したソースを再試行」を表示する。検索ボリューム画面ではプロジェクト切替時に入力・ジョブ・結果・出力状態をクリアし、切替前の応答を破棄する。ジョブ読込時はプロジェクトとジョブ種別を照合する。

### 6.15 S-900 管理

| 項目 | 内容 |
| --- | --- |
| 目的 | ワークスペース、APIキー、クレジット消費、通知、ジョブ、監査ログを管理する。Phase 2で順位アラート運用導線、Phase 3で外部連携スタブ設定を扱う。 |
| 表示 | MVPは設定値、API認証情報、クレジット消費、通知チャンネル、送信履歴、ジョブ一覧、監査ログ。Phase 2では`rank_alert`通知、`alert_events`、Phase 2ジョブ種別（競合分析、コンテンツ分析、ブリーフ生成、クラスタ生成、順位チェック、順位アラート評価）を検索・参照できる。Phase 3で外部連携スタブ設定と実行履歴を追加する。 |
| 操作 | MVPはAPIキー登録/無効化/ローテーション、通知テスト、ジョブ再実行、監査検索。Phase 2では順位アラート通知履歴の確認、`alert_events`から送信履歴への遷移、Phase 2ジョブ種別での絞り込み、関連リソースID/Correlation IDによる監査検索導線を追加する。Phase 3で外部連携スタブ接続テストを追加する。 |
| API | MVPは`/api/admin/workspace`、`/api/admin/api-credentials`、`/api/admin/notification-channels`、`/api/jobs`、`/api/admin/audit-logs`。Phase 2では`/api/projects/{projectId}/alerts`、`/api/projects/{projectId}/alert-events`、`/api/admin/notification-deliveries`、`/api/jobs`のPhase 2 job_type検索を管理導線で利用する。Phase 3で`/api/projects/{projectId}/connectors`を追加する。 |
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
