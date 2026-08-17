# ADR 0006: 外部API DTOをOpenAPIから生成する

| 項目 | 内容 |
| --- | --- |
| Status | Accepted |
| Date | 2026-05-30 (2026-07-26, 2026-08-17 改訂) |
| Decision Owner | developer |

## Context

ラッコキーワードAPIはOpenAPI仕様(v1.14.0時点でOpenAPI 3.1)として提供される。エンドポイント数が多く、リクエスト/レスポンスの項目や制約の手書き実装は仕様変更時の漏れにつながる。

## Decision

外部API DTOは`docs/rakko-keyword-api-docs.json`を正本として`Generated/`配下に置き、Infrastructure層に閉じ込める。Application層では業務DTOへ変換する。

DTOはスキーマ準拠のコードとして次の手順でのみ更新する(この手順外での直接編集はしない):

1. vendor仕様(`docs/rakko-keyword-api-docs.json`)の差分をレビューする。
2. `Generated/`配下のDTOをスキーマ差分に合わせて更新する。アプリが使用しないoptionalプロパティの省略は許容する。
3. `scripts/generate-rakko-keyword-dtos.ps1`でOpenApiVersion/SourceSha256メタデータを更新する(`-ValidateOnly`はメタデータと必須スキーマ名の存在を検証する)。
4. ContractTestsの`RakkoKeywordDtoShapeContractTests`がDTO形状を検証する: DTOの全プロパティがスキーマに存在すること(削除・改名の検知)、スキーマのrequiredプロパティがDTOに存在すること(必須項目欠落の検知)を、ネストされたオブジェクトまで再帰的に照合する。

## Consequences

| 区分 | 内容 |
| --- | --- |
| 利点 | OpenAPI差分をレビューしやすく、契約テストを組みやすい。 |
| 利点 | 外部API仕様の型変更(必須項目の削除・改名)を契約テストで早期に検知できる。 |
| 注意 | 上記の更新手順外で生成コードを直接編集しない。 |
| 注意 | 外部DTOを画面/APIの公開契約に直接漏らさない。 |
| 注意 | optionalプロパティの省略は許容されるため、新規機能の利用時はスキーマとDTOの差分を確認する。 |

## Related Documents

- ../api_design.md
- ../external_api_design.md
- ../test_plan.md
