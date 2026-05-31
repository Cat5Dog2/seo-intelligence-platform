# ADR 0002: フロントエンドにBlazor Web Appを採用する

| 項目 | 内容 |
| --- | --- |
| Status | Accepted |
| Date | 2026-05-30 |
| Decision Owner | developer |

## Context

初期版は開発者本人が利用する業務ツールであり、SEOデータの検索、一覧、ジョブ進捗、管理画面、レポート確認が中心となる。フロントエンドの開発効率とバックエンド契約の型安全性を重視する。

## Decision

フロントエンドはBlazor Web Appを採用する。SSR/Interactive ServerまたはWASMの選択は、実装時のホスティング要件と応答性に応じて決める。

## Consequences

| 区分 | 内容 |
| --- | --- |
| 利点 | .NET DTOやバリデーションモデルを共有しやすい。 |
| 利点 | 管理UI、ダッシュボード、ジョブ進捗など業務画面を少ない技術差分で実装できる。 |
| 注意 | 大量テーブルやグラフでは仮想化、ページング、差分更新を設計する必要がある。 |
| 注意 | Interactive Server採用時は接続状態とSignalR負荷を考慮する。 |

## Related Documents

- ../screen_design.md
- ../basic_design.md
