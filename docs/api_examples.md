# API例

**SEOインテリジェンス基盤**

## 1. 目的

本書はMVP実装時に使う主要APIのリクエスト/レスポンス例を示す。
正式な契約は`api_design.md`と実装後に生成されるOpenAPIを優先する。

## 2. 共通レスポンス

成功時は共通エンベロープで返す。

```json
{
  "requestId": "corr-018fd8a8",
  "result": true,
  "data": {},
  "errors": [],
  "meta": {
    "jobId": null,
    "externalRequestId": null,
    "consumedCredit": 0,
    "page": null
  }
}
```

エラー時も同じエンベロープを使う。

```json
{
  "requestId": "corr-018fd8a8",
  "result": false,
  "data": null,
  "errors": [
    {
      "code": "ValidationError",
      "message": "keyword is required",
      "target": "keyword"
    }
  ],
  "meta": {}
}
```

## 3. Project作成

`POST /api/projects`

```json
{
  "name": "自社メディアSEO",
  "defaultLocation": "Japan",
  "defaultLanguage": "Japanese",
  "kpi": {
    "organicSessions": 100000,
    "conversions": 500
  },
  "memo": "MVP検証用プロジェクト"
}
```

```json
{
  "requestId": "corr-project-create",
  "result": true,
  "data": {
    "projectId": "018fd8a8-1000-7000-9000-000000000001",
    "workspaceId": "018fd8a8-0000-7000-9000-000000000001",
    "name": "自社メディアSEO",
    "defaultLocation": "Japan",
    "defaultLanguage": "Japanese",
    "status": "active",
    "createdAt": "2026-05-31T00:00:00Z",
    "updatedAt": "2026-05-31T00:00:00Z"
  },
  "errors": [],
  "meta": {}
}
```

## 4. Projectアーカイブ

`DELETE /api/projects/{projectId}`

```json
{
  "requestId": "corr-project-delete",
  "result": true,
  "data": {
    "projectId": "018fd8a8-1000-7000-9000-000000000001",
    "status": "archived",
    "archivedAt": "2026-05-31T00:10:00Z"
  },
  "errors": [],
  "meta": {}
}
```

物理削除は行わない。

## 5. Site作成

`POST /api/projects/{projectId}/sites`

```json
{
  "domain": "example.com",
  "canonicalUrl": "https://example.com/",
  "type": "own"
}
```

```json
{
  "requestId": "corr-site-create",
  "result": true,
  "data": {
    "siteId": "018fd8a8-2000-7000-9000-000000000001",
    "projectId": "018fd8a8-1000-7000-9000-000000000001",
    "domain": "example.com",
    "canonicalUrl": "https://example.com/",
    "type": "own",
    "status": "active"
  },
  "errors": [],
  "meta": {}
}
```

## 6. API認証情報作成

`POST /api/admin/api-credentials`

秘密値をAPIサーバーへ渡してSecret Storeへ保存する場合。Configuration実装ではAPIプロセス内にのみ保存されるため、再起動で失われ、Workerプロセスからは参照できない点に注意する（詳細は`api_design.md`と`environment_setup.md`を参照）。

```json
{
  "provider": "rakko-keyword",
  "secretValue": "<input-only-secret>"
}
```

既存Secret参照を使う場合。

```json
{
  "provider": "rakko-keyword",
  "keyRef": "rakko-keyword-api-key-dev"
}
```

```json
{
  "requestId": "corr-credential-create",
  "result": true,
  "data": {
    "credentialId": "018fd8a8-3000-7000-9000-000000000001",
    "provider": "rakko-keyword",
    "keyRef": "rakko-keyword-api-key-dev",
    "status": "active",
    "createdAt": "2026-05-31T00:00:00Z"
  },
  "errors": [],
  "meta": {}
}
```

`secretValue`はレスポンス、ログ、監査ログへ出さない。

## 7. 通知チャンネル作成

`POST /api/admin/notification-channels`

```json
{
  "projectId": null,
  "channelType": "discord",
  "name": "MVP Alerts",
  "webhookSecretRef": "discord-webhook-dev",
  "eventTypes": [
    "job_failed",
    "credit_low"
  ]
}
```

```json
{
  "requestId": "corr-channel-create",
  "result": true,
  "data": {
    "channelId": "018fd8a8-4000-7000-9000-000000000001",
    "channelType": "discord",
    "name": "MVP Alerts",
    "eventTypes": [
      "job_failed",
      "credit_low"
    ],
    "status": "active"
  },
  "errors": [],
  "meta": {}
}
```

## 8. キーワード探索

`POST /api/projects/{projectId}/keyword-discovery/suggest`

軽量条件では同期完了として200を返してよい。

```json
{
  "seedKeyword": "seo ツール",
  "sources": [
    "google",
    "related",
    "other",
    "question",
    "ranking"
  ],
  "location": "Japan",
  "language": "Japanese",
  "limit": 100,
  "filter": {
    "include": [
      "無料"
    ],
    "exclude": [
      "求人"
    ]
  },
  "sortBy": "opportunityScore",
  "orderBy": "desc",
  "syncPreferred": true
}
```

```json
{
  "requestId": "corr-keyword-sync",
  "result": true,
  "data": {
    "seedId": "018fd8a8-5000-7000-9000-000000000001",
    "items": [
      {
        "keywordId": "018fd8a8-5100-7000-9000-000000000001",
        "keyword": "seo ツール 無料",
        "source": "google",
        "suggestClass": "suggest",
        "searchVolume": 1200,
        "seoDifficulty": 42.5,
        "cpc": 180.0,
        "competition": 0.62,
        "firstSeenRange": "2026-05",
        "opportunityScore": 73.4
      }
    ],
    "summary": {
      "total": 1,
      "deduplicated": 1,
      "consumedCredit": 2
    }
  },
  "errors": [],
  "meta": {
    "consumedCredit": 2
  }
}
```

重い条件では202 Acceptedでジョブレスポンスを返す。

```json
{
  "requestId": "corr-keyword-async",
  "result": true,
  "data": {
    "jobId": "018fd8a8-5200-7000-9000-000000000001",
    "jobType": "KeywordDiscoveryJob",
    "status": "queued",
    "progress": 0,
    "statusUrl": "/api/jobs/018fd8a8-5200-7000-9000-000000000001",
    "externalRequestId": null,
    "resultResource": null,
    "retryCount": 0,
    "nextRunAt": null,
    "error": null
  },
  "errors": [],
  "meta": {
    "jobId": "018fd8a8-5200-7000-9000-000000000001",
    "externalRequestId": null,
    "consumedCredit": 0,
    "page": null
  }
}
```

## 9. 一括検索ボリュームジョブ登録

`POST /api/projects/{projectId}/search-volume/jobs`

CSVファイル本体は送らない。Blazor UIでパース済みの`keywords`配列を送る。

```json
{
  "keywords": [
    "seo ツール",
    "seo 分析",
    "検索順位 チェック"
  ],
  "location": "Japan",
  "language": "Japanese",
  "seoDifficulty": true,
  "aggregationPeriodMonths": 12
}
```

```json
{
  "requestId": "corr-search-volume-register",
  "result": true,
  "data": {
    "jobId": "018fd8a8-6000-7000-9000-000000000001",
    "jobType": "RegisterSearchVolumeJob",
    "status": "queued",
    "progress": 0,
    "statusUrl": "/api/jobs/018fd8a8-6000-7000-9000-000000000001",
    "externalRequestId": null,
    "resultResource": null,
    "retryCount": 0,
    "nextRunAt": null,
    "error": null
  },
  "errors": [],
  "meta": {
    "jobId": "018fd8a8-6000-7000-9000-000000000001",
    "externalRequestId": null,
    "consumedCredit": 0,
    "page": null
  }
}
```

同一`Idempotency-Key`と同一request hashでは既存ジョブを返す。

## 10. ジョブ状態取得

`GET /api/jobs/{jobId}`

```json
{
  "requestId": "corr-job-get",
  "result": true,
  "data": {
    "jobId": "018fd8a8-6000-7000-9000-000000000001",
    "jobType": "RegisterSearchVolumeJob",
    "status": "waiting_external",
    "progress": 45,
    "statusUrl": "/api/jobs/018fd8a8-6000-7000-9000-000000000001",
    "externalRequestId": "rakko-request-001",
    "resultResource": null,
    "retryCount": 1,
    "nextRunAt": "2026-05-31T00:05:00Z",
    "error": null
  },
  "errors": [],
  "meta": {
    "jobId": "018fd8a8-6000-7000-9000-000000000001",
    "externalRequestId": "rakko-request-001",
    "consumedCredit": 0
  }
}
```

## 11. 一括検索ボリューム結果取得

`GET /api/projects/{projectId}/search-volume/jobs/{jobId}/results?page=1&pageSize=50`

```json
{
  "requestId": "corr-search-volume-results",
  "result": true,
  "data": {
    "jobId": "018fd8a8-6000-7000-9000-000000000001",
    "items": [
      {
        "keywordId": "018fd8a8-5100-7000-9000-000000000001",
        "keyword": "seo ツール",
        "location": "Japan",
        "language": "Japanese",
        "searchVolume": 5400,
        "seoDifficulty": 58.2,
        "cpc": 240.0,
        "competition": 0.71,
        "monthlyVolumes": [
          {
            "yearMonth": "2026-04",
            "searchVolume": 5200
          },
          {
            "yearMonth": "2026-05",
            "searchVolume": 5400
          }
        ],
        "opportunityScore": 66.8,
        "cacheHit": false,
        "sourceCallId": "018fd8a8-7000-7000-9000-000000000001"
      }
    ]
  },
  "errors": [],
  "meta": {
    "page": {
      "page": 1,
      "pageSize": 50,
      "total": 1
    }
  }
}
```

## 12. CSV出力ジョブ登録

`POST /api/projects/{projectId}/exports/csv`

```json
{
  "exportType": "keyword_metrics",
  "filter": {
    "minSearchVolume": 100,
    "minOpportunityScore": 50
  },
  "columns": [
    "keyword",
    "searchVolume",
    "seoDifficulty",
    "cpc",
    "competition",
    "opportunityScore"
  ]
}
```

```json
{
  "requestId": "corr-export-register",
  "result": true,
  "data": {
    "jobId": "018fd8a8-8000-7000-9000-000000000001",
    "jobType": "DataExportJob",
    "status": "queued",
    "progress": 0,
    "statusUrl": "/api/jobs/018fd8a8-8000-7000-9000-000000000001",
    "externalRequestId": null,
    "resultResource": null,
    "retryCount": 0,
    "nextRunAt": null,
    "error": null
  },
  "errors": [],
  "meta": {
    "jobId": "018fd8a8-8000-7000-9000-000000000001"
  }
}
```

## 13. CSV出力状態取得

`GET /api/projects/{projectId}/exports/{exportId}`

```json
{
  "requestId": "corr-export-get",
  "result": true,
  "data": {
    "exportId": "018fd8a8-8100-7000-9000-000000000001",
    "projectId": "018fd8a8-1000-7000-9000-000000000001",
    "exportType": "keyword_metrics",
    "format": "csv",
    "status": "succeeded",
    "fileUri": "storage://exports/018fd8a8-8100.csv",
    "createdAt": "2026-05-31T00:00:00Z",
    "completedAt": "2026-05-31T00:01:00Z"
  },
  "errors": [],
  "meta": {}
}
```

## 14. CSVダウンロードURL発行

`GET /api/projects/{projectId}/exports/{exportId}/download`

```json
{
  "requestId": "corr-export-download",
  "result": true,
  "data": {
    "exportId": "018fd8a8-8100-7000-9000-000000000001",
    "downloadUrl": "/api/projects/018fd8a8-1000-7000-9000-000000000001/exports/018fd8a8-8100-7000-9000-000000000001/content"
  },
  "errors": [],
  "meta": {}
}
```

## 14.1 CSVファイル本体の取得

`GET /api/projects/{projectId}/exports/{exportId}/content`

成功時はエンベロープではなくファイル本体を返す。

```text
HTTP/1.1 200 OK
Content-Type: text/csv; charset=utf-8
Content-Disposition: attachment; filename=keyword_metrics-018fd8a881007000900000000000001.csv

keyword,searchVolume,opportunityScore
content marketing,1200,72.5
```

URL発行は`csv_export.download_url_issued`、ファイル取得は`csv_export.downloaded`として`audit_logs`へ記録する。

ブラウザからはサービスキーを提示できないため、画面ではWebホストの`/downloads/projects/{projectId}/exports/{exportId}`を開く。Webホストが管理者Cookieで認可し、サービスキー付きで上記APIを呼んで応答を中継する。

## 15. 監査ログ検索

`GET /api/admin/audit-logs?resourceType=api_credential&page=1&pageSize=50`

```json
{
  "requestId": "corr-audit-list",
  "result": true,
  "data": {
    "items": [
      {
        "auditLogId": "018fd8a8-9000-7000-9000-000000000001",
        "actor": "developer",
        "action": "api_credential.created",
        "resourceType": "api_credential",
        "resourceId": "018fd8a8-3000-7000-9000-000000000001",
        "correlationId": "corr-credential-create",
        "createdAt": "2026-05-31T00:00:00Z"
      }
    ]
  },
  "errors": [],
  "meta": {
    "page": {
      "page": 1,
      "pageSize": 50,
      "total": 1
    }
  }
}
```

## 16. 主要エラー例

### 16.1 別プロジェクト参照

URL上の`projectId`と対象リソースの`project_id`が一致しない場合は404または403を返す。

```json
{
  "requestId": "corr-project-scope-error",
  "result": false,
  "data": null,
  "errors": [
    {
      "code": "ResourceNotFound",
      "message": "resource was not found",
      "target": "exportId"
    }
  ],
  "meta": {}
}
```

### 16.2 外部API 402

```json
{
  "requestId": "corr-credit-error",
  "result": true,
  "data": {
    "jobId": "018fd8a8-6000-7000-9000-000000000001",
    "jobType": "RegisterSearchVolumeJob",
    "status": "failed_fatal",
    "progress": 30,
    "statusUrl": "/api/jobs/018fd8a8-6000-7000-9000-000000000001",
    "externalRequestId": null,
    "resultResource": null,
    "retryCount": 0,
    "nextRunAt": null,
    "error": {
      "code": "ExternalApiCreditRequired",
      "message": "Rakko Keyword API credit is insufficient"
    }
  },
  "errors": [],
  "meta": {
    "jobId": "018fd8a8-6000-7000-9000-000000000001"
  }
}
```

402は同一ジョブで再試行しない。Discord通知と監査ログを残す。

## 17. RunbookスモークAPI

`scripts/smoke-test.ps1` / `scripts/smoke-test.sh` は以下のMVP運用APIを確認する。

```text
GET /healthz
GET /readyz
GET /api/projects?page=1&pageSize=5
GET /api/admin/audit-logs?page=1&pageSize=5
POST /api/admin/master-data/sync
POST /api/projects/{projectId}/exports/csv
```

プロジェクトID未指定時は、スモーク用プロジェクトを作成してCSV出力ジョブ登録に使う。

`POST /api/projects`

```json
{
  "name": "Runbook smoke 20260602000000",
  "defaultLocation": "Japan",
  "defaultLanguage": "Japanese",
  "kpi": {},
  "memo": "Created by scripts/smoke-test.ps1"
}
```

`POST /api/admin/master-data/sync`

```json
{
  "requestId": "corr-smoke-master-data",
  "result": true,
  "data": {
    "jobId": "018fd8a8-a000-7000-9000-000000000001",
    "status": "queued"
  },
  "errors": [],
  "meta": {
    "jobId": "018fd8a8-a000-7000-9000-000000000001"
  }
}
```

Discordテスト通知はSecret参照が設定済みのチャンネルIDを `SMOKE_DISCORD_CHANNEL_ID` に指定した場合だけ実行する。
