# ドメイン用語集

**SEOインテリジェンス基盤**

## 1. 目的

本書は実装時の命名、DTO、DBカラム、画面表示で用語がぶれないように、主要な業務用語と技術用語を定義する。
詳細な仕様は各正本文書を優先する。

## 2. 基本スコープ

| 用語 | 英語/コード候補 | 定義 |
| --- | --- | --- |
| ワークスペース | workspace | 初期版で1件だけ持つ管理単位。設定、API認証情報、通知、監査の親。 |
| プロジェクト | project | SEO調査対象をまとめる単位。業務データAPIの基本スコープ。 |
| サイト | site | プロジェクト配下の自社、競合、参考サイト。`type`で区別する。 |
| 対象地域 | location | 検索ボリュームや順位チェックの地域条件。ラッコAPIの地域マスタと対応する。 |
| 対象言語 | language | キーワード正規化、検索ボリューム取得、重複排除の言語条件。 |
| 操作主体 | actor | 初期版では固定値`developer`。将来の複数ユーザー化に備えて監査へ保存する。 |

## 3. キーワード調査

| 用語 | 英語/コード候補 | 定義 |
| --- | --- | --- |
| シードキーワード | seed keyword | 調査の起点語。`keyword_seeds.seed`に保存する。 |
| キーワード | keyword | 正規化済みのグローバル語彙。`keywords`に保存する。 |
| 正規化テキスト | normalized_text | 前後空白除去、Unicode正規化などを行った比較用キーワード文字列。 |
| テキストハッシュ | text_hash | 言語別重複排除に使うハッシュ。`UNIQUE(language, text_hash)`のキー。 |
| サジェスト | suggestion | 検索エンジン別の候補語。`keyword_suggestions`に保存する。 |
| 関連語 | related keyword | ラッコAPIの関連キーワード結果。`related_keywords`に保存する。 |
| LSI/PAA | lsi_paa | 潜在語、People Also Ask相当の項目。`lsi_paa_items`に保存する。 |
| FAQ/質問 | question | よくある質問候補。`questions`に保存する。 |
| 同時ランクイン語 | ranking keyword | 同じページが同時にランクインしている語。クラスタリングの根拠になる。 |
| 検索ソース | source/engine | Google、Bing、YouTube、Amazon、楽天、Shopping、Imageなどの取得元。 |
| サジェスト階層 | suggest_class | サジェスト取得時の分類や階層。フィルタ条件に使う。 |

## 4. 検索指標とスコア

| 用語 | 英語/コード候補 | 定義 |
| --- | --- | --- |
| 検索ボリューム | search_volume | 指定地域/言語での検索需要。`keyword_metrics.search_volume`に保存する。 |
| 月別検索ボリューム | monthly volume | `YYYY-MM`単位の検索需要履歴。`keyword_monthly_volumes`に保存する。 |
| SEO難易度 | seo_difficulty | 上位獲得の難しさを示す指標。外部APIレスポンス由来。 |
| CPC | cpc | 広告クリック単価。機会スコアの材料にする。 |
| 広告競合性 | competition | 広告競合の強さ。機会スコアの材料にする。 |
| 出現時期 | first_seen_range | 候補語や指標の初回確認時期レンジ。 |
| 機会スコア | opportunity_score | 狙うべきキーワードの優先度。プロジェクト別に`project_keyword_scores`へ保存する。 |
| スコア構成 | score_components_json | 機会スコア算出に使った入力値、係数、理由。再計算と説明用に保持する。 |
| 関連度 | relevance | シードやプロジェクト目的に対する近さ。スコアリングやクラスタリングに使う。 |
| トレンド係数 | trend factor | 月別推移から算出する増減傾向の係数。 |

## 5. 外部API/契約/監査

| 用語 | 英語/コード候補 | 定義 |
| --- | --- | --- |
| ラッコキーワードAPI | Rakko Keyword API | キーワード候補、検索ボリューム、競合、順位などを取得する外部API。 |
| 外部API呼び出し | external_api_call | 外部APIの1回のHTTP呼び出し監査。`external_api_calls`に保存する。 |
| 外部requestId | external_request_id | 外部APIが非同期登録時に返すrequestId。`job_external_requests`で追跡する。 |
| 契約スコープ | contract scope | API契約上のデータ利用範囲。キャッシュ共有可否の境界。 |
| 契約スコープキー | contract_scope_key | 契約スコープを識別する文字列。検索指標の再利用判定に使う。 |
| 契約スコープID | api_contract_scope_id | `api_contract_scopes`への参照。外部API呼び出しの契約根拠。 |
| API認証情報 | api_credential | 外部APIキーなどの認証情報メタデータ。秘密値はDBに保持しない。 |
| Secret参照 | key_ref/webhook_secret_ref/auth_ref | Secret Store上の秘密値参照名。実値ではない。 |
| 消費クレジット | consumed_credit | 外部APIレスポンスの`meta.consumedCredit`由来の消費量。 |
| リクエストハッシュ | request_hash | 冪等性、監査、重複検知に使うリクエスト内容のハッシュ。 |
| レスポンスハッシュ | response_hash | Storage保存済みレスポンスの同一性確認に使うハッシュ。 |
| source_call_id | source_call_id | 業務データがどの`external_api_calls.id`由来かを追跡する参照。 |
| correlation_id | correlation_id | API、ジョブ、外部呼び出し、監査ログを横断追跡するID。 |
| 監査ログ | audit_log | APIキー操作、外部API実行、出力、共有URL、AIなどの操作記録。 |

## 6. ジョブ

| 用語 | 英語/コード候補 | 定義 |
| --- | --- | --- |
| アプリジョブ | job | 業務上の非同期処理状態。`jobs`が正本。 |
| Hangfireジョブ | hangfire job | 実行基盤側のジョブ。業務監査の正本にはしない。 |
| 冪等キー | idempotency_key | 同一スコープで重複登録を抑止するキー。 |
| queued | queued | 登録直後または再試行待ち。キャンセル可能。 |
| running | running | Workerが処理中。キャンセル不可。 |
| waiting_external | waiting_external | 外部requestIdの完了待ち。キャンセル可能。 |
| succeeded | succeeded | 全処理成功。結果参照可能。 |
| failed_retryable | failed_retryable | 429/500/503やDB一時障害など、再試行可能な失敗。 |
| failed_fatal | failed_fatal | 400/402/403、入力不正、認証不備など、同一ジョブでは再試行しない失敗。 |
| canceled | canceled | 実行前または外部待機中にキャンセルされた状態。 |
| ポーリングジョブ | polling job | 外部API status確認を短いジョブとして再スケジュールする処理。 |

## 7. 出力/ファイル

| 用語 | 英語/コード候補 | 定義 |
| --- | --- | --- |
| CSV出力 | csv export | Phase 1対象データをCSVとしてStorageへ保存する処理。 |
| データ出力 | data_export | CSV/Excel出力履歴。MVPはCSVのみ。 |
| ダウンロードURL | downloadUrl | Storage上のファイルへ短時間だけアクセスできるURL。発行を監査する。 |
| file_uri | file_uri | Storage上の成果物URI。APIレスポンスでは必要に応じて短時間URLに変換する。 |
| CSV入力 | csv input | MVPではブラウザ内でパースし、APIにはファイル本体ではなく`keywords`配列を送る。 |
| インポート | data_import | Phase 3対象。Storageへアップロード済みファイルをWorkerが検証・取込する。 |

## 8. Phase 2/3用語

| 用語 | 英語/コード候補 | 定義 |
| --- | --- | --- |
| トピッククラスター | topic_cluster | 語彙類似度、検索意図、同時ランクインなどでまとめた親子トピック。 |
| 記事ブリーフ | article_brief | 検索意図、構成、必須語、FAQ、競合URLを含む制作指示書。 |
| 獲得キーワード | influx keyword | 自社/競合ドメインまたはURLが獲得しているキーワード。 |
| 獲得ページ | influx page | 推定流入や集客価値を持つページ。 |
| 共起語 | co-occurrence word | 上位ページ本文、タイトル、見出しに出現する関連語。 |
| 順位結果 | rank_result | キーワードとURL/ドメインの検索順位履歴。 |
| アラート | alert | 順位下落、圏外化、競合抜かれなどの通知条件。 |
| リライトタスク | rewrite_task | 既存ページ改善候補と優先度、理由、ステータス。 |
| カニバリ候補 | cannibalization_candidate | 同一キーワードで複数URLが競合している候補。 |
| レポート | report | PDF/Excel/共有URLとして出力する月次等の成果物。 |
| 成果物バージョン | artifact_version | ブリーフ、レポート、AI生成物の版履歴。 |
| AIセッション | ai_session | AIアシスタントの会話単位。 |
| AIメッセージ | ai_message | prompt、response、tool_calls、参照データ、token_usageを持つ会話明細。 |

## 9. 命名ルール

- DBテーブル/カラムは既存設計に合わせてsnake_caseを使う。
- C#の型名はPascalCase、プロパティはPascalCase、JSONレスポンスはcamelCaseを基本にする。
- `projectId`はURLパラメータを正本にし、業務APIのrequest bodyには含めない。
- `sourceCallId`は外部API由来データの追跡に使い、画面表示用の出典説明にも利用できる。
- 秘密値を示す名前には`SecretValue`を使い、保存後の永続カラムには`key_ref`や`secret_ref`を使う。
