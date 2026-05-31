# ADR 0007: 秘密情報はSecret Store参照とし監査ログを保持する

| 項目 | 内容 |
| --- | --- |
| Status | Accepted |
| Date | 2026-05-30 |
| Decision Owner | developer |

## Context

本システムはラッコキーワードAPIキー、Discord Webhook、AI APIキー、将来OAuthトークンを扱う可能性がある。初期版は単一利用者でも、秘密情報漏えいと操作追跡を防ぐ必要がある。

## Decision

秘密情報の実値はSecret Storeへ保存し、DBには`key_ref`や`webhook_secret_ref`のみ保存する。APIレスポンスと画面には実値を返さない。APIキー操作、外部API実行、CSV/レポート出力、AI実行、ジョブ操作は`audit_logs`へ保存する。

## Consequences

| 区分 | 内容 |
| --- | --- |
| 利点 | DB漏えい時の秘密情報露出を抑えられる。 |
| 利点 | 外部API消費、出力、AI利用を後から追跡できる。 |
| 注意 | ローカル開発でも`.env`や設定ファイルに実値をコミットしない。 |
| 注意 | Secretローテーションと参照権限の運用手順が必要。 |

## Related Documents

- ../db_design.md
- ../api_design.md
- ../operations_runbook.md
- ../environment_setup.md
