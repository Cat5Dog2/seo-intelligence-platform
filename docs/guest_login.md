# ゲストログイン

`/login`の「ゲストでログイン」から、アカウント不要でMockデモを開始する。環境変数やDB migrationの追加は不要。Web自体の起動条件（通常の管理者アカウントとIdentity DB）は既存どおりである。

## 操作できる範囲

- プロジェクトとサイトの作成・編集・アーカイブ・復元。
- キーワード探索と検索ボリューム調査。外部通信のない固定の模擬指標を返し、ジョブは即時完了する。
- 候補語と検索ボリュームのCSV出力・ダウンロード。検索ボリュームの出力は指定ジョブとキーワード検索条件で絞る。
- 競合、獲得語/ページ、コンテンツ分析、クラスター、記事ブリーフ、順位、リライトの固定サンプル閲覧。

デモは本番分析の結果や精度を再現するものではない。キーワード探索は入力シードから5件のGoogleサジェスト例を生成する。探索の詳細条件は模擬結果へ反映しない。その他の生成・更新、管理設定、通知送信、AI実行、共有URL発行、Excel/PDF出力はデモ対象外で、操作時に利用できる範囲を表示する。

## データの分離と期限

デモデータはWebのメモリにのみ置き、通常DB・Storage・Redis・Hangfire・Secret Store・外部APIを利用しない。通常の管理者操作や別のゲストセッションと共有しない。CSVもメモリから配信し、URLのプロジェクトとデモ内リソースの一致を確認する。

セッションは1時間で失効し、ログアウト、管理者ログインへの切り替え、Web再起動でも利用できなくなる。Cookieは非永続で延長しない。最大200セッションを保持し、上限に達すると古いセッションの一部を解放するため、期限前に再ログインが必要になる場合がある。複数Webインスタンスで利用する場合は同じインスタンスへ接続する必要がある。

セッションあたりプロジェクト10件、サイト30件、ジョブ50件。一括調査は100キーワードまで、各キーワードは100文字まで。大量データの挙動は通常のMock環境で確認する。

GuestにはAdminロールを付与しない。管理画面・パスワード変更はサーバー側で拒否する。ログインPOSTはCSRF検証とレート制限を通す。通常APIに到達するのはAdminのみで、Guestの未対応操作や期限切れを通常APIへ転送しない。Identityユーザーを作成しないため、ゲストCookieだけはIdentityのsecurity stamp検証の代わりにメモリセッションの存在・期限を検証する。

## 検証

```powershell
dotnet test tests/IntegrationTests/IntegrationTests.csproj --filter 'FullyQualifiedName~GuestDemoSessionTests|FullyQualifiedName~WebGuestLoginTests|FullyQualifiedName~WebAuthenticationTests|FullyQualifiedName~WebAccountAuthorizationTests|FullyQualifiedName~WebDownloadEndpointTests'
```

ブラウザ確認は、起動済みWebに対して次を実行する。管理者パスワードやAPIサービスキーは不要。

```powershell
$env:E2E_GUEST_BROWSER_ENABLED = 'true'
$env:E2E_WEB_URL = 'http://localhost:5295'
dotnet test tests/E2ETests/E2ETests.csproj --filter FullyQualifiedName~BrowserGuestLoginTests
```
