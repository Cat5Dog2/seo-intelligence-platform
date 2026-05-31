# ADR 0006: 外部API DTOをOpenAPIから生成する

| 項目 | 内容 |
| --- | --- |
| Status | Accepted |
| Date | 2026-05-30 |
| Decision Owner | developer |

## Context

ラッコキーワードAPIはOpenAPI 3.0仕様として提供される。エンドポイント数が多く、リクエスト/レスポンスの項目や制約の手書き実装は仕様変更時の漏れにつながる。

## Decision

外部API DTOは`docs/rakko-keyword-api-docs.json`から生成し、Infrastructure層に閉じ込める。Application層では業務DTOへ変換する。

## Consequences

| 区分 | 内容 |
| --- | --- |
| 利点 | OpenAPI差分をレビューしやすく、契約テストを組みやすい。 |
| 利点 | 外部API仕様の型変更を早期に検知できる。 |
| 注意 | 生成コードを直接編集しない。 |
| 注意 | 外部DTOを画面/APIの公開契約に直接漏らさない。 |

## Related Documents

- ../api_design.md
- ../external_api_design.md
- ../test_plan.md
