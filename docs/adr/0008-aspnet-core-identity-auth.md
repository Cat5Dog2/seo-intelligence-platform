# ADR 0008: 単一管理者ログインにASP.NET Core Identityを採用しAPIをサービスキーで保護する

| 項目 | 内容 |
| --- | --- |
| Status | Accepted |
| Date | 2026-08-02 |
| Decision Owner | developer |

## Context

`basic_design.md` と `api_design.md` は当初から「本番相当環境へ公開する場合は単一管理者ログイン + Cookie/BFF構成を必須にする」と予告していたが、実装は未着手だった。そのため `docker_deployment.md` と `operations_runbook.md` はVPS公開時にCaddy Basic認証やCloudflare Accessなどの外部認証ゲートを必須条件として記載していた。

本システムは同一VPS上で別リポジトリの `web-writing-tool` と同居する予定である。`web-writing-tool` はASP.NET Core Identityでログインを実装済みであり、運用者が1名であることから、両アプリの認証方式を揃えたほうが運用手順とトラブルシュートが単純になる。

一方で構成には差がある。`web-writing-tool` は単一Webプロジェクトで内部APIエンドポイントをin-processにホストするため、Cookie認証だけで全体を保護できる。本システムはWeb、API、Workerの3プロセス構成であり、共通Caddyが `/api/*` をAPIコンテナへ直接振り分けている。Web側にCookie認証を追加してもAPIは無防備なままである。

## Decision

認証を次の2層で構成する。

1. 利用者向け認証はASP.NET Core Identity + Cookie認証とし、`web-writing-tool` と同じ設定値を使う。パスワードポリシーは12文字以上かつ数字/大文字/小文字/記号必須、ロックアウトは5回失敗で15分とする。Identityテーブルは既存の `SeoIntelligenceDbContext` を `IdentityDbContext<ApplicationUser, IdentityRole, string>` 継承へ変更して同一DBへ配置し、テーブル名は本リポジトリのsnake_case規約に合わせて `identity_` 接頭辞で命名する。
2. Web からAPIへの呼び出しは `X-Service-Key` ヘッダーの共有シークレットで認証する。値はSecret Storeから取得し、定数時間比較で検証する。APIはfallback policyで全エンドポイントを要認証とし、`/healthz`、`/readyz`、`GET /api/report-shares/{token}` のみ匿名許可とする。

あわせて共通Caddyの公開面を縮小し、`/api/report-shares/*` 以外の `/api/*` は公開しない。

`web-writing-tool` とは独立した認証とする。DB、Data Protectionキー、Cookie名、ホスト名をすべて分離し、SSOは行わない。

## Consequences

| 区分 | 内容 |
| --- | --- |
| 利点 | アプリ内で認証が完結し、外部認証ゲートだけに依存する運用を解消できる。 |
| 利点 | `web-writing-tool` と同じIdentity設定のため、運用者が覚える手順が1つで済む。 |
| 利点 | APIがWeb以外からの呼び出しを拒否するため、Caddyの設定ミス時も無防備にならない。 |
| 利点 | ロール `Admin` / `User` を定義済みのため、`ISSUE-P4-001` のRBAC拡張が段階的に行える。 |
| 注意 | Web が管理者サインインのために `SeoIntelligence.Infrastructure` を参照し、DBへ直接接続する。業務データは従来どおりAPI経由であり、Webはこの用途に限りAPIと同じ合成ルートとして振る舞う。 |
| 注意 | Identityテーブル用のmigrationが1本増える。既存業務テーブルのスキーマは変更していない。 |
| 注意 | 同一ホスト名で `web-writing-tool` と併存させるとCookie名が衝突するため、別サブドメインでの運用が前提になる。 |
| 注意 | Caddy Basic認証などの外部ゲートは多層防御として維持することを推奨する。 |
| 注意 | 監査ログの操作主体は既存データ互換のため固定値 `developer` を維持しており、ログインユーザー名とは連動しない。 |
| 注意 | Adminが1人も存在せず`AdminSeed`も未設定の場合、Webホストは起動に失敗する。healthyだが誰もサインインできない状態を作らないための意図的なfail-closedであり、初回デプロイでのみ`ADMIN_SEED_EMAIL`と`ADMIN_SEED_PASSWORD`が必要になる。Compose側では必須化せず空値を渡せる形にし、初回サインイン後に資格情報を消しても以降の操作が失敗しないようにする。 |
| 注意 | 認可失敗の遷移先は認証状態で分ける。フルページ遷移はCookieの`AccessDeniedPath`、Blazor回路内の遷移は`RedirectToSignInOrForbidden`が担当し、どちらも未認証は`/login`、認証済み権限不足は`/forbidden`へ送る。 |
| 注意 | WebからAPIへの認証は共有サービスキーであり、APIは呼び出し元のロールを判別できない。したがって認可境界はWeb側のレイアウトとページで完結させる必要がある。`RequireAdmin`を持たない画面（`/account`、`/forbidden`、`/Error`）は業務コンポーネントを含まない専用レイアウトで表示する。Phase 4でロール別の権限を扱う場合は、サービスキーに加えて呼び出し元ロールをAPIへ伝える仕組みが必要になる。 |
| 注意 | ステータスコードページ（`/not-found`への再実行）はアカウント系エンドポイントへ適用しない。これらはAPI形式のステータスコードを返すため、再実行すると本来のコードがサインインリダイレクトへ置き換わってしまう。 |

## Alternatives Considered

| 案 | 却下理由 |
| --- | --- |
| パスワードハッシュをSecret Storeへ保存し、Identityを使わない | `web-writing-tool` と実装方式が揃わない。パスワード変更やロックアウトを自前実装することになる。 |
| 外部認証ゲート（Caddy Basic認証、Cloudflare Access）のみ | セッション、ログアウト、ログイン監査、API単体の保護が得られない。 |
| APIにもCookie認証を通す | Blazor ServerからAPIへの呼び出しはサーバー間通信でブラウザCookieが流れないため、転送実装が必要になる。 |
| JWT / OIDC / SSO | 単一利用者に対してIdP運用コストが見合わない。SSOは `ISSUE-P4-001` の範囲。 |

## Related Documents

- ../basic_design.md
- ../api_design.md
- ../db_design.md
- ../docker_deployment.md
- ../operations_runbook.md
- ../environment_setup.md
- ../test_plan.md
- 0007-secret-store-and-audit.md
