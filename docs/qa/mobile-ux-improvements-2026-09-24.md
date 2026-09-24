# モバイルUX改善と検証（2026-09-24）

## 対象

ユーザーが指定した5点（結果カード、結果への移動、上部とフィルターの省スペース化、下部ナビ、地域・言語設定）を修正した。前回のUI/UX改修を含む作業ツリー上で実装し、ローカルの実際のBlazor Web画面をEdgeから操作した。APIは隔離したゲストデモのMockを利用した。

## 変更内容

1. 幅560px以下の検索ボリューム・キーワード候補・共通ジョブ一覧をカード表示に変更。先頭3列を主表示、補助情報は「詳細」へ格納。ジョブ操作とダウンロードはカード内に置き、操作要素の重複描画を避けた。
2. 調査完了時は入力・進捗を折りたたみ、「結果を見る」で結果見出しへスクロールとフォーカスを移動。再展開、入力クリア、再調査が可能。失敗・キャンセル・結果取得失敗の導線を維持した。
3. モバイルで重複するプロジェクト名を省き、更新ボタンを見出し横へ配置。ゲストの共通案内は設定内、閲覧専用の説明は折りたたみへ集約。検索結果とブリーフ一覧のフィルターを折りたたんだ。
4. 幅920px以下に「概要・探索・検索数・その他」の下部ナビを固定。「その他」から全機能と設定へ移動できる。本文の下余白とセーフエリアに対応。
5. 設定をネイティブdialogに変更し、モバイルでは下部シート、PCでは中央に表示。Escape・閉じるボタン・背景クリックで閉じる。地域・言語はマスタの選択欄を共通化し、日本・日本語などの表示名とAPI値を分離した。取得失敗時も現在値を残し、再取得できる。

操作検証で、ゲストのブリーフ一覧がレビュー状態フィルターを無視する既存不具合を再現した。確認済み・差し戻しで0件になる回帰テストを先に失敗させ、サンプルデータにも絞り込みを適用した。

## 今回変更したファイル

- 共通画面: `Components/App.razor`、`Components/Layout/MainLayout.razor`/`.razor.css`、新規`MainNavigation.razor`/`.razor.css`。
- 共通部品: `Components/Common/DataTable.razor`、`JobProgressPanel.razor`、`GuestReadOnlyNotice.razor`、`LocationLanguageSelector.razor`、新規`LocaleFields.razor`。
- 主なページ: `Components/Pages/SearchVolume.razor`、`ArticleBriefs.razor`、`Keywords.razor`。
- 重複プロジェクト名のクラス付与: `AiAssistant.razor`、`Competitors.razor`、`ContentAnalysis.razor`、`Dashboard.razor`、`Home.razor`、`Influx.razor`、`RankMonitoring.razor`、`Reports.razor`、`RewriteManagement.razor`、`TopicClusters.razor`。
- 表示・ブラウザ操作: `Services/UiText.cs`、`Services/GuestDemoSession.Samples.cs`、`wwwroot/app.css`、`wwwroot/seo-intelligence.js`。
- 上記アプリのパスはすべて`src/SeoIntelligence.Web/`配下。
- テスト: `tests/E2ETests/BlazorUsabilityTests.cs`、`BrowserQaRegressionTests.cs`、`BrowserMobileLayoutTests.cs`、`BrowserGuestLoginTests.cs`、`BrowserSmokeFlow.cs`、`tests/IntegrationTests/GuestDemoSessionTests.cs`。
- 文書: `docs/screen_design.md`、`docs/test_plan.md`、`docs/guest_login.md`、本書。

前回の改修および既存の`docs/qa/guest-browser-2026-09-23.md`の削除状態はそのまま保持した。

## 自動検証

```powershell
dotnet test tests/E2ETests --no-restore --filter 'Category!=BrowserE2E' --verbosity quiet
dotnet test tests/IntegrationTests --no-build --no-restore --filter 'FullyQualifiedName~WebGuestLoginTests|FullyQualifiedName~GuestDemoSessionTests|FullyQualifiedName~WebAuthenticationTests|FullyQualifiedName~WebAccountAuthorizationTests|FullyQualifiedName~WebDownloadEndpointTests' --verbosity quiet
dotnet build SeoIntelligence.sln --no-restore --verbosity quiet
git diff --check
```

- UI等の関連テスト83件成功、認証・ゲスト・ダウンロード等の関連テスト93件成功、合計176件成功。
- 全体ビルドは警告0・エラー0。
- 差分の空白エラーなし。GitのLF/CRLF正規化予告のみ。
- ゲストのレビュー状態フィルター回帰テストは5ケース中2ケースの失敗を確認してから修正し、5ケースすべて成功した。

## ブラウザ検証

- 13画面（プロジェクト、ダッシュボード、キーワード、検索ボリューム、競合、獲得語/ページ、コンテンツ分析、クラスター、ブリーフ、リライト、AI、順位、レポート）を360×640、375×667、390×844、1280×900で確認。計52パターンでページ全体の横はみ出しなし。
- 小画面の表示中ボタン・入力欄は44px以上、入力文字は16px以上。チェックボックス・ラジオの素の入力要素はサイズ集計から除外した。
- 検索結果は360/375/390pxでカード、1280pxでは表。390pxの表示領域317pxにカードが収まり、従来の760px表の横スクロールを解消した。
- 調査の入力、重複除去、地域・言語の選択、結果の自動取得、入力の自動折りたたみ、「結果を見る」のスクロール・フォーカス移動、詳細の開閉、再編集、入力クリア、空入力エラーを確認。
- CSV出力後、共通ジョブカードにダウンロードリンクが表示された。今回ブラウザからのファイル保存自体は実行していない。ダウンロードの認可・内容は既存の統合テストで検証。
- 記事ブリーフのフィルター開閉、確認済みでの空結果、解除を確認。
- 下部ナビから全機能メニューを開き、記事ブリーフへの遷移時に閉じることを確認。
- 設定シートは360×640でも画面内。閉じるボタン・Escape・背景クリックで閉じ、元の操作ボタンへフォーカスが戻った。開いている間は背景スクロールが止まった。
- 844×390の横向き設定画面ではシートが上端16px〜下端390pxに収まり、シート内をスクロールできた。

390px幅の比較（同じローカルUIをDOMで測定）:

| 対象 | 改善前 | 改善後 |
| --- | ---: | ---: |
| 検索ボリューム結果の先頭（ページ上端から） | 約1,410px | 約563px |
| ブリーフの最初の記事（ページ上端から） | 約716px | 約367px |

## 未実行・範囲外

- iPhone/Safari、Android実機のソフトキーボード、物理的な片手操作、実機回転、ノッチ・セーフエリアの実測。
- 実API、実クレジット消費、低速/切断回線での検証、全テストスイート。
- standalone BrowserE2Eランナーは起動せず、上記のブラウザ操作は既存の接続済みEdgeで行った。CI用の既存ブラウザテストは新UIに合わせて更新し、コンパイルを確認した。
- 本番反映、コミット、push、PR作成は実施していない。
