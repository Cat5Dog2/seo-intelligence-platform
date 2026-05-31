# ADR 0003: PostgreSQL / EF Core / JSONBを採用する

| 項目 | 内容 |
| --- | --- |
| Status | Accepted |
| Date | 2026-05-30 |
| Decision Owner | developer |

## Context

本システムは、正規化キーワード、検索指標履歴、順位履歴、ジョブ、監査ログ、外部APIローデータ参照、可変な分析条件を扱う。リレーショナル整合性とJSONの柔軟性を両立する必要がある。

## Decision

DBはPostgreSQLを採用し、アプリケーションからはEF Coreでアクセスする。可変条件や外部レスポンススナップショットはJSONBで保持する。

## Consequences

| 区分 | 内容 |
| --- | --- |
| 利点 | リレーショナル制約、インデックス、JSONB、全文検索補助を同一DBで扱える。 |
| 利点 | EF Core migrationsでスキーマ変更を管理できる。 |
| 注意 | JSONBへ寄せすぎると検索性と型安全性が落ちるため、業務キーと検索条件は列として持つ。 |
| 注意 | 大量履歴テーブルは将来パーティション化を検討する。 |

## Related Documents

- ../db_design.md
- ../test_plan.md
