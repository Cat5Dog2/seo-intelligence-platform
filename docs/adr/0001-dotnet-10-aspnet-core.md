# ADR 0001: .NET 10 LTS / ASP.NET Coreを採用する

| 項目 | 内容 |
| --- | --- |
| Status | Accepted |
| Date | 2026-05-30 |
| Decision Owner | developer |

## Context

本システムは、Web API、Blazor UI、Worker、外部APIクライアント、DBアクセス、テストを一貫した技術スタックで実装する。長期運用、保守性、型安全性、OpenAPI、非同期処理、Observabilityとの相性が重要である。

## Decision

新規開発の標準ランタイムとして.NET 10 LTSを採用し、APIはASP.NET Core Web API / Minimal APIsで実装する。

## Consequences

| 区分 | 内容 |
| --- | --- |
| 利点 | Web/API/Worker/UIを同一言語とランタイムで揃えられる。 |
| 利点 | DI、Options、Logging、OpenTelemetry、OpenAPI、EF Coreとの統合が強い。 |
| 注意 | .NET 10対応ライブラリのバージョン固定とアップデート確認が必要。 |
| 注意 | CI/CD、コンテナベースイメージ、SDKを.NET 10へ統一する。 |

## Related Documents

- ../basic_design.md
- ../api_design.md
- ../environment_setup.md
