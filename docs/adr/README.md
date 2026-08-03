# ADR一覧

**SEOインテリジェンス基盤**

本ディレクトリは、主要な技術選定と設計判断をArchitecture Decision Recordとして残す。

## ADR一覧

| ADR | タイトル | 状態 |
| --- | --- | --- |
| 0001 | .NET 10 LTS / ASP.NET Coreを採用する | Accepted |
| 0002 | フロントエンドにBlazor Web Appを採用する | Accepted |
| 0003 | PostgreSQL / EF Core / JSONBを採用する | Accepted |
| 0004 | Worker Service + Hangfire + PostgreSQL storageを採用する | Accepted |
| 0005 | Redisをキャッシュ、分散ロック、レート制御に使う | Accepted |
| 0006 | 外部API DTOをOpenAPIから生成する | Accepted |
| 0007 | 秘密情報はSecret Store参照とし監査ログを保持する | Accepted |
| 0008 | 単一管理者ログインにASP.NET Core Identityを採用しAPIをサービスキーで保護する | Accepted |

## フォーマット

各ADRは以下の構成にする。

- Status
- Context
- Decision
- Consequences
- Related Documents
