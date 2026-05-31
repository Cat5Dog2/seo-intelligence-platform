# ADR 0005: Redisをキャッシュ、分散ロック、レート制御に使う

| 項目 | 内容 |
| --- | --- |
| Status | Accepted |
| Date | 2026-05-30 |
| Decision Owner | developer |

## Context

外部API呼び出しの重複抑止、レート制御、短時間キャッシュ、ジョブ重複実行防止が必要である。ジョブ永続化はHangfire PostgreSQL storageで行うため、Redisは補助用途に限定する。

## Decision

Redisをキャッシュ、分散ロック、レート制御、一時状態管理に採用する。ジョブキューの永続化正本にはしない。

## Consequences

| 区分 | 内容 |
| --- | --- |
| 利点 | 重複実行防止、外部API同時実行数制御、短時間キャッシュを高速に扱える。 |
| 利点 | Redis障害時もDB上の永続データは保持される。 |
| 注意 | Redis上の値を監査正本にしない。 |
| 注意 | ロックのTTL、キー設計、障害時の解放をテストする。 |

## Related Documents

- ../job_design.md
- ../external_api_design.md
