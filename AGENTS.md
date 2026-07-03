# AGENTS.md

## 適用範囲

- このファイルはリポジトリ全体に適用する。
- 本プロジェクトは、ラッコキーワードAPIを中核にしたSEOインテリジェンス基盤である。
- 実装判断は、設計ドキュメントと `todo.md` のIssue単位を基準にする。

## コミュニケーション

- ユーザーへの回答は、明示指定がない限り日本語で行う。
- 変更後は、変更ファイル、実行した検証コマンド、未実行テストと理由を報告する。
- 曖昧な点は、合理的な仮定を置いて短く明記する。仕様や安全性に影響する場合は確認する。

## 正本ドキュメント

- 要件、スコープ、受入基準: `docs/requirements.md`
- 方式、アーキテクチャ、レイヤ構成: `docs/basic_design.md`
- 内部API、レスポンス、エラー、入力制約: `docs/api_design.md`
- DBテーブル、制約、インデックス、ステータス: `docs/db_design.md`
- 画面、UI状態、API対応: `docs/screen_design.md`
- 非同期ジョブ、状態遷移、リトライ: `docs/job_design.md`
- テスト方針、受入テスト、Mock方針: `docs/test_plan.md`
- 外部API、Secret、クレジット、キャッシュ: `docs/external_api_design.md`
- 運用、障害対応、スモークテスト: `docs/operations_runbook.md`
- 環境構築、Secret、起動確認: `docs/environment_setup.md`
- 技術選定: `docs/adr/*`
- 実装順とIssue粒度: `todo.md`

同じ内容が複数文書にある場合は、スコープは `requirements.md`、API契約は `api_design.md`、DB定義は `db_design.md`、ジョブ状態は `job_design.md`、画面仕様は `screen_design.md` を優先する。

## 開発ワークフロー

- 編集前に `git status` と関連ファイル、関連ドキュメントを確認する。
- 原則として `todo.md` の1 Issueだけを対象にし、別Issueの先行実装や大規模リファクタを混ぜない。
- 既存の設計、命名、レイヤ構成を優先し、小さく焦点を絞った変更にする。
- 仕様変更や実装後に確定したコマンドがある場合は、該当する `docs/` を更新する。
- 大きな技術選定や既存ADRと異なる判断をする場合は、ADR追加または更新を検討する。
- コミット、push、PR作成は、ユーザーが明示的に依頼した場合だけ行う。
- ファイル削除、履歴リセット、force push、Secretや `.env` の変更、DB migration方針の変更は、明示的な依頼なしに行わない。
- 本番依存の追加は、事前にユーザー確認を取る。

## 技術スタック

- Runtime: .NET 10 LTS
- Backend: ASP.NET Core Web API / Minimal APIs
- Frontend: Blazor Web App
- Worker: .NET Worker Service + Hangfire + PostgreSQL storage
- DB: PostgreSQL + EF Core + JSONB
- Cache/Coordination: Redis
- Observability: OpenTelemetry、構造化ログ、メトリクス
- External: ラッコキーワードAPI、Discord Webhook、Phase 3以降でAI/GSC/GA4/CMS/BIスタブ

想定構成:

```text
SeoIntelligence.sln
src/
  SeoIntelligence.Web/
  SeoIntelligence.Api/
  SeoIntelligence.Application/
  SeoIntelligence.Domain/
  SeoIntelligence.Infrastructure/
  SeoIntelligence.Worker/
  SeoIntelligence.Contracts/
tests/
  UnitTests/
  IntegrationTests/
  ContractTests/
  E2ETests/
docs/
```

## アーキテクチャ規約

- Domainは他プロジェクトへ依存しない。
- ApplicationはDomainとContractsへ依存し、Infrastructureへ依存しない。
- InfrastructureはApplicationのインターフェースを実装し、EF Core、外部APIクライアント、AI、Discord、Storage、Secret Storeを配置する。
- Api、Web、WorkerはApplicationを呼び出す。
- 外部API DTOは `docs/rakko-keyword-api-docs.json` から生成し、Infrastructure層に閉じ込める。生成コードは直接編集しない。
- Application層では外部DTOを業務DTOへ変換し、外部仕様を内部APIや画面契約へ漏らさない。
- DI、Options pattern、構造化ログ、Correlation IDを基本とする。

## API規約

- 内部APIは共通レスポンスエンベロープと共通エラー形式を使う。
- `X-Correlation-Id` を受け取り、未指定時はサーバーで生成し、ログ、DB、レスポンスmetaへ追跡できる形で残す。
- 業務データAPIは `/api/projects/{projectId}/...` を正本とし、body内の `projectId` は受け付けない。
- URL上の `projectId` とDB上の `project_id` の一致を必ず検証する。不一致は原則404、管理上明示したい場合のみ403を使う。
- DELETE系APIは物理削除せず、`status=archived` または `status=disabled` へ状態変更する。復元/再有効化APIで `active` に戻す。
- 重い外部API連携は非同期ジョブ化し、202 Accepted、`jobId`、状態URLを返す。
- ヘルスチェックは `/healthz`、Readinessは `/readyz`、OpenAPIは `/openapi/v1.json` を想定する。

## DBとデータ管理

- 業務データは原則 `workspace_id` または `project_id` を持たせる。グローバル共有はキーワード正規化マスタや地域/言語マスタなどに限定する。
- 可変な分析条件、外部レスポンス要約、AI参照データはJSONBで保持する。ただし検索や一意制約に使う業務キーは列として持つ。
- APIキー、Webhook URL、OAuthトークン、AIキーなどの実値はDBに保存しない。DBには `key_ref`、`webhook_secret_ref`、`auth_ref` などの参照のみ保存する。
- 外部API request/response本体、ローデータ、出力ファイルはStorageに置き、DBにはURI、ハッシュ、ステータス、消費クレジット、契約スコープを保存する。
- `external_api_calls` を外部API呼び出し監査の正本とし、`consumedCredit` を保存する。
- `audit_logs` にはAPIキー操作、外部API実行、CSV/Excel/レポート出力、AI実行、ジョブ操作、ダウンロードURL発行を記録する。初期版の操作主体は固定値 `developer`。
- EF Core migrationsを使う。Hangfire内部テーブルは業務監査や画面表示の正本にしない。
- Redisはキャッシュ、分散ロック、レート制御、一時状態管理に使い、永続データや監査の正本にしない。

## ジョブ規約

- 業務上のジョブ状態の正本は `jobs` テーブルとする。
- `Idempotency-Key` と `request_hash` により、同一スコープ、同一条件の重複登録を抑止する。
- 主な状態は `queued`、`running`、`waiting_external`、`succeeded`、`failed_retryable`、`failed_fatal`、`canceled`。
- 429、500、503、DB一時障害はretryableとして扱う。400、402、403は再試行せずfatalにする。
- 402はクレジット不足としてDiscord通知し、403はAPIキー/権限/契約スコープ確認へ誘導する。
- `waiting_external` のキャンセルでは外部requestId自体は取り消さず、内部ポーリングと結果取込を停止する。
- 再試行時は既存の `job_external_requests` と `external_api_calls` を参照し、外部APIの二重登録や結果の二重取込を避ける。

## 外部APIとSecret

- 通常開発とCIでは外部API Mockを既定にする。Real利用は明示的に切り替えた場合だけ行い、契約スコープとクレジット消費を確認する。
- ラッコキーワードAPIは `X-API-Key` をSecret Storeから取得する。ログ、レスポンス、監査ログ、テスト出力へ実値を出さない。
- 検索指標キャッシュは `api_contract_scopes.scope_key`、地域、言語、取得日時を考慮し、契約スコープが一致する場合だけ再利用する。
- Discord Webhook URLはSecret Storeに保存し、DBには参照名のみ保存する。通知履歴と再送状態は `notification_deliveries` に残す。
- AI機能ではプロンプトから秘密情報や個人情報を除去し、出力は人間レビュー前提の下書きとして扱う。

## フロントエンド規約

- Blazor Web Appで実装する。
- ヘッダーで選択中プロジェクトを保持し、プロジェクト配下APIは必ず `projectId` 付きで呼び出す。
- loading、empty、validation error、job running、job failed、retryableを共通状態として扱う。
- 一覧はソート、フィルタ、ページングを基本にし、大量テーブルでは仮想化や差分更新を検討する。
- MVPのCSV入力はブラウザ内でパースし、APIへは `keywords` JSON配列だけを送る。CSVファイル本体はアップロードしない。
- APIキーやWebhookの実値は画面に再表示しない。マスク値またはSecret参照名のみ表示する。

## テストと検証

- 変更内容に対して最小の関連チェックから実行する。
- 外部APIはMock/契約テストを中心にし、CIでReal APIを直接呼ばない。
- 重要観点は、プロジェクトスコープ混在防止、Secret非表示、ソフト削除、監査ログ、ジョブ冪等性、429/402/403/500/503の分岐。

代表コマンド:

```text
dotnet build
dotnet test
dotnet test --filter Category=Unit
dotnet test --filter Category=Integration
dotnet test --filter Category=Contract
dotnet ef database update --project src/SeoIntelligence.Infrastructure --startup-project src/SeoIntelligence.Api
dotnet run --project src/SeoIntelligence.Api
dotnet run --project src/SeoIntelligence.Worker
```

ソリューションやテストプロジェクトが未作成の場合は、実行できなかった理由を報告する。

## テストコード作成時の厳守事項

### 絶対に守ってください！

#### テストコードの品質
- テストは必ず実際の機能を検証すること
- `expect(true).toBe(true)` のような意味のないアサーションは絶対に書かない
- 各テストケースは具体的な入力と期待される出力を検証すること
- モックは必要最小限に留め、実際の動作に近い形でテストすること

#### ハードコーディングの禁止
- テストを通すためだけのハードコードは絶対に禁止
- 本番コードに `if (testMode)` のような条件分岐を入れない
- テスト用の特別な値（マジックナンバー）を本番コードに埋め込まない
- 環境変数や設定ファイルを使用して、テスト環境と本番環境を適切に分離すること

#### テスト実装の原則
- テストが失敗する状態から始めること（Red-Green-Refactor）
- 境界値、異常系、エラーケースも必ずテストすること
- カバレッジだけでなく、実際の品質を重視すること
- テストケース名は何をテストしているか明確に記述すること

#### 実装前の確認
- 機能の仕様を正しく理解してからテストを書くこと
- 不明な点があれば、仮の実装ではなく、ユーザーに確認すること
