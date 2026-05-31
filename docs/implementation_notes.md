# 実装メモ

**SEOインテリジェンス基盤**

## 1. 目的

本書は実装中に発生した細かな判断、正本ドキュメントへ反映する前の注意点、ADR化するほどではない決定を記録する場所とする。
設計を変更する場合は、必要に応じて`requirements.md`、`basic_design.md`、`api_design.md`、`db_design.md`、`job_design.md`などの正本へ反映する。

## 2. 記録ルール

- 日付は`YYYY-MM-DD`で記録する。
- 変更理由、採用判断、影響範囲、追跡先を短く残す。
- 秘密値、APIキー、Webhook URL、個人情報は書かない。
- 正本と矛盾する内容を恒久化しない。矛盾が出たら正本側を更新するか、本書のメモを削除/修正する。
- 外部APIの実データ、レスポンス全文、契約上扱いに注意が必要な情報は貼らず、Storage URIやハッシュで追跡する。

## 3. 決定ログ

| 日付 | 領域 | 判断 | 理由 | 追跡先 |
| --- | --- | --- | --- | --- |
| 2026-05-31 | Docs | `mvp_implementation_plan.md`、`domain_glossary.md`、`api_examples.md`、`implementation_notes.md`を追加 | Codex実装時の参照性を上げるため。正本は既存設計書のまま維持する。 | 本書 |

## 4. 実装時チェックリスト

| 項目 | 確認 |
| --- | --- |
| スコープ | 今回の実装対象がMVP、Phase 2、Phase 3、推奨バックログのどれかを明示したか。 |
| 正本確認 | APIは`api_design.md`、DBは`db_design.md`、ジョブは`job_design.md`、画面は`screen_design.md`を確認したか。 |
| projectId | 業務APIでbody内`projectId`を受け付けず、URL上の`projectId`を正本にしているか。 |
| Secret | 秘密値をDB、ログ、レスポンス、監査ログ、テスト出力に出していないか。 |
| Soft delete | DELETE系が物理削除ではなく`archived`または`disabled`更新になっているか。 |
| Job state | `jobs`を業務状態の正本にし、Hangfire内部状態を画面/監査の正本にしていないか。 |
| Idempotency | ジョブ登録で`Idempotency-Key`と`request_hash`による重複抑止を考慮したか。 |
| External API | RealではなくMockを既定にしているか。Real利用時はクレジットと契約スコープを確認したか。 |
| Storage | 外部API request/response本体や出力ファイルをDBへ直接保存していないか。 |
| Audit | APIキー操作、外部API実行、CSV出力、ジョブ操作、ダウンロードURL発行を監査しているか。 |
| Tests | 変更に最も近いUnit/Integration/Contract/E2Eを実行したか。未実行なら理由を残したか。 |

## 5. 未反映メモ

現時点では未反映メモなし。

| 日付 | メモ | 反映先候補 | 状態 |
| --- | --- | --- | --- |

## 6. ADR候補

以下に該当する判断は、本書ではなくADR追加を検討する。

| 判断の種類 | ADR化の目安 |
| --- | --- |
| 主要ライブラリ採用 | DB、ジョブ、認証、UI、Storage、Observabilityなどの中核技術を選ぶ場合。 |
| アーキテクチャ変更 | レイヤ構成、API方式、ジョブ基盤、データ保存方式を変える場合。 |
| セキュリティ境界 | Secret管理、認可、監査、共有URL、外部公開に関する方針を変える場合。 |
| 後戻り困難なDB変更 | パーティション、シャーディング、保持期間、履歴テーブル方針を変える場合。 |

## 7. 正本への反映手順

1. 実装差分が仕様変更か、実装詳細かを切り分ける。
2. 仕様変更なら該当する正本文書を更新する。
3. 技術選定の変更ならADRを追加または更新する。
4. テスト観点が増える場合は`test_plan.md`へ反映する。
5. 運用手順が増える場合は`operations_runbook.md`または`environment_setup.md`へ反映する。
6. 本書の未反映メモを解消済みにする。

## 8. コマンドメモ

正式なコマンドは実装後に`environment_setup.md`と`test_plan.md`へ反映する。

```text
dotnet build
dotnet test
dotnet test --filter Category=Unit
dotnet test --filter Category=Integration
dotnet test --filter Category=Contract
```
