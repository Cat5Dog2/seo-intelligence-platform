# ADR 0004: Worker Service + Hangfire + PostgreSQL storageを採用する

| 項目 | 内容 |
| --- | --- |
| Status | Accepted |
| Date | 2026-05-30 |
| Decision Owner | developer |

## Context

ラッコキーワードAPIには、一括検索ボリューム調査や順位チェックのように登録、ポーリング、結果取得が必要な非同期処理がある。ジョブ再実行、失敗履歴、スケジューリング、管理画面が必要である。

## Decision

非同期処理は.NET Worker Serviceで実行し、ジョブ実行基盤にHangfire、永続化にPostgreSQL storageを採用する。業務上の状態正本はアプリ独自の`jobs`テーブルに置く。

## Consequences

| 区分 | 内容 |
| --- | --- |
| 利点 | バックグラウンド処理、再試行、スケジュール、管理画面を早期に利用できる。 |
| 利点 | PostgreSQLに寄せることで初期構成を単純化できる。 |
| 注意 | Hangfire内部状態と業務状態を混同しない。 |
| 注意 | 高負荷化した場合はキュー分離、ワーカー数、DB負荷を調整する。 |

## Related Documents

- ../job_design.md
- ../db_design.md
- ../operations_runbook.md
