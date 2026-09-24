# UI/UX改善・検証記録（2026-09-24）

公開環境のゲストレビューで挙げた改善をローカルソースへ反映した。本番反映・コミット・push・PR作成は行っていない。

## 変更内容

| 観点 | 反映内容 |
| --- | --- |
| ゲスト | 閲覧専用の案内と非対応操作の非表示。通常ログインの生成・編集を維持。 |
| 用語・ブリーフ本文 | 日本語ラベルとレビュー状態。概要・検索意図・見出しを表示し、JSONは詳細内へ。 |
| モバイル | 設定・アカウントをまとめ、ゲスト案内を折りたたみ。390×844の検索ボリューム入力位置は約936pxから約394pxに短縮し、開始ボタンは約584px。 |
| 調査フロー | 入力付近の開始ボタンを一つにし、進捗追跡・結果取得を自動化。二重登録と遅延応答の混入を防止。過去の調査へ直接戻れる。 |
| 探索条件 | ソース・エンジン・件数・並べ替え・フィルタを詳細条件へ。取得元ごとの状態も折りたたむ。 |
| ダッシュボード | 主指標4つ、次の操作、直近5件。その他の指標は折りたたみ。空状態でも実行中・失敗があれば表示する。 |
| 一覧・詳細 | ブリーフのカード一覧と全幅の本文。状態の途中改行を防止。 |
| 色・操作 | 主操作の紫色を統一。更新・出力を補助操作として配置。 |
| ログイン | ブランド・説明を追加し、全幅の「登録不要でデモを試す」へ変更。 |

ブラウザで見つかったCSV受付後の再描画不足、ブリーフ版番号の文字列表示、ゲストの直近ジョブが古い順になる問題も修正した。

## 変更ファイル

以下はリポジトリ相対パス。同じ行のファイルは先頭に示したディレクトリに属する。

- `src/SeoIntelligence.Web/Components/Common/`: `WorkspaceComponentBase.cs`, `GuestReadOnlyNotice.razor`, `BriefContentView.razor`, `AuditLink.razor`, `JobProgressPanel.razor`
- `src/SeoIntelligence.Web/Components/Layout/`: `MainLayout.razor`, `MainLayout.razor.css`
- `src/SeoIntelligence.Web/Components/Pages/`: `Login.razor`, `Dashboard.razor`, `Keywords.razor`, `SearchVolume.razor`, `ArticleBriefs.razor`, `Competitors.razor`, `Influx.razor`, `ContentAnalysis.razor`, `TopicClusters.razor`, `RankMonitoring.razor`, `RewriteManagement.razor`, `AiAssistant.razor`
- `src/SeoIntelligence.Web/Services/`: `UiText.cs`, `GuestDemoSession.cs`
- `src/SeoIntelligence.Web/wwwroot/app.css`
- `tests/E2ETests/`: `BrowserQaRegressionTests.cs`, `BlazorUsabilityTests.cs`, `BrowserGuestLoginTests.cs`, `BrowserSmokeFlow.cs`
- `tests/IntegrationTests/`: `WebGuestLoginTests.cs`, `GuestDemoSessionTests.cs`
- `docs/`: `screen_design.md`, `guest_login.md`, `test_plan.md`、本ファイル

着手前から存在した`docs/qa/guest-browser-2026-09-23.md`の削除差分には触れていない。

## 自動検証

```powershell
dotnet build SeoIntelligence.sln --no-restore --verbosity quiet
dotnet test tests/E2ETests --no-restore --filter 'Category!=BrowserE2E' --verbosity quiet
dotnet test tests/IntegrationTests --no-build --no-restore --filter 'FullyQualifiedName~WebGuestLoginTests|FullyQualifiedName~GuestDemoSessionTests|FullyQualifiedName~WebAuthenticationTests|FullyQualifiedName~WebAccountAuthorizationTests|FullyQualifiedName~WebDownloadEndpointTests' --verbosity quiet
git diff --check
```

- ビルド: 警告0・エラー0。
- ブラウザ起動を伴わないE2ETests内の関連テスト: **69件成功**。
- ゲスト・認証・権限・ダウンロード関連テスト: **88件成功**。
- 自動追跡の待機/実行/外部待ち→完了、失敗・キャンセル、重複登録抑止、プロジェクト切替・クリア・破棄後の遅延応答無視を検証。
- ブリーフのデモ/実生成形式、日本語表示、欠損値、ゲスト/通常ログインの表示差を検証。
- CSVリンクの実描画、ジョブの順序とページング、結果のない実行中/失敗ダッシュボードを検証。
- 不具合の回帰テストは修正前に失敗することを確認した。

## ブラウザ確認

Edgeでメモリ内の認証・APIフィクスチャを使う`http://localhost:5277`を操作した。外部APIと本番データは利用していない。

- 業務13画面×360/390/768/1280pxの**52パターン**を確認。ページ全体の横はみ出し、表外の操作欄の画面外配置、初期表示エラーは0件。
- 360/390pxで入力文字16px以上、チェックボックス等を除く入力・ボタンの高さ44px以上を確認。
- ゲストログイン、モバイルメニュー開閉と遷移後の閉鎖、設定メニュー、JSON詳細のキーボード開閉、一覧への復帰を確認。
- 空入力エラー、重複キーワード除外、1回の開始操作で結果表示、CSV受付後のダウンロードリンク出現を確認。
- ダッシュボードの空状態/データあり表示、履歴の「結果を開く」からの自動表示を確認。
- ブリーフの概要・検索意図・見出し、版番号、レビュー状態、日本語JSON、ゲストの読み取り専用属性を確認。
- 設定・調査条件の展開状態を360pxで確認。検証用ビューポートを元に戻した。

## 未実行・範囲

既存のPlaywright BrowserE2Eは導線・セレクターを更新し、今回はEdgeの実操作で確認した。ブラウザでのCSV実体の保存とファイル選択ダイアログは再実行していない（ダウンロード経路・認証・内容は関連統合テストで検証）。通常ログインの操作維持はWebホストの統合テストで確認した。実機Safari/Android、実外部API、DBを含む全テストスイート、負荷試験はUI改修の範囲外として実行していない。
