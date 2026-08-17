# ラッコキーワードAPI

- **OpenAPI Version:** `3.1.1`
- **API Version:** `1.14.0`

ラッコキーワードAPIの仕様です。\
スタンダードプランでAPIキー（最大5個）を発行することで利用できます。\
取得データは社内利用の範囲でご利用いただけます。サービスへの組み込み等をご検討の際はAPI利用ガイドライン・利用規約を厳守の上ご利用ください。\
MCPの設定方法や接続手順については、MCP設定ガイドをご覧ください。\
\
[API Docs（JSON（OpenAPI）/ AI・プログラム向け）](/api-docs.json)\
[API Docs（Markdown / 人間向け）](/api-docs.md)

## Servers

- **URL:** `https://api.rakkokeyword.com`
  - **Description:** 本番環境

## Operations

### サジェストキーワード取得

- **Method:** `POST`
- **Path:** `/v1/suggest-keywords`
- **Tags:** サジェストキーワード取得

サジェストキーワード取得。

あるキーワードの関連語を広く集めたいときにまず使う。 Google/YouTube/Amazon/楽天/Bing等から、modesパラメータで指定（複数可）した検索エンジンのサジェストを返す。

Googleは汎用的、SEO目的に適する。Bingは汎用的だがユーザー層が高齢者層寄り。 YouTube,Google動画は動画リサーチに、Amazonや楽天はECにおすすめ。

サジェストには、検索エンジンが元キーワードを入力した人の検索意図を汲み取って提案する「関連性の高い複合キーワード」が表示される。

キーワードリサーチの最初期に使用すると、ユーザーがどのような掛け合わせワードで検索しているのか、市場需要や検索意図を幅広く把握できる。

サジェスト候補は通常最大1,000件前後、increaseKeyword=trueで最大10,000件前後存在する。

月間検索数・SEO難易度・CPC・競合性などのSEO指標付き。 SEO難易度はデータがない場合が多い。またSEO指標は最新でない可能性がある。 この指標を重要視する用途なら、データ取得後にPOST /v1/search-volumeで最新のSEO指標を取得すること。

さらに大量のキーワードが必要な場合は POST /v1/related-keywordsを使う。 元キーワードと検索意図の近いキーワードを取得したい場合は POST /v1/ranking-keywordsを使う。 検索意図を深掘りしたい場合は、元キーワードを調べた人が次に調べるKW・抱えている疑問を取得できるPOST /v1/other-keywordsを併用する。

1リクエストあたり1.5クレジットを消費。

#### Request Body

##### Content-Type: application/json

- **`keyword` (required)**

  `string` — サジェスト取得の元となる検索キーワード。1文字以上の文字列を指定する。

- **`filter`**

  `object` — 結果のフィルタリング条件。月間検索数・SEO難易度・CPC・競合性・出現時期・サジェストクラスなどで絞り込む。

  - **`competition`**

    `object` — 競合性フィルタ（0〜100の範囲指定）

    - **`max`**

      `integer` — 最大値

    - **`min`**

      `integer` — 最小値

  - **`cpc`**

    `object` — クリック単価（CPC）フィルタ（USD、範囲指定）

    - **`max`**

      `number` — 最大CPC

    - **`min`**

      `number` — 最小CPC

  - **`firstSeenRange`**

    `object` — 出現時期フィルタ

    - **`include`**

      `string`, possible values: `"last_7_days", "last_30_days", "last_90_days", "within_6_months", "within_1_year", "over_1_year"` — 出現時期の選択肢

  - **`keyword`**

    `object` — キーワードフィルタ

    - **`includes`**

      `array` — 含む単語のリスト（複数入力時はOR）

      **Items:**

      `string`

    - **`notIncludes`**

      `array` — 含まない単語のリスト（複数入力時はOR）

      **Items:**

      `string`

  - **`searchVolume`**

    `object` — 月間検索数フィルタ（範囲指定）

    - **`max`**

      `integer` — 最大値

    - **`min`**

      `integer` — 最小値

  - **`seoDifficulty`**

    `object` — SEO難易度フィルタ（0〜100の範囲指定）

    - **`max`**

      `integer` — 最大値

    - **`min`**

      `integer` — 最小値

  - **`suggestClass`**

    `array` — サジェストクラスフィルタ（0-3の配列）。0: ＋（サジェスト）, 1: ＋＋（サジェストのサジェスト）, 2: ＋α（元キーワードにあいうえお...・abcde...・12345...を付与した際に表示されるサジェスト）, 3: ＋＋＋（「＋＋」または「＋α」からさらに展開されたサジェスト）

    **Items:**

    `integer`

- **`increaseKeyword`**

  `boolean`, default: `false` — キーワード増量オプション。true にすると、より多くのサジェストキーワードを取得する。SEOキーワードを網羅的に取得したい場合は、trueにすること。省略時は false。

- **`limit`**

  `integer` — 取得件数の上限。正の整数を指定。省略時はすべての結果を返す。

- **`modes`**

  `array`, default: `["google"]` — サジェストキーワードを取得する検索エンジン（複数選択可）。google / bing / youtube / googleVideo / amazon / rakuten / googleShopping / googleImage から選択。省略時は google のみ。

  **Items:**

  `string`, possible values: `"google", "bing", "youtube", "googleVideo", "amazon", "rakuten", "googleShopping", "googleImage"`

- **`orderBy`**

  `string`, possible values: `"asc", "desc"`, default: `"desc"` — ソート順。asc: 昇順 / desc: 降順。省略時は desc。

- **`sortBy`**

  `string`, possible values: `"keyword", "suggestClass", "seoDifficulty", "searchVolume", "cpc", "competition", "firstSeenRange"`, default: `"searchVolume"` — 結果のソート項目。keyword / suggestClass / seoDifficulty / searchVolume / cpc / competition / firstSeenRange。省略時は searchVolume。

**Example:**

```json
{
  "keyword": "ラッコ",
  "modes": [
    "google",
    "bing"
  ],
  "increaseKeyword": false,
  "filter": {
    "suggestClass": [
      0,
      1
    ],
    "keyword": {
      "includes": [
        "水族館"
      ],
      "notIncludes": [
        "グッズ"
      ]
    },
    "seoDifficulty": {
      "min": 1,
      "max": 100
    },
    "searchVolume": {
      "min": 100,
      "max": 10000
    },
    "cpc": {
      "min": 0.5,
      "max": 10
    },
    "competition": {
      "min": 1,
      "max": 100
    },
    "firstSeenRange": {
      "include": "last_30_days"
    }
  },
  "sortBy": "searchVolume",
  "orderBy": "desc",
  "limit": 10
}
```

#### Responses

##### Status: 200 検索成功

###### Content-Type: application/json

- **`data` (required)**

  `object` — サジェストキーワード検索結果データ

  - **`items` (required)**

    `array` — サジェストキーワードのリスト。各アイテムにキーワード・サジェスト分類・SEO指標・取得エンジン情報を含む。

    **Items:**

    - **`keyword` (required)**

      `string` — サジェストキーワード文字列

    - **`metrics` (required)**

      `object` — SEO関連の各種指標（検索ボリューム・SEO難易度・CPC・競合性・出現時期）

      - **`competition` (required)**

        `object` — 広告競合性。0–100で表し、高いほど競合性が高い（0–33:低 / 34–66:中 / 67–100:高）。

      - **`cpc` (required)**

        `object` — 推定クリック単価（USD）

      - **`firstSeenRange` (required)**

        `object` — 出現時期。キーワードが最初にラッコキーワードデータベースで検出された時期を日付範囲ラベルで表す。不明な場合は null。

      - **`searchVolume` (required)**

        `object` — 月間検索数（年平均）

      - **`seoDifficulty` (required)**

        `object` — SEO難易度。1–100で表し、高いほど難易度が高い（1–33:低 / 34–66:中 / 67–100:高）。不明な場合は null。

    - **`suggestClass` (required)**

      `string` — サジェストキーワードの区分ラベル。＋（0: サジェスト）, ＋＋（1: サジェストのサジェスト）, ＋α（2: 元キーワードにあいうえお...・abcde...・12345...を付与した際に表示されるサジェスト）, ＋＋＋（3: 「＋＋」または「＋α」からさらに展開されたサジェスト）

    - **`suggestEngines` (required)**

      `object` — このサジェストキーワードを返した検索エンジンの情報（エンジン数と一覧）

      - **`active` (required)**

        `array` — このキーワードが取得できたサーチエンジン一覧

        **Items:**

        `string`, possible values: `"google", "bing", "youtube", "googleVideo", "amazon", "rakuten", "googleShopping", "googleImage"`

      - **`count` (required)**

        `number` — このキーワードが取得できたサーチエンジン数

  - **`query` (required)**

    `object` — リクエストで指定された検索クエリ情報（キーワードと対象エンジン）

    - **`keyword` (required)**

      `string` — サジェスト取得の元になった検索キーワード

    - **`suggestEngines` (required)**

      `array` — サジェストキーワードの取得対象としたサーチエンジン一覧。単一取得の場合も配列で出力されます。

      **Items:**

      `string`, possible values: `"google", "bing", "youtube", "googleVideo", "amazon", "rakuten", "googleShopping", "googleImage"`

  - **`summary` (required)**

    `object` — 件数サマリー（全体件数とレスポンスに含まれる件数）

    - **`returnedCount` (required)**

      `number` — このレスポンスに含まれている件数

    - **`totalCount` (required)**

      `number` — 取得対象全体の件数

- **`errors` (required)**

  `array` — エラーメッセージの配列。正常時は空配列。

  **Items:**

  `string`

- **`meta` (required)**

  `object` — リクエストに関するメタ情報（課金・消費リソースなど）

  - **`consumedCredit` (required)**

    `number` — このリクエストで消費されたクレジット数。

- **`result` (required)**

  `boolean` — API 呼び出しの成否。正常時は true、エラー時は false。

**Example:**

```json
{
  "result": true,
  "meta": {
    "consumedCredit": 1.5
  },
  "data": {
    "query": {
      "keyword": "ラッコ",
      "suggestEngines": [
        "google"
      ]
    },
    "summary": {
      "totalCount": 150,
      "returnedCount": 100
    },
    "items": [
      {
        "keyword": "ラッコ 水族館",
        "suggestClass": "＋",
        "metrics": {
          "seoDifficulty": 45,
          "searchVolume": 12000,
          "cpc": 1.5,
          "competition": 2,
          "firstSeenRange": "last_30_days"
        },
        "suggestEngines": {
          "count": 2,
          "active": [
            "google",
            "youtube"
          ]
        }
      }
    ]
  },
  "errors": []
}
```

##### Status: 400 バリデーションエラー

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "keyword is a required field"
  ]
}
```

##### Status: 402 クレジット不足

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Insufficient credits. Required: 1, Available: 0"
  ]
}
```

##### Status: 403 認証エラー

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Forbidden"
  ]
}
```

##### Status: 429 レート制限超過

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Rate limit exceeded. Please try again later."
  ]
}
```

##### Status: 500 Internal Server Error

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "500 Internal Server Error"
  ]
}
```

##### Status: 503 Service Unavailable - データベース接続エラーなど

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Service Unavailable"
  ]
}
```

### 関連キーワード取得

- **Method:** `POST`
- **Path:** `/v1/related-keywords`
- **Tags:** 関連キーワード取得

関連キーワード取得。

指定キーワードに部分一致するキーワードをラッコのDBから大量に取得する。 関連語が欲しい場合はまずPOST /v1/suggest-keywordsを使い、さらに大量にKWを取得したい場合にのみ当機能を使う。

最大25,000件を取得。月間検索数・SEO難易度・CPC・競合性などのSEO指標付きで返却。 SEO難易度はデータがない場合が多い。またSEO指標は最新でない可能性がある。 この指標を重要視する用途なら、データ取得後にPOST /v1/search-volumeで最新のSEO指標を取得すること。

検索意図の近いキーワードを取得したい場合はPOST /v1/ranking-keywordsを、 LSIキーワードを取得したい場合はPOST /v1/other-keywordsを使う。

1リクエストあたり1.5クレジットを消費。

#### Request Body

##### Content-Type: application/json

- **`keyword` (required)**

  `string` — 関連キーワード取得の元となる検索キーワード。1文字以上の文字列を指定する。

- **`filter`**

  `object` — 結果のフィルタリング条件。月間検索数・SEO難易度・CPC・競合性・出現時期などで絞り込む。

  - **`competition`**

    `object` — 競合性フィルタ（0〜100の範囲指定）

    - **`max`**

      `integer` — 最大値

    - **`min`**

      `integer` — 最小値

  - **`cpc`**

    `object` — クリック単価（CPC）フィルタ（USD、範囲指定）

    - **`max`**

      `number` — 最大CPC

    - **`min`**

      `number` — 最小CPC

  - **`firstSeenRange`**

    `object` — 出現時期フィルタ

    - **`include`**

      `string`, possible values: `"last_7_days", "last_30_days", "last_90_days", "within_6_months", "within_1_year", "over_1_year"` — 出現時期の選択肢

  - **`keyword`**

    `object` — キーワードフィルタ（含む/含まないキーワード指定）

    - **`includes`**

      `array` — 含む単語のリスト（複数入力時はOR）

      **Items:**

      `string`

    - **`notIncludes`**

      `array` — 含まない単語のリスト（複数入力時はOR）

      **Items:**

      `string`

  - **`searchVolume`**

    `object` — 月間検索数フィルタ（範囲指定）

    - **`max`**

      `integer` — 最大値

    - **`min`**

      `integer` — 最小値

  - **`seoDifficulty`**

    `object` — SEO難易度フィルタ（0〜100の範囲指定）

    - **`max`**

      `integer` — 最大値

    - **`min`**

      `integer` — 最小値

- **`limit`**

  `integer`, default: `1000` — 取得件数の上限。1〜25000 の整数を指定。省略時は 1000 件。

- **`matchType`**

  `string`, possible values: `"partialMatch", "phraseMatch", "prefixMatch", "suffixMatch", "wordMatch"`, default: `"partialMatch"` — キーワードのマッチタイプ。partialMatch: 部分一致 / phraseMatch: フレーズ一致 / prefixMatch: 前方一致 / suffixMatch: 後方一致 / wordMatch: 単語一致。省略時は partialMatch。

- **`orderBy`**

  `string`, possible values: `"asc", "desc"`, default: `"desc"` — ソート順。asc: 昇順 / desc: 降順。省略時は desc。

- **`sortBy`**

  `string`, possible values: `"seoDifficulty", "searchVolume", "cpc", "competition", "firstSeenRange"`, default: `"searchVolume"` — 結果のソート項目。seoDifficulty / searchVolume / cpc / competition / firstSeenRange。省略時は searchVolume。

**Example:**

```json
{
  "keyword": "ラッコ",
  "matchType": "partialMatch",
  "filter": {
    "keyword": {
      "includes": [
        "水族館"
      ],
      "notIncludes": [
        "グッズ"
      ]
    },
    "seoDifficulty": {
      "min": 1,
      "max": 100
    },
    "searchVolume": {
      "min": 100,
      "max": 10000
    },
    "cpc": {
      "min": 0.5,
      "max": 10
    },
    "competition": {
      "min": 1,
      "max": 100
    },
    "firstSeenRange": {
      "include": "last_30_days"
    }
  },
  "sortBy": "searchVolume",
  "orderBy": "desc",
  "limit": 100
}
```

#### Responses

##### Status: 200 検索成功

###### Content-Type: application/json

- **`data` (required)**

  `object` — 関連キーワード検索結果データ

  - **`items` (required)**

    `array` — 関連キーワードのリスト。各アイテムにキーワード・SEO指標を含む。

    **Items:**

    - **`keyword` (required)**

      `string` — 検索キーワードを元に取得した関連キーワード

    - **`metrics` (required)**

      `object` — SEO関連の各種指標（検索ボリューム・SEO難易度・CPC・競合性・出現時期）

      - **`competition` (required)**

        `object` — 広告競合性。0–100で表し、高いほど競合性が高い（0–33:低 / 34–66:中 / 67–100:高）。

      - **`cpc` (required)**

        `object` — 推定クリック単価（USD）

      - **`firstSeenRange` (required)**

        `object` — 出現時期。キーワードが最初にラッコキーワードデータベースで検出された時期を日付範囲ラベルで表す。不明な場合は null。

      - **`searchVolume` (required)**

        `object` — 月間検索数（年平均）

      - **`seoDifficulty` (required)**

        `object` — SEO難易度。1–100で表し、高いほど難易度が高い（1–33:低 / 34–66:中 / 67–100:高）。不明な場合は null。

  - **`query` (required)**

    `object` — リクエストで指定された検索クエリ情報

    - **`keyword` (required)**

      `string` — 関連キーワード取得の元になった検索キーワード

  - **`summary` (required)**

    `object` — 件数サマリー（全体件数とレスポンスに含まれる件数）

    - **`returnedCount` (required)**

      `number` — このレスポンスに含まれている件数

    - **`totalCount` (required)**

      `number` — 取得対象全体の件数

- **`errors` (required)**

  `array` — エラーメッセージの配列。正常時は空配列。

  **Items:**

  `string`

- **`meta` (required)**

  `object` — リクエストに関するメタ情報（課金・消費リソースなど）

  - **`consumedCredit` (required)**

    `number` — このリクエストで消費されたクレジット数。

- **`result` (required)**

  `boolean` — API 呼び出しの成否。正常時は true、エラー時は false。

**Example:**

```json
{
  "result": true,
  "meta": {
    "consumedCredit": 1.5
  },
  "data": {
    "query": {
      "keyword": "ラッコ"
    },
    "summary": {
      "totalCount": 150,
      "returnedCount": 100
    },
    "items": [
      {
        "keyword": "ラッコ 水族館",
        "metrics": {
          "seoDifficulty": 40,
          "searchVolume": 90500,
          "cpc": 0,
          "competition": 1,
          "firstSeenRange": "last_30_days"
        }
      }
    ]
  },
  "errors": []
}
```

##### Status: 400 バリデーションエラー

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "keyword is a required field"
  ]
}
```

##### Status: 402 クレジット不足

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Insufficient credits. Required: 1, Available: 0"
  ]
}
```

##### Status: 403 認証エラー

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Forbidden"
  ]
}
```

##### Status: 429 レート制限超過

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Rate limit exceeded. Please try again later."
  ]
}
```

##### Status: 500 Internal Server Error

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "500 Internal Server Error"
  ]
}
```

##### Status: 503 Service Unavailable - データベース接続エラーなど

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Service Unavailable"
  ]
}
```

### 潜在的な検索キーワード/質問（LSI/PAA）取得

- **Method:** `POST`
- **Path:** `/v1/other-keywords`
- **Tags:** 潜在的な検索キーワード/質問（LSI/PAA）取得

潜在的な検索キーワード/質問（LSI/PAA）取得。

そのキーワードで検索する人の次の検索行動・疑問を予測したり、SEO記事設計やユーザーニーズの深掘りに役立つ。

潜在的な検索キーワード（LSI・Google検索結果の「他の人はこちらも検索」に表示されるキーワード）と「他の人はこちらも質問」（PAA）を最大2階層まで、それぞれ数十件程度を再帰取得する。 LSIには月間検索数・SEO難易度・CPC・競合性などのSEO指標が付く。 SEO難易度はデータがない場合が多い。またSEO指標は最新でない可能性がある。 この指標を重要視する用途なら、データ取得後にPOST /v1/search-volumeで最新のSEO指標を取得すること。

importance(high/medium/low)は再帰取得中の出現回数で決まる。 多く登場するほど高く、Googleがより広く提示しているキーワード/質問であることを示す。

LSIはGoogleがそのキーワードを調べた人が次に調べると予測するキーワード。 PAAはGoogleがそのキーワードを調べた人が気になっていると予測する質問文・悩み。

importanceの高いLSI/PAAは、元の検索キーワードを調べる人にとって関心の高い・重要な事柄である可能性が高い。

関連語をより多く取得したい場合は POST /v1/suggest-keywords を使う。 指定キーワードを含む質問を大量に取得したい場合はPOST /v1/question-searchを使う。

1リクエストあたり22.5クレジットを消費。

#### Request Body

##### Content-Type: application/json

- **`keyword` (required)**

  `string` — 潜在的な検索キーワード（LSI）および関連する質問（PAA）を取得するための検索キーワード。1文字以上の文字列を指定する。

- **`orderBy`**

  `string`, possible values: `"asc", "desc"`, default: `"desc"` — ソート順。asc: 昇順 / desc: 降順。省略時は desc。

- **`sortBy`**

  `string`, possible values: `"importance", "seoDifficulty", "searchVolume", "cpc", "competition", "firstSeenRange"`, default: `"importance"` — 結果のソート項目。importance / seoDifficulty / searchVolume / cpc / competition / firstSeenRange。省略時は importance。

**Example:**

```json
{
  "keyword": "ラッコ",
  "sortBy": "importance",
  "orderBy": "desc"
}
```

#### Responses

##### Status: 200 検索成功

###### Content-Type: application/json

- **`data` (required)**

  `object` — 潜在的な検索キーワード/関連する質問の検索結果データ

  - **`items` (required)**

    `array` — LSI/PAA アイテムのリスト。LSI アイテムが先に、PAA アイテムが後に並ぶ。各アイテムに種別・重要度・取得元キーワードを含み、LSI の場合は SEO 指標も含まれる。

    **Items:**

    - **`importance` (required)**

      `string`, possible values: `"low", "medium", "high"` — 重要度。高いほど関連性や注目度が高いことを示す。high: 高 / medium: 中 / low: 低。

    - **`sourceKeyword` (required)**

      `string` — このキーワードまたは質問の取得元となったキーワード

    - **`type` (required)**

      `string`, possible values: `"lsi", "paa"` — データ種別。lsi: 潜在的な検索キーワード / paa: 関連する質問。

    - **`keyword`**

      `string` — 取得した潜在的な検索キーワード。type が lsi の場合に含まれる。

    - **`metrics`**

      `object` — SEO関連の各種指標。type が lsi の場合のみ含まれる。

      - **`competition` (required)**

        `object` — 広告競合性。0–100で表し、高いほど競合性が高い（0–33:低 / 34–66:中 / 67–100:高）。

      - **`cpc` (required)**

        `object` — 推定クリック単価（USD）

      - **`firstSeenRange` (required)**

        `object` — 出現時期。キーワードが最初にラッコキーワードデータベースで検出された時期を日付範囲ラベルで表す。不明な場合は null。

      - **`searchVolume` (required)**

        `object` — 月間検索数（年平均）

      - **`seoDifficulty` (required)**

        `object` — SEO難易度。1–100で表し、高いほど難易度が高い（1–33:低 / 34–66:中 / 67–100:高）。不明な場合は null。

    - **`question`**

      `string` — 取得した関連する質問。type が paa の場合に含まれる。

  - **`query` (required)**

    `object` — リクエストで指定された検索クエリ情報

    - **`keyword` (required)**

      `string` — 潜在的な検索キーワード/質問（LSI/PAA）取得の元になった検索キーワード

  - **`summary` (required)**

    `object` — LSI/PAA の件数サマリー

    - **`lsiCount` (required)**

      `number` — LSI（潜在的な検索キーワード）の件数

    - **`paaCount` (required)**

      `number` — PAA（People Also Ask / 関連する質問）の件数

- **`errors` (required)**

  `array` — エラーメッセージの配列。正常時は空配列。

  **Items:**

  `string`

- **`meta` (required)**

  `object` — リクエストに関するメタ情報（課金・消費リソースなど）

  - **`consumedCredit` (required)**

    `number` — このリクエストで消費されたクレジット数。

- **`result` (required)**

  `boolean` — API 呼び出しの成否。正常時は true、エラー時は false。

**Example:**

```json
{
  "result": true,
  "meta": {
    "consumedCredit": 22.5
  },
  "data": {
    "query": {
      "keyword": "ラッコ"
    },
    "summary": {
      "lsiCount": 1,
      "paaCount": 1
    },
    "items": [
      {
        "type": "lsi",
        "keyword": "ラッコ 水族館",
        "question": "ラッコはどこで見れますか？",
        "importance": "high",
        "sourceKeyword": "ラッコ",
        "metrics": {
          "seoDifficulty": 30,
          "searchVolume": 33100,
          "cpc": 2.17,
          "competition": 5,
          "firstSeenRange": "last_30_days"
        }
      }
    ]
  },
  "errors": []
}
```

##### Status: 400 バリデーションエラー

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "keyword is a required field"
  ]
}
```

##### Status: 402 クレジット不足

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Insufficient credits. Required: 1, Available: 0"
  ]
}
```

##### Status: 403 認証エラー

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Forbidden"
  ]
}
```

##### Status: 429 レート制限超過

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Rate limit exceeded. Please try again later."
  ]
}
```

##### Status: 500 Internal Server Error

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "500 Internal Server Error"
  ]
}
```

##### Status: 503 Service Unavailable - データベース接続エラーなど

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Service Unavailable"
  ]
}
```

### よくある質問検索取得

- **Method:** `POST`
- **Path:** `/v1/question-search`
- **Tags:** よくある質問検索

よくある質問検索。 指定キーワードを含む質問を、相対需要の高い順に最大1,000件取得する。 Q\&A/SEO記事作成や、AIO/GEO/LLMO対策の際、質問サンプルを得るのに有用。

ラッコキーワードのDBに蓄積した質問文を返却する。 ユーザーがGoogle検索AIモードや、チャットAIに入力する可能性の高い質問・疑問を網羅的に集められる。

相対需要は、その検索結果内で最も需要が高い質問を100とした1〜100の相対値。 絶対的な検索数や表示回数ではないため、検索キーワードが異なる結果の間では比較できない。

質問文・相対需要・出現時期での絞り込み（filter）と、相対需要・出現時期での並び替え（sortBy / orderBy）ができる。

そのキーワードを検索したときにGoogle検索結果に表示される質問を取得したい場合はPOST /v1/other-keywordsを使う。

1リクエストあたり1.5クレジットを消費。

#### Request Body

##### Content-Type: application/json

- **`keyword` (required)**

  `string` — よくある質問検索の元となる検索キーワード。1文字以上の文字列を指定する。

- **`filter`**

  `object` — 結果のフィルタリング条件。質問文・相対需要・出現時期などで絞り込む。

  - **`firstSeenRange`**

    `object` — 出現時期フィルタ

    - **`include`**

      `string`, possible values: `"last_7_days", "last_30_days", "last_90_days", "within_6_months", "within_1_year", "over_1_year"` — 出現時期の選択肢

  - **`keyword`**

    `object` — キーワードフィルタ（含む/含まない質問文の指定）

    - **`includes`**

      `array` — 含む単語のリスト（複数入力時はOR）

      **Items:**

      `string`

    - **`notIncludes`**

      `array` — 含まない単語のリスト（複数入力時はOR）

      **Items:**

      `string`

  - **`relativeDemand`**

    `object` — 相対需要フィルタ（1〜100の範囲指定）

    - **`max`**

      `integer` — 相対需要スコアの最大値

    - **`min`**

      `integer` — 相対需要スコアの最小値

- **`limit`**

  `integer`, default: `100` — 出力数の上限。1〜1000 の整数を指定。省略時は 100。

- **`orderBy`**

  `string`, possible values: `"asc", "desc"`, default: `"desc"` — ソート順。asc: 昇順 / desc: 降順。省略時は desc。

- **`sortBy`**

  `string`, possible values: `"relativeDemand", "firstSeenRange"`, default: `"relativeDemand"` — 結果のソート項目。relativeDemand: 相対需要 / firstSeenRange: 出現時期。省略時は relativeDemand。

**Example:**

```json
{
  "keyword": "ラッコ",
  "filter": {
    "keyword": {
      "includes": [
        "水族館"
      ],
      "notIncludes": [
        "グッズ"
      ]
    },
    "relativeDemand": {
      "min": 34,
      "max": 66
    },
    "firstSeenRange": {
      "include": "last_30_days"
    }
  },
  "sortBy": "relativeDemand",
  "orderBy": "desc",
  "limit": 100
}
```

#### Responses

##### Status: 200 検索成功

###### Content-Type: application/json

- **`data` (required)**

  `object` — よくある質問検索結果データ

  - **`items` (required)**

    `array` — 質問アイテムのリスト

    **Items:**

    - **`metrics` (required)**

      `object` — 質問の各種指標（相対需要・出現時期）

      - **`firstSeenRange` (required)**

        `object` — 出現時期。質問が最初にラッコキーワードデータベースで検出された時期を日付範囲ラベルで表す。不明な場合は null。

      - **`relativeDemand` (required)**

        `number` — 相対需要。検索結果内での相対的な需要の高さ（1〜100）。高いほどよく見られている質問。

    - **`question` (required)**

      `string` — 検索キーワードに関連する質問

  - **`query` (required)**

    `object` — 検索クエリ情報

    - **`keyword` (required)**

      `string` — よくある質問検索の元になった検索キーワード

    - **`limit` (required)**

      `integer` — リクエストで指定された出力数の上限

    - **`orderBy` (required)**

      `string`, possible values: `"asc", "desc"` — リクエストで指定されたソート順。asc: 昇順 / desc: 降順。

    - **`sortBy` (required)**

      `string`, possible values: `"relativeDemand", "firstSeenRange"` — リクエストで指定されたソート項目。relativeDemand: 相対需要 / firstSeenRange: 出現時期。

    - **`filter`**

      `object` — リクエストで指定された絞り込み条件（質問文・相対需要・出現時期）。指定がない場合は省略される。

      - **`firstSeenRange`**

        `object` — 出現時期フィルタ

        - **`include`**

          `string`, possible values: `"last_7_days", "last_30_days", "last_90_days", "within_6_months", "within_1_year", "over_1_year"` — 出現時期の選択肢

      - **`keyword`**

        `object` — キーワードフィルタ（含む/含まない質問文の指定）

        - **`includes`**

          `array` — 含む単語のリスト（複数入力時はOR）

          **Items:**

          `string`

        - **`notIncludes`**

          `array` — 含まない単語のリスト（複数入力時はOR）

          **Items:**

          `string`

      - **`relativeDemand`**

        `object` — 相対需要フィルタ（1〜100の範囲指定）

        - **`max`**

          `integer` — 相対需要スコアの最大値

        - **`min`**

          `integer` — 相対需要スコアの最小値

  - **`summary` (required)**

    `object` — 件数サマリー（全体件数とレスポンスに含まれる件数）

    - **`returnedCount` (required)**

      `number` — このレスポンスに含まれている件数

    - **`totalCount` (required)**

      `number` — 取得対象全体の件数

- **`errors` (required)**

  `array` — エラーメッセージの配列。正常時は空配列。

  **Items:**

  `string`

- **`meta` (required)**

  `object` — リクエストに関するメタ情報（課金・消費リソースなど）

  - **`consumedCredit` (required)**

    `number` — このリクエストで消費されたクレジット数

- **`result` (required)**

  `boolean` — API 呼び出しの成否。正常時は true、エラー時は false。

**Example:**

```json
{
  "result": true,
  "meta": {
    "consumedCredit": 1.5
  },
  "data": {
    "query": {
      "keyword": "ラッコ",
      "filter": {
        "keyword": {
          "includes": [
            "水族館"
          ],
          "notIncludes": [
            "グッズ"
          ]
        },
        "relativeDemand": {
          "min": 34,
          "max": 66
        },
        "firstSeenRange": {
          "include": "last_30_days"
        }
      },
      "sortBy": "relativeDemand",
      "orderBy": "desc",
      "limit": 100
    },
    "summary": {
      "totalCount": 150,
      "returnedCount": 100
    },
    "items": [
      {
        "question": "ラッコが絶滅しそうな理由は何ですか?",
        "metrics": {
          "relativeDemand": 87,
          "firstSeenRange": "last_30_days"
        }
      }
    ]
  },
  "errors": []
}
```

##### Status: 400 バリデーションエラー

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "keyword must be a string"
  ]
}
```

##### Status: 402 クレジット不足

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Insufficient credits. Required: 1, Available: 0"
  ]
}
```

##### Status: 403 認証エラー

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Forbidden"
  ]
}
```

##### Status: 429 レート制限超過

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Rate limit exceeded. Please try again later."
  ]
}
```

##### Status: 500 Internal Server Error

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "500 Internal Server Error"
  ]
}
```

##### Status: 503 Service Unavailable - データベース接続エラーなど

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Service Unavailable"
  ]
}
```

### 同時ランクインキーワード取得

- **Method:** `POST`
- **Path:** `/v1/ranking-keywords`
- **Tags:** 同時ランクインキーワード

同時ランクインキーワード取得。 Google検索上位ページが他にSEO流入を獲得しているKWを抽出。検索意図の近い・遠い関連語の発見に使う。

最大5,000件取得。月間検索数・SEO難易度・CPC・競合性などのSEO指標付き。

SEO指標は最新でない可能性がある。 この指標を重要視する用途なら、データ取得後にPOST /v1/search-volumeで最新のSEO指標を取得すること。

指定キーワードで上位ページ群がランクインしている他のキーワードを集めるため、検索意図の近いキーワードを発見できる。 searchTop / searchRangeの範囲を狭めれば検索意図の近いキーワードが、広げれば検索意図の遠いキーワードの抽出が行える。

relevanceの高いキーワードは、検索意図が近い可能性が高いため、一つのSEO記事で同時にGoogle上位ランクインを狙える可能性が高い。 relevanceの低いキーワードは、検索意図が遠い可能性が高いため、元キーワードとは別の記事として上位ランクインを狙うことが望ましい。 searchTop / searchRangeを広げると、relevanceの低いキーワードを多く取得できるため、既存キーワードと被りづらい・新しいキーワードを発見するのに役立つ。

特定サイト・ページがSEO流入を獲得しているキーワードを調べたい場合は POST /v1/influx-keywords を、 より多くの関連語を取得したい場合は POST /v1/suggest-keywords を使う。

1リクエストあたり4.5クレジットを消費。

#### Request Body

##### Content-Type: application/json

- **`keyword` (required)**

  `string` — 同時ランクインキーワード取得の元となる検索キーワード。指定キーワードの検索上位URLが他にランクインしているキーワードを取得する。1文字以上の文字列を指定する。

- **`filter`**

  `object` — 結果のフィルタリング条件。キーワード・SEO難易度・月間検索数・CPC・競合性・関連度で絞り込む。

  - **`competition`**

    `object` — 競合性フィルタ（0〜100の範囲指定）

    - **`max`**

      `integer` — 最大値

    - **`min`**

      `integer` — 最小値

  - **`cpc`**

    `object` — クリック単価（CPC）フィルタ（USD、範囲指定）

    - **`max`**

      `number` — 最大CPC

    - **`min`**

      `number` — 最小CPC

  - **`keyword`**

    `object` — キーワードフィルタ

    - **`includes`**

      `array` — 含む単語のリスト（複数入力時はOR）

      **Items:**

      `string`

    - **`notIncludes`**

      `array` — 含まない単語のリスト（複数入力時はOR）

      **Items:**

      `string`

  - **`relevance`**

    `object` — 関連度フィルタ（0〜100の範囲指定）

    - **`max`**

      `integer` — 最大値

    - **`min`**

      `integer` — 最小値

  - **`searchVolume`**

    `object` — 月間検索数フィルタ（範囲指定）

    - **`max`**

      `integer` — 最大値

    - **`min`**

      `integer` — 最小値

  - **`seoDifficulty`**

    `object` — SEO難易度フィルタ（0〜100の範囲指定）

    - **`max`**

      `integer` — 最大値

    - **`min`**

      `integer` — 最小値

- **`limit`**

  `integer`, default: `500` — 取得件数。1〜5000 の整数を指定する。省略時は 500。

- **`orderBy`**

  `string`, possible values: `"asc", "desc"`, default: `"desc"` — ソート順。asc: 昇順 / desc: 降順。省略時は desc。

- **`searchRange`**

  `object`, default: `50` — 検索順位範囲。searchTopで指定したページがGoogle上位表示できているキーワードのうち、この順位以内にランクインしているキーワードを対象にする。選択肢: 10 / 20 / 30 / 50 / 100。省略時は 50。

- **`searchTop`**

  `object`, default: `20` — 検索上位ページの参照数。そのキーワードでGoogle検索で上位表示できているページのうち、上位何件のURLを同時ランクイン判定に使用するかを指定する。選択肢: 3 / 5 / 10 / 20 / 30 / 50。省略時は 20。値を大きくすると、Google検索でより下位にランクインしている＝検索意図とズレのより大きいページが調査対象となる。値を小さくすると、Google検索でより上位にランクインしている＝検索意図に一致するページのみが調査対象となる。

- **`sortBy`**

  `string`, possible values: `"seoDifficulty", "searchVolume", "cpc", "competition", "relevance"`, default: `"relevance"` — 結果のソート項目。seoDifficulty / searchVolume / cpc / competition / relevance。省略時は relevance。

**Example:**

```json
{
  "keyword": "ラッコ",
  "searchTop": 20,
  "searchRange": 50,
  "filter": {
    "keyword": {
      "includes": [
        "水族館"
      ],
      "notIncludes": [
        "グッズ"
      ]
    },
    "seoDifficulty": {
      "min": 1,
      "max": 100
    },
    "searchVolume": {
      "min": 100,
      "max": 10000
    },
    "cpc": {
      "min": 0.5,
      "max": 10
    },
    "competition": {
      "min": 1,
      "max": 100
    },
    "relevance": {
      "min": 1,
      "max": 100
    }
  },
  "sortBy": "relevance",
  "orderBy": "desc",
  "limit": 500
}
```

#### Responses

##### Status: 200 検索成功

###### Content-Type: application/json

- **`data` (required)**

  `object` — 同時ランクインキーワード検索結果データ

  - **`items` (required)**

    `array` — 同時ランクインキーワード結果のリスト。各アイテムにキーワード・単語数・SEO指標を含む。

    **Items:**

    - **`keyword` (required)**

      `string` — 同時ランクインしているキーワード

    - **`metrics` (required)**

      `object` — SEO関連の各種指標（SEO難易度・月間検索数・CPC・競合性・関連度）

      - **`competition` (required)**

        `number` — 広告競合性。0–100で表し、高いほど競合性が高い（0–33:低 / 34–66:中 / 67–100:高）。

      - **`cpc` (required)**

        `number` — 推定クリック単価（USD）

      - **`relevance` (required)**

        `number` — 同時ランクイン度。1–100で表し、高いほど元キーワードと検索結果の重複度が高いことを示す。

      - **`searchVolume` (required)**

        `number` — 月間検索数（年平均）

      - **`seoDifficulty` (required)**

        `object` — SEO難易度。1–100で表し、高いほど難易度が高い（1–33:低 / 34–66:中 / 67–100:高）。不明な場合は null。

    - **`wordCount` (required)**

      `number` — キーワードのスペース区切りの単語数

  - **`query` (required)**

    `object` — リクエストで指定された検索クエリ情報

    - **`keyword` (required)**

      `string` — 同時ランクインキーワード取得の元になった検索キーワード

  - **`summary` (required)**

    `object` — 件数サマリー（全体件数とレスポンスに含まれる件数）

    - **`returnedCount` (required)**

      `number` — このレスポンスに含まれている件数

    - **`totalCount` (required)**

      `number` — 取得対象全体の件数

- **`errors` (required)**

  `array` — エラーメッセージの配列。正常時は空配列。

  **Items:**

  `string`

- **`meta` (required)**

  `object` — リクエストに関するメタ情報（課金・消費リソースなど）

  - **`consumedCredit` (required)**

    `number` — このリクエストで消費されたクレジット数。

- **`result` (required)**

  `boolean` — API 呼び出しの成否。正常時は true、エラー時は false。

**Example:**

```json
{
  "result": true,
  "meta": {
    "consumedCredit": 4.5
  },
  "data": {
    "query": {
      "keyword": "ラッコ"
    },
    "summary": {
      "totalCount": 150,
      "returnedCount": 100
    },
    "items": [
      {
        "keyword": "ラッコ 水族館",
        "wordCount": 2,
        "metrics": {
          "seoDifficulty": 30,
          "searchVolume": 10000,
          "cpc": 0.5,
          "competition": 32,
          "relevance": 5
        }
      }
    ]
  },
  "errors": []
}
```

##### Status: 400 バリデーションエラー

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "keyword should not be empty"
  ]
}
```

##### Status: 402 クレジット不足

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Insufficient credits. Required: 1, Available: 0"
  ]
}
```

##### Status: 403 認証エラー

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Forbidden"
  ]
}
```

##### Status: 429 レート制限超過

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Rate limit exceeded. Please try again later."
  ]
}
```

##### Status: 500 Internal Server Error

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "500 Internal Server Error"
  ]
}
```

##### Status: 503 Service Unavailable - データベース接続エラーなど

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Service Unavailable"
  ]
}
```

### 一括キーワード調査登録

- **Method:** `POST`
- **Path:** `/v1/search-volume`
- **Tags:** 一括キーワード調査

一括キーワード調査登録。 キーワードリストを渡すと非同期で月間検索数・SEO難易度・CPC・競合性などを調査開始する。

処理はバックグラウンドで行われるため、以下の手順で結果を取得すること:

1. 戻り値の requestId を控える
2. GET /v1/search-volume/{requestId}/status で完了を待つ（ポーリング推奨: 初回は30秒後、以降30秒間隔）
3. isCompleted=true になったら POST /v1/search-volume/{requestId}/results で結果を取得する

通常は10秒程度で取得完了するが、seoDifficultyをONにした場合、最大60分程度時間がかかる。 ONの場合、一定の時間が経過してから処理ステータスを確認することを推奨する。 SEO以外の目的で当機能を使う場合、時短のためseoDifficultyをOFFすることを推奨する。

1キーワードあたり0.03クレジットを消費。seoDifficultyがONの場合、追加で1キーワードあたり0.75クレジットを消費する。ただし、1リクエストの消費クレジットの合計が15クレジットに満たない場合は15クレジットを消費する。

#### Request Body

##### Content-Type: application/json

- **`keywords` (required)**

  `array` — キーワード（入力上限50,000件）

  **Items:**

  `string`

- **`aggregationPeriodMonths`**

  `object`, default: `12` — 集計期間（月数）。12/24/36/48 のいずれか。省略時は 12。

- **`dataCompletion`**

  `boolean`, default: `true` — データ補完フラグ。true の場合にデータ補完を行う。省略時は true。

- **`deduplicate`**

  `boolean`, default: `true` — キーワードの重複除去を行うかどうか。省略時は true。

- **`language`**

  `string`, default: `"Japanese"` — 言語名。指定可能な言語名は metadata の languages 一覧を参照。省略時は Japanese。

- **`location`**

  `string`, default: `"Japan"` — 地域名。省略時は Japan。 - 指定可能な地域名は metadata の locations 一覧を参照（一覧は国レベルのみ） - 市区町村レベルの地域も指定可能。「市区町村名,上位地域名,国名」のようにカンマ区切りの正式名で指定する（例: Shibuya,Tokyo,Japan） - 途中の階層のみ（例: 都道府県のみ）の指定は未サポート

- **`seoDifficulty`**

  `boolean`, default: `false` — SEO難易度取得フラグ。true の場合にSEO難易度を取得する。省略時は false。

**Example:**

```json
{
  "keywords": [
    "ラッコ",
    "カワウソ"
  ],
  "seoDifficulty": false,
  "dataCompletion": true,
  "location": "Japan",
  "language": "Japanese",
  "deduplicate": true,
  "aggregationPeriodMonths": 12
}
```

#### Responses

##### Status: 201 登録成功

###### Content-Type: application/json

- **`data` (required)**

  `object` — 履歴登録結果

  - **`requestId`**

    `number` — リクエストID

- **`errors` (required)**

  `array` — エラーメッセージの配列。正常時は空配列。

  **Items:**

  `string`

- **`meta` (required)**

  `object` — リクエストに関するメタ情報（課金・消費リソースなど）

  - **`consumedCredit` (required)**

    `number` — このリクエストで消費されたクレジット数。

- **`result` (required)**

  `boolean` — API 呼び出しの成否。正常時は true、エラー時は false。

**Example:**

```json
{
  "result": true,
  "meta": {
    "consumedCredit": 10
  },
  "data": {
    "requestId": 1234567
  },
  "errors": []
}
```

##### Status: 400 バリデーションエラー

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "keywords is a required field"
  ]
}
```

##### Status: 402 クレジット不足

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Insufficient credits. Required: 1, Available: 0"
  ]
}
```

##### Status: 403 認証エラー

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Forbidden"
  ]
}
```

##### Status: 429 レート制限超過

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Rate limit exceeded. Please try again later."
  ]
}
```

##### Status: 500 Internal Server Error

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "500 Internal Server Error"
  ]
}
```

##### Status: 503 Service Unavailable - データベース接続エラーなど

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Service Unavailable"
  ]
}
```

### 一括キーワード調査履歴一覧取得

- **Method:** `GET`
- **Path:** `/v1/search-volume/histories`
- **Tags:** 一括キーワード調査

一括キーワード調査履歴一覧取得。一括キーワード調査の過去リクエスト履歴一覧を取得する。取得結果から requestId を取り出し、GET /v1/search-volume/{requestId}/status で完了確認 / POST /v1/search-volume/{requestId}/results で結果取得が可能。

- ソート順: createdAt 降順固定(新しい順)
- 全体ステータス (status) は searchVolume と seoDifficulty の両方が processed なら completed(seoDifficulty=skip も完了扱い)。noiseReduction は判定対象外。

クレジットは消費しない。

#### Parameters

##### `limit`

- **In:** `query`

取得件数。1〜100の整数を指定する。省略時は 100。

`number`, default: `100`

##### `offset`

- **In:** `query`

取得開始位置。0以上の整数を指定する。省略時は 0。offset + limit が 50000 を超える指定は不可。

`number`, default: `0`

##### `status`

- **In:** `query`

ステータスフィルタ。completed: 全処理完了 / processing: 処理中。省略時は全件取得。

`string`, possible values: `"completed", "processing"`

#### Responses

##### Status: 200 取得成功

###### Content-Type: application/json

- **`data` (required)**

  `object` — 一括キーワード調査履歴一覧データ

  - **`items` (required)**

    `array` — 一括キーワード調査履歴アイテムのリスト

    **Items:**

    - **`aggregationPeriodMonths` (required)**

      `number` — 集計期間（月数）

    - **`completedAt` (required)**

      `object` — 全処理完了日時（ISO 8601、UTC）。未完了時は null。

    - **`createdAt` (required)**

      `string`, format: `date-time` — リクエスト作成日時（ISO 8601、UTC）

    - **`dataCompletion` (required)**

      `boolean` — データ補完が有効かどうか

    - **`keywordCount` (required)**

      `number` — キーワードの件数

    - **`keywordSummary` (required)**

      `string` — キーワードのサマリ（カンマ区切り、先頭20件・255文字以内で切り詰め）

    - **`language` (required)**

      `string` — 言語名。Google Ads API の LanguageCriterion に準拠。

    - **`location` (required)**

      `string` — 地域名。Google Ads API の LocationCriterion に準拠。

    - **`requestId` (required)**

      `number` — リクエストID

    - **`seoDifficulty` (required)**

      `boolean` — SEO難易度取得が有効かどうか

    - **`status` (required)**

      `string`, possible values: `"completed", "processing"` — 全体ステータス。statuses の searchVolume と seoDifficulty の両方が processed の場合に completed（seoDifficulty が skip の場合も完了扱い）、それ以外は processing。noiseReduction は判定対象外。

    - **`statuses` (required)**

      `object` — 各処理のステータス情報

      - **`noiseReduction` (required)**

        `string`, possible values: `"unprocessed", "processing", "processed"` — ノイズ除去ステータス。unprocessed: 未処理 / processing: 処理中 / processed: 完了。ノイズ除去には時間がかかる可能性があります。

      - **`searchVolume` (required)**

        `string`, possible values: `"unprocessed", "processing", "processed"` — 月間検索数取得ステータス。unprocessed: 未処理 / processing: 処理中 / processed: 完了。

      - **`seoDifficulty` (required)**

        `string`, possible values: `"skip", "unprocessed", "processing", "processed"` — SEO難易度取得ステータス。unprocessed: 未処理 / processing: 処理中 / processed: 完了 / skip: スキップ（SEO難易度取得OFFの場合）。

  - **`query` (required)**

    `object` — リクエストで指定されたクエリパラメータ

    - **`limit` (required)**

      `number` — リクエストで指定された取得件数

    - **`offset` (required)**

      `number` — リクエストで指定された取得開始位置

    - **`status` (required)**

      `object` — リクエストで指定されたステータスフィルタ

  - **`summary` (required)**

    `object` — 件数サマリ

    - **`returnedCount` (required)**

      `number` — このレスポンスに含まれている件数

    - **`totalCount` (required)**

      `number` — 取得対象全体の件数

- **`errors` (required)**

  `array` — エラーメッセージの配列。正常時は空配列。

  **Items:**

  `string`

- **`meta` (required)**

  `object` — リクエストに関するメタ情報（課金・消費リソースなど）

  - **`consumedCredit` (required)**

    `number` — このリクエストで消費されたクレジット数。

- **`result` (required)**

  `boolean` — API 呼び出しの成否。正常時は true、エラー時は false。

**Example:**

```json
{
  "result": true,
  "meta": {
    "consumedCredit": 0
  },
  "data": {
    "query": {
      "limit": 100,
      "offset": 0,
      "status": null
    },
    "summary": {
      "totalCount": 1,
      "returnedCount": 1
    },
    "items": [
      {
        "requestId": 1500,
        "createdAt": "2026-05-31T01:00:00.000Z",
        "completedAt": null,
        "status": "processing",
        "statuses": {
          "searchVolume": "processed",
          "seoDifficulty": "unprocessed",
          "noiseReduction": "processing"
        },
        "keywordSummary": "ラッコ,カワウソ",
        "keywordCount": 2,
        "seoDifficulty": true,
        "location": "Japan",
        "language": "Japanese",
        "aggregationPeriodMonths": 12,
        "dataCompletion": true
      }
    ]
  },
  "errors": []
}
```

##### Status: 400 バリデーションエラー

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Invalid query parameters"
  ]
}
```

##### Status: 403 認証失敗

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Forbidden"
  ]
}
```

##### Status: 429 レート制限超過

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Rate limit exceeded. Please try again later."
  ]
}
```

##### Status: 500 Internal Server Error

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "500 Internal Server Error"
  ]
}
```

### 一括キーワード調査ステータス取得

- **Method:** `GET`
- **Path:** `/v1/search-volume/{requestId}/status`
- **Tags:** 一括キーワード調査

一括キーワード調査処理ステータス確認。POST /v1/search-volume で登録した一括キーワード調査の処理ステータスを確認する。 isCompleted が true になるまでポーリングすること（推奨間隔: 30秒）。 isCompleted=true になったら POST /v1/search-volume/{requestId}/results で結果を取得できる。

何回かステータスをチェックしてもisCompletedがtrueにならない場合は一定の時間が経ってから再度結果をチェックすることを推奨する。 （利用が混雑している場合は、取得完了まで数時間以上時間がかかるケースがあるため）

クレジットは消費しない。

#### Parameters

##### `requestId` required

- **In:** `path`

POST /v1/search-volume で取得したリクエストID

`number`

#### Responses

##### Status: 200 ステータス取得成功

###### Content-Type: application/json

- **`data` (required)**

  `object` — ステータス情報

  - **`isCompleted`**

    `boolean` — 全処理完了フラグ。searchVolume が processed かつ seoDifficulty が processed または skip の場合に true。noiseReduction は判定対象外。

  - **`statuses`**

    `object` — 各処理のステータス情報

    - **`noiseReduction`**

      `string`, possible values: `"unprocessed", "processing", "processed"` — ノイズ除去ステータス。unprocessed: 未処理 / processing: 処理中 / processed: 完了。ノイズ除去には時間がかかる可能性があります。

    - **`searchVolume`**

      `string`, possible values: `"unprocessed", "processing", "processed"` — 月間検索数取得ステータス。unprocessed: 未処理 / processing: 処理中 / processed: 完了。

    - **`seoDifficulty`**

      `string`, possible values: `"skip", "unprocessed", "processing", "processed"` — SEO難易度取得ステータス。unprocessed: 未処理 / processing: 処理中 / processed: 完了 / skip: スキップ。

- **`errors` (required)**

  `array` — エラーメッセージの配列。正常時は空配列。

  **Items:**

  `string`

- **`meta` (required)**

  `object` — リクエストに関するメタ情報（課金・消費リソースなど）

  - **`consumedCredit` (required)**

    `number` — このリクエストで消費されたクレジット数。

- **`result` (required)**

  `boolean` — API 呼び出しの成否。正常時は true、エラー時は false。

**Example:**

```json
{
  "result": true,
  "meta": {
    "consumedCredit": 0
  },
  "data": {
    "isCompleted": true,
    "statuses": {
      "searchVolume": "processed",
      "noiseReduction": "processing",
      "seoDifficulty": "skip"
    }
  },
  "errors": []
}
```

##### Status: 400 バリデーションエラー

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Invalid requestId"
  ]
}
```

##### Status: 403 認証失敗

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Forbidden"
  ]
}
```

##### Status: 429 レート制限超過

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Rate limit exceeded. Please try again later."
  ]
}
```

##### Status: 500 Internal Server Error

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "500 Internal Server Error"
  ]
}
```

### 一括キーワード調査データ取得

- **Method:** `POST`
- **Path:** `/v1/search-volume/{requestId}/results`
- **Tags:** 一括キーワード調査

一括キーワード調査結果取得。POST /v1/search-volume で登録した一括キーワード調査の結果を取得する。事前に GET /v1/search-volume/{requestId}/status で処理完了を確認してから呼び出す。フィルタ・ソート・件数制限が可能。

クレジットは消費しない。

#### Parameters

##### `requestId` required

- **In:** `path`

POST /v1/search-volume で取得したリクエストID

`number`

#### Request Body

##### Content-Type: application/json

- **`filter`**

  `object` — 結果のフィルタリング条件。キーワード・SEO難易度・月間検索数・CPC・競合性で絞り込む。

  - **`competition`**

    `object` — 競合性フィルタ（0〜100の範囲指定）

    - **`max`**

      `integer` — 最大値

    - **`min`**

      `integer` — 最小値

  - **`cpc`**

    `object` — CPC（$）フィルタ（範囲指定）

    - **`max`**

      `number` — 最大CPC

    - **`min`**

      `number` — 最小CPC

  - **`firstSeenRange`**

    `object` — 出現時期フィルタ

    - **`include`**

      `string`, possible values: `"last_7_days", "last_30_days", "last_90_days", "within_6_months", "within_1_year", "over_1_year"` — 出現時期の選択肢

  - **`keyword`**

    `object` — キーワードフィルタ（含む/含まないキーワード指定）

    - **`includes`**

      `array` — 含む単語のリスト（複数入力時はOR）

      **Items:**

      `string`

    - **`notIncludes`**

      `array` — 含まない単語のリスト（複数入力時はOR）

      **Items:**

      `string`

  - **`searchVolume`**

    `object` — 月間検索数フィルタ（範囲指定）

    - **`max`**

      `integer` — 最大値

    - **`min`**

      `integer` — 最小値

  - **`seoDifficulty`**

    `object` — SEO難易度フィルタ（0〜100の範囲指定）

    - **`max`**

      `integer` — 最大値

    - **`min`**

      `integer` — 最小値

- **`limit`**

  `integer`, default: `100` — 取得件数。1〜50,000の整数を指定する。省略時は 100。

- **`noiseReduction`**

  `boolean`, default: `true` — ノイズ除去フラグ。true の場合にノイズ除去を適用する。省略時は true。

- **`orderBy`**

  `string`, possible values: `"asc", "desc"`, default: `"desc"` — ソート順。asc: 昇順 / desc: 降順。省略時は desc。

- **`sortBy`**

  `string`, possible values: `"keyword", "seoDifficulty", "searchVolume", "rateOfChange", "cpc", "competition", "firstSeenRange"`, default: `"searchVolume"` — ソート項目。keyword / seoDifficulty / searchVolume / rateOfChange / cpc / competition / firstSeenRange。省略時は searchVolume。

**Example:**

```json
{
  "noiseReduction": true,
  "filter": {
    "keyword": {
      "includes": [
        "水族館"
      ],
      "notIncludes": [
        "グッズ"
      ]
    },
    "seoDifficulty": {
      "min": 1,
      "max": 100
    },
    "searchVolume": {
      "min": 100,
      "max": 10000
    },
    "cpc": {
      "min": 0.5,
      "max": 10
    },
    "competition": {
      "min": 1,
      "max": 100
    },
    "firstSeenRange": {
      "include": "last_30_days"
    }
  },
  "sortBy": "searchVolume",
  "orderBy": "desc",
  "limit": 100
}
```

#### Responses

##### Status: 200 データ取得成功

###### Content-Type: application/json

- **`data` (required)**

  `object` — 検索ボリューム結果データ

  - **`items`**

    `array` — 検索結果アイテムのリスト

    **Items:**

    - **`dataSource` (required)**

      `object` — 検索数データの取得元。取得できなかった場合は null。

    - **`keyword` (required)**

      `string` — キーワード

    - **`metrics` (required)**

      `object` — 各種指標（SEO難易度・月間検索数・CPC・広告競合性）

      - **`competition` (required)**

        `object` — 広告競合性。0–100で表し、高いほど競合性が高い。（0–33:低 / 34–66:中 / 67–100:高） 無効な場合は null。

      - **`cpc` (required)**

        `object` — 推定クリック単価（USD）。無効な場合は null。

      - **`firstSeenRange` (required)**

        `object` — 出現時期。キーワードが最初にラッコキーワードデータベースで検出された時期を日付範囲ラベルで表す。不明な場合は null。

      - **`searchVolume` (required)**

        `object` — 月間検索数（年平均）。無効な場合は null。

      - **`seoDifficulty` (required)**

        `object` — SEO難易度。1–100で表し、高いほど難易度が高い。（1–33:低 / 34–66:中 / 67–100:高）不明な場合は null。

    - **`trends` (required)**

      `object` — 検索数トレンド（増減率・月別検索数）

      - **`changeRate` (required)**

        `object` — 検索数の増減率（3か月・6か月・12か月）

        - **`12m` (required)**

          `object` — 直近12か月（直近月を含む）の平均に対する直近月の検索数増減率。集計期間に関わらず固定12か月。パーセントではなく比率（0.1 = +10%、1.0 = +100%）。12か月分のデータが無い場合は null。対象期間の検索数がすべて0の場合は0。

        - **`3m` (required)**

          `object` — 直近3か月（直近月を含む）の平均に対する直近月の検索数増減率。集計期間に関わらず固定3か月。パーセントではなく比率（0.1 = +10%、1.0 = +100%）。3か月分のデータが無い場合は null。対象期間の検索数がすべて0の場合は0。

        - **`6m` (required)**

          `object` — 直近6か月（直近月を含む）の平均に対する直近月の検索数増減率。集計期間に関わらず固定6か月。パーセントではなく比率（0.1 = +10%、1.0 = +100%）。6か月分のデータが無い場合は null。対象期間の検索数がすべて0の場合は0。

        - **`yoy1y` (required)**

          `object` — 1年前同月比（集計期間24か月以上で算出）

        - **`yoy2y` (required)**

          `object` — 2年前同月比（集計期間36か月以上で算出）

        - **`yoy3y` (required)**

          `object` — 3年前同月比（集計期間48か月以上で算出）

      - **`monthlySearchVolume` (required)**

        `object` — 月ごとの検索数。キーは YYYY-MM 形式。データがない場合は null。

  - **`query`**

    `object` — クエリ情報（リクエストID・地域・言語）

    - **`aggregationPeriodMonths` (required)**

      `number` — 集計期間（月数）

    - **`language` (required)**

      `string` — 月間検索数取得対象の言語

    - **`location` (required)**

      `string` — 月間検索数取得対象の地域

    - **`requestId` (required)**

      `number` — リクエストID

  - **`summary`**

    `object` — 件数サマリー

    - **`returnedCount` (required)**

      `number` — このレスポンスに含まれている件数

    - **`totalCount` (required)**

      `number` — 取得対象全体の件数

- **`errors` (required)**

  `array` — エラーメッセージの配列。正常時は空配列。

  **Items:**

  `string`

- **`meta` (required)**

  `object` — リクエストに関するメタ情報（課金・消費リソースなど）

  - **`consumedCredit` (required)**

    `number` — このリクエストで消費されたクレジット数。

- **`result` (required)**

  `boolean` — API 呼び出しの成否。正常時は true、エラー時は false。

**Example:**

```json
{
  "result": true,
  "meta": {
    "consumedCredit": 0
  },
  "data": {
    "query": {
      "requestId": 1234567,
      "location": "Japan",
      "language": "Japanese",
      "aggregationPeriodMonths": 12
    },
    "summary": {
      "totalCount": 150,
      "returnedCount": 100
    },
    "items": [
      {
        "keyword": "ラッコ",
        "dataSource": "GoogleLive",
        "metrics": {
          "seoDifficulty": 40,
          "searchVolume": 90500,
          "cpc": 0,
          "competition": 1,
          "firstSeenRange": "last_30_days"
        },
        "trends": {
          "changeRate": {
            "12m": 0.4159,
            "6m": 0.0796,
            "3m": -0.0695,
            "yoy1y": 0.1523,
            "yoy2y": -0.0845,
            "yoy3y": 0.2311
          },
          "monthlySearchVolume": {
            "2025-01": 2740000,
            "2025-02": 2240000
          }
        }
      }
    ]
  },
  "errors": []
}
```

##### Status: 400 バリデーションエラー

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Invalid request parameters"
  ]
}
```

##### Status: 403 認証失敗

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Forbidden"
  ]
}
```

##### Status: 429 レート制限超過

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Rate limit exceeded. Please try again later."
  ]
}
```

##### Status: 500 Internal Server Error

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "500 Internal Server Error"
  ]
}
```

### 地域一覧取得

- **Method:** `GET`
- **Path:** `/v1/metadata/locations`
- **Tags:** メタデータ

対応地域一覧取得。POST /v1/search-rank / POST /v1/search-volume の location パラメータに指定可能な地域名の一覧を取得する。未対応の地域名でエラーになった場合、この一覧から正しい地域名を確認する。一覧が多い場合は locationName（地域名・部分一致）/ countryCode（ISO 3166-1 alpha-2 国コード・完全一致）で絞り込める（複数指定時はAND条件）。フィルタ未指定時は国レベルのみ、フィルタ指定時は国レベルに加え市区町村レベルの地域も返る。

クレジットは消費しない。認証は不要。

#### Parameters

##### `locationName`

- **In:** `query`

地域名での絞り込み（部分一致・大文字小文字無視）。指定すると国レベルに加え市区町村など下位レベルの地域も返却する。省略可。

`string`

##### `countryCode`

- **In:** `query`

ISO 3166-1 alpha-2 国コードでの絞り込み（完全一致・大文字小文字無視 例: JP）。指定すると国レベルに加え市区町村など下位レベルの地域も返却する。locationName と併用した場合はAND条件。省略可。

`string`

##### `limit`

- **In:** `query`

取得件数。正の整数を指定。省略時は全件取得。

`number`

#### Responses

##### Status: 200 取得成功

###### Content-Type: application/json

- **`data` (required)**

  `object` — 地域一覧

  - **`locations` (required)**

    `array` — 指定可能な地域の一覧（フィルタ未指定時は国レベルのみ・フィルタ指定時は市区町村レベルも含む）

    **Items:**

    - **`countryIsoCode` (required)**

      `string` — ISO 3166-1 alpha-2 国コード

    - **`name` (required)**

      `string` — 地域名

- **`errors` (required)**

  `array` — エラーメッセージの配列。正常時は空配列。

  **Items:**

  `string`

- **`meta` (required)**

  `object` — リクエストに関するメタ情報（課金・消費リソースなど）

  - **`consumedCredit` (required)**

    `number` — このリクエストで消費されたクレジット数。無料ツールのため常に 0。

- **`result` (required)**

  `boolean` — API 呼び出しの成否。正常時は true、エラー時は false。

**Example:**

```json
{
  "result": true,
  "meta": {
    "consumedCredit": 0
  },
  "data": {
    "locations": [
      {
        "name": "Japan",
        "countryIsoCode": "JP"
      }
    ]
  },
  "errors": []
}
```

##### Status: 400 バリデーションエラー

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "countryCode must be an ISO 3166-1 alpha-2 code (two letters)"
  ]
}
```

##### Status: 500 Internal Server Error

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "500 Internal Server Error"
  ]
}
```

##### Status: 503 Service Unavailable - データベース接続エラーなど

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Service Unavailable"
  ]
}
```

### 言語一覧取得

- **Method:** `GET`
- **Path:** `/v1/metadata/languages`
- **Tags:** メタデータ

対応言語一覧取得。POST /v1/search-rank / POST /v1/search-volume の language パラメータに指定可能な言語名の一覧を取得する。未対応の言語名でエラーになった場合、この一覧から正しい言語名を確認する。言語名は取得した一覧の値（例: Japanese）をそのまま指定する。件数が限られるため絞り込みフィルタは持たない。

クレジットは消費しない。認証は不要。

#### Responses

##### Status: 200 取得成功

###### Content-Type: application/json

- **`data` (required)**

  `object` — 言語一覧

  - **`languages` (required)**

    `array` — 指定可能な言語の一覧

    **Items:**

    - **`name` (required)**

      `string` — 言語名

- **`errors` (required)**

  `array` — エラーメッセージの配列。正常時は空配列。

  **Items:**

  `string`

- **`meta` (required)**

  `object` — リクエストに関するメタ情報（課金・消費リソースなど）

  - **`consumedCredit` (required)**

    `number` — このリクエストで消費されたクレジット数。無料ツールのため常に 0。

- **`result` (required)**

  `boolean` — API 呼び出しの成否。正常時は true、エラー時は false。

**Example:**

```json
{
  "result": true,
  "meta": {
    "consumedCredit": 0
  },
  "data": {
    "languages": [
      {
        "name": "Japanese"
      }
    ]
  },
  "errors": []
}
```

##### Status: 500 Internal Server Error

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "500 Internal Server Error"
  ]
}
```

##### Status: 503 Service Unavailable - データベース接続エラーなど

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Service Unavailable"
  ]
}
```

### 獲得キーワード調査取得

- **Method:** `POST`
- **Path:** `/v1/influx-keywords`
- **Tags:** 獲得キーワード調査

獲得キーワード調査。 指定ドメイン/URLがGoogleからSEO流入を獲得しているキーワードを取得。競合のSEO調査・自サイト分析に使う。

最大10,000件取得。 対象サイト・ページが各キーワードで何位にランクインし、どれだけ推定流入数を得ているかを返す。

検索順位やSEO指標は最新でない可能性がある。 この指標を重要視する用途なら、データ取得後にPOST /v1/search-volumeで最新のSEO指標を取得するか、POST /v1/search-rankで最新の検索順位を取得すること。

競合がSEO流入を獲得している主要なキーワードを調査するのに有用。 また、コンテンツギャップの調査にも役立つ。 自サイト・競合サイトのデータを比較することで、 競合がランクインしているが、自サイトがランクインできていないキーワードを把握できる。

ページ単位で集計したい場合は POST /v1/influx-pages を、 指定ドメインの競合サイトを抽出したい場合はPOST /v1/competitiveを使う。

1リクエストあたり4.5クレジットを消費。

#### Request Body

##### Content-Type: application/json

- **`targets` (required)**

  `array` — 獲得キーワード調査の対象ドメインまたはURLとマッチタイプの配列。最大20件まで指定可能。各要素は { url, matchType } のオブジェクト。

  **Items:**

  - **`url` (required)**

    `string` — ドメインまたはURL

  - **`matchType`**

    `string`, possible values: `"url", "forward_url", "domain", "sub_domain"`, default: `"sub_domain"` — マッチタイプ。url: 完全一致URL / forward\_url: 前方一致URL / domain: ドメイン完全一致 / sub\_domain: サブドメイン含むドメイン一致。省略時は sub\_domain。

- **`filter`**

  `object` — 結果のフィルタリング条件。キーワード・SEO難易度・検索順位・月間検索数・CPC・競合性・推定流入数で絞り込む。

  - **`competition`**

    `object` — 広告競合性フィルタ（0〜100の範囲指定）

    - **`max`**

      `integer` — 最大値

    - **`min`**

      `integer` — 最小値

  - **`cpc`**

    `object` — CPC（$）フィルタ（範囲指定）

    - **`max`**

      `number` — 最大CPC

    - **`min`**

      `number` — 最小CPC

  - **`etv`**

    `object` — 推定流入数フィルタ（範囲指定）

    - **`max`**

      `integer` — 最大値

    - **`min`**

      `integer` — 最小値

  - **`keyword`**

    `object` — キーワードフィルタ（含む/含まないキーワード指定）

    - **`includes`**

      `array` — 含む単語のリスト（複数入力時はOR）

      **Items:**

      `string`

    - **`notIncludes`**

      `array` — 含まない単語のリスト（複数入力時はOR）

      **Items:**

      `string`

  - **`rank`**

    `object` — 検索順位フィルタ（1〜100の範囲指定）

    - **`max`**

      `integer` — 最大値

    - **`min`**

      `integer` — 最小値

  - **`searchVolume`**

    `object` — 月間検索数フィルタ（範囲指定）

    - **`max`**

      `integer` — 最大値

    - **`min`**

      `integer` — 最小値

  - **`seoDifficulty`**

    `object` — SEO難易度フィルタ（0〜100の範囲指定）

    - **`max`**

      `integer` — 最大値

    - **`min`**

      `integer` — 最小値

- **`keywordCollapse`**

  `boolean`, default: `false` — キーワード重複除去の有効/無効。true にすると同一キーワードの重複を除去する。省略時は false。

- **`limit`**

  `integer`, default: `100` — 取得件数。1〜10000 の整数を指定する。省略時は 100。

- **`orderBy`**

  `string`, possible values: `"asc", "desc"`, default: `"desc"` — ソート順。asc: 昇順 / desc: 降順。省略時は desc。

- **`sortBy`**

  `string`, possible values: `"keyword", "seoDifficulty", "rank", "searchVolume", "cpc", "competition", "etv"`, default: `"etv"` — ソート項目。keyword / seoDifficulty / rank / searchVolume / cpc / competition / etv。省略時は etv。

**Example:**

```json
{
  "targets": [
    {
      "url": "https://rakkokeyword.com/",
      "matchType": "sub_domain"
    }
  ],
  "keywordCollapse": false,
  "filter": {
    "keyword": {
      "includes": [
        "水族館"
      ],
      "notIncludes": [
        "グッズ"
      ]
    },
    "seoDifficulty": {
      "min": 1,
      "max": 100
    },
    "rank": {
      "min": 1,
      "max": 100
    },
    "searchVolume": {
      "min": 100,
      "max": 10000
    },
    "cpc": {
      "min": 0.5,
      "max": 10
    },
    "competition": {
      "min": 1,
      "max": 100
    },
    "etv": {
      "min": 100,
      "max": 10000
    }
  },
  "sortBy": "etv",
  "orderBy": "desc",
  "limit": 100
}
```

#### Responses

##### Status: 200 検索成功

###### Content-Type: application/json

- **`data` (required)**

  `object` — 獲得キーワード調査結果データ

  - **`items` (required)**

    `array` — 獲得キーワード調査結果のリスト。各アイテムに対象・キーワード・指標・順位情報を含む。

    **Items:**

    - **`keyword` (required)**

      `string` — 対象が獲得しているSEOキーワード

    - **`metrics` (required)**

      `object` — キーワードの各種指標（SEO難易度・月間検索数・CPC・広告競合性）

      - **`competition` (required)**

        `number` — 広告競合性。0〜100 で表し、高いほど競合性が高い（0–33:低 / 34–66:中 / 67–100:高）

      - **`cpc` (required)**

        `number` — 推定クリック単価（USD）

      - **`searchVolume` (required)**

        `number` — 月間検索数（年平均）

      - **`seoDifficulty` (required)**

        `object` — SEO難易度。1–100で表し、高いほど難易度が高い（1–33:低 / 34–66:中 / 67–100:高）。不明な場合は null。

    - **`ranking` (required)**

      `object` — 検索順位情報（順位・推定流入数・ランクインURL）

      - **`estimatedTraffic` (required)**

        `number` — このキーワードからの推定検索流入数（月間）

      - **`position` (required)**

        `number` — 検索順位

      - **`url` (required)**

        `string` — ランクインしているURL

    - **`target` (required)**

      `string` — このキーワードを獲得している対象URLまたはドメイン

  - **`query` (required)**

    `object` — リクエストで指定されたクエリ情報

    - **`targets` (required)**

      `array` — 獲得キーワード調査の対象URLまたはドメイン一覧

      **Items:**

      `string`

  - **`summary` (required)**

    `object` — 集計サマリー（件数・推定流入数・キーワード数）

    - **`estimatedTraffic` (required)**

      `number` — 対象全体の推定検索流入数（月間）

    - **`keywordCount` (required)**

      `number` — ランクインしているキーワード数

    - **`returnedCount` (required)**

      `number` — このレスポンスに含まれている件数

    - **`totalCount` (required)**

      `number` — 取得対象全体の件数

- **`errors` (required)**

  `array` — エラーメッセージの配列。正常時は空配列。

  **Items:**

  `string`

- **`meta` (required)**

  `object` — リクエストに関するメタ情報（課金・消費リソースなど）

  - **`consumedCredit` (required)**

    `number` — このリクエストで消費されたクレジット数。

- **`result` (required)**

  `boolean` — API 呼び出しの成否。正常時は true、エラー時は false。

**Example:**

```json
{
  "result": true,
  "meta": {
    "consumedCredit": 4.5
  },
  "data": {
    "query": {
      "targets": [
        "https://example.com/"
      ]
    },
    "summary": {
      "totalCount": 983,
      "returnedCount": 100,
      "estimatedTraffic": 2824,
      "keywordCount": 983
    },
    "items": [
      {
        "target": "https://example.com/",
        "keyword": "ラッコ",
        "metrics": {
          "seoDifficulty": 30,
          "searchVolume": 10000,
          "cpc": 0,
          "competition": 0
        },
        "ranking": {
          "position": 1,
          "estimatedTraffic": 438,
          "url": "https://example.com/page"
        }
      }
    ]
  },
  "errors": []
}
```

##### Status: 400 バリデーションエラー

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "urls must be a string"
  ]
}
```

##### Status: 402 クレジット不足

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Insufficient credits. Required: 1, Available: 0"
  ]
}
```

##### Status: 403 認証エラー

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Forbidden"
  ]
}
```

##### Status: 429 レート制限超過

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Rate limit exceeded. Please try again later."
  ]
}
```

##### Status: 500 Internal Server Error

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "500 Internal Server Error"
  ]
}
```

##### Status: 503 Service Unavailable - データベース接続エラーなど

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Service Unavailable"
  ]
}
```

### 獲得ページ調査取得

- **Method:** `POST`
- **Path:** `/v1/influx-pages`
- **Tags:** 獲得キーワード調査

獲得ページ調査。

指定ドメイン/URLがGoogleからSEO集客できているページを取得。 競合や自サイトの主力集客ページの把握に有用。

最大10,000件取得。 指定ドメインの各ページの合計推定流入数・合計集客価値(USD)・ランクインキーワード数・最もSEO流入を集めているキーワードを返す。

検索順位やSEO指標は最新でない可能性がある。 この指標を重要視する用途なら、データ取得後にPOST /v1/search-volumeで最新のSEO指標を取得するか、POST /v1/search-rankで最新の検索順位＋SEO指標を取得すること。

競合がすでにSEOで集客できているページは、Google検索のユーザーから需要がある情報が掲載されていることが推定できる。

当機能で発見したページの、2位以降のSEO流入キーワードを確認したい場合はPOST /v1/influx-keywordsをmatchType=urlで使う。

1リクエストあたり4.5クレジットを消費。

#### Request Body

##### Content-Type: application/json

- **`targets` (required)**

  `array` — 獲得キーワード調査（ページ軸）の対象ドメインまたはURLとマッチタイプの配列。最大20件まで指定可能。

  **Items:**

  - **`url` (required)**

    `string` — ドメインまたはURL

  - **`matchType`**

    `string`, possible values: `"url", "forward_url", "domain", "sub_domain"`, default: `"sub_domain"` — マッチタイプ。url: 完全一致URL / forward\_url: 前方一致URL / domain: ドメイン完全一致 / sub\_domain: サブドメイン含むドメイン一致。省略時は sub\_domain。

- **`filter`**

  `object` — 結果のフィルタリング条件。合計推定流入数・キーワード数・合計集客価値・タイトル・URL・トップキーワード・SEO難易度で絞り込む。

  - **`keywordCount`**

    `object` — キーワード数フィルタ（範囲指定）

    - **`max`**

      `integer` — 最大値

    - **`min`**

      `integer` — 最小値

  - **`title`**

    `object` — タイトルフィルタ（含む/含まないキーワード指定）

    - **`includes`**

      `array` — 含む単語のリスト（複数入力時はOR）

      **Items:**

      `string`

    - **`notIncludes`**

      `array` — 含まない単語のリスト（複数入力時はOR）

      **Items:**

      `string`

  - **`topKeyword`**

    `object` — トップキーワードフィルタ（含む/含まないキーワード指定）

    - **`includes`**

      `array` — 含む単語のリスト（複数入力時はOR）

      **Items:**

      `string`

    - **`notIncludes`**

      `array` — 含まない単語のリスト（複数入力時はOR）

      **Items:**

      `string`

  - **`topSeoDifficulty`**

    `object` — SEO難易度フィルタ（0〜100の範囲指定）

    - **`max`**

      `integer` — 最大値

    - **`min`**

      `integer` — 最小値

  - **`totalEtv`**

    `object` — 合計推定流入数フィルタ（範囲指定）

    - **`max`**

      `integer` — 最大値

    - **`min`**

      `integer` — 最小値

  - **`totalTrafficValue`**

    `object` — 合計集客価値（USD）フィルタ（範囲指定）

    - **`max`**

      `integer` — 最大値

    - **`min`**

      `integer` — 最小値

  - **`url`**

    `object` — URLフィルタ（含む/含まないURL指定）

    - **`includes`**

      `array` — 含むURLのリスト（複数入力時はOR）

      **Items:**

      `string`

    - **`notIncludes`**

      `array` — 含まないURLのリスト（複数入力時はOR）

      **Items:**

      `string`

- **`limit`**

  `integer`, default: `100` — 取得件数。1〜10000 の整数を指定する。省略時は 100。

- **`orderBy`**

  `string`, possible values: `"asc", "desc"`, default: `"desc"` — ソート順。asc: 昇順 / desc: 降順。省略時は desc。

- **`sortBy`**

  `string`, possible values: `"totalEtv", "totalTrafficValue", "keywordCount"`, default: `"totalEtv"` — ソート項目。totalEtv / totalTrafficValue / keywordCount。省略時は totalEtv。

- **`topKeywordCollapse`**

  `boolean`, default: `false` — トップキーワード重複除去の有効/無効。true にすると同一トップキーワードの重複を除去する。省略時は false。

**Example:**

```json
{
  "targets": [
    {
      "url": "https://rakkokeyword.com/",
      "matchType": "sub_domain"
    }
  ],
  "topKeywordCollapse": false,
  "filter": {
    "totalEtv": {
      "min": 100,
      "max": 10000
    },
    "keywordCount": {
      "min": 100,
      "max": 10000
    },
    "totalTrafficValue": {
      "min": 100,
      "max": 10000
    },
    "title": {
      "includes": [
        "水族館"
      ],
      "notIncludes": [
        "グッズ"
      ]
    },
    "url": {
      "includes": [
        "https://rakkokeyword.com/"
      ],
      "notIncludes": [
        "https://rakkokeyword.com/result/"
      ]
    },
    "topKeyword": {
      "includes": [
        "水族館"
      ],
      "notIncludes": [
        "グッズ"
      ]
    },
    "topSeoDifficulty": {
      "min": 1,
      "max": 100
    }
  },
  "sortBy": "totalEtv",
  "orderBy": "desc",
  "limit": 100
}
```

#### Responses

##### Status: 200 検索成功

###### Content-Type: application/json

- **`data` (required)**

  `object` — 獲得キーワード調査結果（ページ軸）データ

  - **`items` (required)**

    `array` — 獲得キーワード調査結果（ページ軸）のリスト。各アイテムに対象・ページ情報・パフォーマンス指標・トップキーワードを含む。

    **Items:**

    - **`page` (required)**

      `object` — ページ情報（タイトル・URL）

      - **`title` (required)**

        `string` — ページタイトル

      - **`url` (required)**

        `string` — ページURL

    - **`performance` (required)**

      `object` — パフォーマンス指標（ランクインキーワード数・推定流入数・集客価値）

      - **`estimatedTraffic` (required)**

        `number` — このページの推定検索流入数（月間）

      - **`rankingKeywordCount` (required)**

        `number` — このページでランクインしているキーワード数

      - **`trafficValue` (required)**

        `number` — このページの集客価値（USD）。推定流入数×CPC で算出される広告換算価値。

    - **`target` (required)**

      `string` — このページが属する対象URLまたはドメイン

    - **`topKeyword` (required)**

      `object` — トップキーワード情報（キーワード・順位・指標）

      - **`keyword` (required)**

        `string` — このページで最もSEO流入を獲得しているトップキーワード

      - **`metrics` (required)**

        `object` — トップキーワードの各種指標（SEO難易度・月間検索数）

        - **`searchVolume` (required)**

          `number` — トップキーワードの月間検索数（年平均）

        - **`seoDifficulty` (required)**

          `object` — SEO難易度。1–100で表し、高いほど難易度が高い（1–33:低 / 34–66:中 / 67–100:高）。不明な場合は null。

      - **`position` (required)**

        `number` — トップキーワードでの検索順位

  - **`query` (required)**

    `object` — リクエストで指定されたクエリ情報

    - **`targets` (required)**

      `array` — 獲得キーワード調査の対象URLまたはドメイン一覧

      **Items:**

      `string`

  - **`summary` (required)**

    `object` — 集計サマリー（件数・推定流入数・キーワード数）

    - **`estimatedTraffic` (required)**

      `number` — 対象全体の推定検索流入数（月間）

    - **`keywordCount` (required)**

      `number` — ランクインしているキーワード数

    - **`returnedCount` (required)**

      `number` — このレスポンスに含まれている件数

    - **`totalCount` (required)**

      `number` — 取得対象全体の件数

- **`errors` (required)**

  `array` — エラーメッセージの配列。正常時は空配列。

  **Items:**

  `string`

- **`meta` (required)**

  `object` — リクエストに関するメタ情報（課金・消費リソースなど）

  - **`consumedCredit` (required)**

    `number` — このリクエストで消費されたクレジット数。

- **`result` (required)**

  `boolean` — API 呼び出しの成否。正常時は true、エラー時は false。

**Example:**

```json
{
  "result": true,
  "meta": {
    "consumedCredit": 4.5
  },
  "data": {
    "query": {
      "targets": [
        "https://example.com/"
      ]
    },
    "summary": {
      "totalCount": 319,
      "returnedCount": 100,
      "estimatedTraffic": 2824,
      "keywordCount": 983
    },
    "items": [
      {
        "target": "https://example.com/",
        "page": {
          "title": "ラッコキーワード｜キーワード分析ツール",
          "url": "https://rakkokeyword.com/"
        },
        "performance": {
          "rankingKeywordCount": 2173,
          "estimatedTraffic": 10000,
          "trafficValue": 5000
        },
        "topKeyword": {
          "keyword": "ラッコ",
          "position": 1,
          "metrics": {
            "seoDifficulty": 30,
            "searchVolume": 10000
          }
        }
      }
    ]
  },
  "errors": []
}
```

##### Status: 400 バリデーションエラー

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "urls must be a string"
  ]
}
```

##### Status: 402 クレジット不足

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Insufficient credits. Required: 1, Available: 0"
  ]
}
```

##### Status: 403 認証エラー

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Forbidden"
  ]
}
```

##### Status: 429 レート制限超過

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Rate limit exceeded. Please try again later."
  ]
}
```

##### Status: 500 Internal Server Error

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "500 Internal Server Error"
  ]
}
```

##### Status: 503 Service Unavailable - データベース接続エラーなど

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Service Unavailable"
  ]
}
```

### 競合サイト抽出

- **Method:** `POST`
- **Path:** `/v1/competitive`
- **Tags:** 獲得キーワード調査

競合サイト抽出。

指定ドメインのSEOランクインキーワードが重複しているサイトを最大20件抽出する。 競合サイトを把握するのに有用。

キーワード重複率・推定流入数・集客価値・キーワード数・ページ数などの指標で競合サイトを比較分析できる。

当機能で発見したサイトのSEO流入キーワードを調査したい場合はPOST /v1/influx-keywordsを、 主要なSEO流入を獲得しているページを調査したい場合はPOST /v1/influx-pagesを使う。

1リクエストあたり4.5クレジットを消費。

#### Request Body

##### Content-Type: application/json

- **`url` (required)**

  `string` — 競合分析を行う対象のドメインURL。対象サイトの競合サイトを抽出し、キーワード重複率や流入数などの指標を比較する。

- **`orderBy`**

  `string`, possible values: `"asc", "desc"`, default: `"desc"` — ソート順。asc: 昇順 / desc: 降順。省略時は desc。

- **`sortBy`**

  `string`, possible values: `"duplicate", "duplicateRate", "competitorUnique", "targetUnique", "etv", "keywordCount", "trafficValue", "pageCount"`, default: `"etv"` — ソート項目。duplicate / duplicateRate / competitorUnique / targetUnique / etv / keywordCount / trafficValue / pageCount。省略時は etv。

**Example:**

```json
{
  "url": "https://rakkokeyword.com/",
  "sortBy": "etv",
  "orderBy": "desc"
}
```

#### Responses

##### Status: 200 検索成功

###### Content-Type: application/json

- **`data` (required)**

  `object` — 競合サイト抽出結果データ

  - **`items` (required)**

    `array` — 競合サイト抽出結果のリスト。各アイテムにサイト情報と各種指標を含む。

    **Items:**

    - **`metrics` (required)**

      `object` — 競合サイトの各種指標（流入数・集客価値・キーワード数・重複率など）

      - **`competitorUniqueKeywordCount` (required)**

        `number` — 競合サイトにのみ存在し、入力対象サイトには存在しないキーワード数

      - **`duplicateKeywordCount` (required)**

        `number` — 入力対象サイトと競合サイトで重複しているキーワード数

      - **`duplicateRate` (required)**

        `number` — 重複キーワード率。0〜1 で表し、高いほど入力対象とのキーワード重複率が高い。

      - **`estimatedTraffic` (required)**

        `number` — 競合サイト全体の推定検索流入数（月間）

      - **`keywordCount` (required)**

        `number` — 競合サイトが獲得しているキーワード数

      - **`pageCount` (required)**

        `number` — 競合サイトのインデックスされたページ数

      - **`targetUniqueKeywordCount` (required)**

        `number` — 入力対象サイトにのみ存在し、競合サイトには存在しないキーワード数

      - **`trafficValue` (required)**

        `number` — 競合サイト全体の集客価値（USD）。推定流入数×CPC で算出される広告換算価値。

    - **`site` (required)**

      `object` — 競合サイト情報（ドメイン・タイトル）

      - **`domain` (required)**

        `string` — 競合サイトのドメイン名

      - **`title` (required)**

        `string` — 競合サイトのタイトル。SERP データから取得できない場合は空文字。

  - **`query` (required)**

    `object` — リクエストで指定されたクエリ情報

    - **`targets` (required)**

      `array` — 競合サイト抽出の対象URLまたはドメイン一覧

      **Items:**

      `string`

  - **`summary` (required)**

    `object` — 件数サマリー（全体件数とレスポンスに含まれる件数）

    - **`returnedCount` (required)**

      `number` — このレスポンスに含まれている件数

    - **`totalCount` (required)**

      `number` — 取得対象全体の件数

- **`errors` (required)**

  `array` — エラーメッセージの配列。正常時は空配列。

  **Items:**

  `string`

- **`meta` (required)**

  `object` — リクエストに関するメタ情報（課金・消費リソースなど）

  - **`consumedCredit` (required)**

    `number` — このリクエストで消費されたクレジット数。

- **`result` (required)**

  `boolean` — API 呼び出しの成否。正常時は true、エラー時は false。

**Example:**

```json
{
  "result": true,
  "meta": {
    "consumedCredit": 4.5
  },
  "data": {
    "query": {
      "targets": [
        "https://rakkoma.com/"
      ]
    },
    "summary": {
      "totalCount": 150,
      "returnedCount": 100
    },
    "items": [
      {
        "site": {
          "domain": "rakko.inc",
          "title": "ラッコ株式会社"
        },
        "metrics": {
          "estimatedTraffic": 15803,
          "trafficValue": 51386,
          "keywordCount": 119,
          "pageCount": 51,
          "duplicateKeywordCount": 119,
          "duplicateRate": 1,
          "competitorUniqueKeywordCount": 0,
          "targetUniqueKeywordCount": 596
        }
      }
    ]
  },
  "errors": []
}
```

##### Status: 400 バリデーションエラー

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "urls must be a string"
  ]
}
```

##### Status: 402 クレジット不足

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Insufficient credits. Required: 1, Available: 0"
  ]
}
```

##### Status: 403 認証エラー

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Forbidden"
  ]
}
```

##### Status: 429 レート制限超過

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Rate limit exceeded. Please try again later."
  ]
}
```

##### Status: 500 Internal Server Error

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "500 Internal Server Error"
  ]
}
```

##### Status: 503 Service Unavailable - データベース接続エラーなど

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Service Unavailable"
  ]
}
```

### 一括サイト調査

- **Method:** `POST`
- **Path:** `/v1/bulk-site-research`
- **Tags:** 一括サイト調査

一括サイト調査。

複数URL（最大100件）をまとめて調査し、各サイトの推定流入数・獲得キーワード数・ページ数などの現在値と、その推移（0〜100の指数）を取得する。 サイト群のSEO規模とトレンドを一括で把握するのに有用。

推移データ（histories）は、現在値スカラー（metrics）とは別ソース・別量のため、系列内最大月を100とする指数（0〜100・小数第2位）に正規化して返す。 etv / keywordCount / pageCount の3系列を各々独立に指数化し、キー名は etvIndex / keywordCountIndex / pageCountIndex とする（現在値との誤読防止）。 現在値・分布・変化率は実数のまま返す（指数化しない）。

urlMatchTypeで調査単位を指定する（url: 完全一致 / forward\_url: 前方一致 / domain: ドメイン一致 / sub\_domain: サブドメイン一致）。 itemsは入力urlsと同数・同順で返る。 本機能はSTANDARD以上のプラン限定。対象URLは最大100件。

入力URL1件あたり0.45クレジットを消費（最低4.5クレジット）。（例: 10URL→4.5クレジット、100URL→45クレジット）。

#### Request Body

##### Content-Type: application/json

- **`urls` (required)**

  `array` — 一括サイト調査の対象URL一覧（1〜100件）。各URLの推定流入数・獲得キーワード数・ページ数の現在値と、その推移（0〜100指数）を取得する。

  **Items:**

  `string`

- **`urlMatchType`**

  `string`, possible values: `"url", "forward_url", "domain", "sub_domain"`, default: `"domain"` — URLのマッチタイプ。url: 完全一致 / forward\_url: 前方一致 / domain: ドメイン一致 / sub\_domain: サブドメイン一致。省略時は domain。

**Example:**

```json
{
  "urls": [
    "https://rakkokeyword.com/"
  ],
  "urlMatchType": "domain"
}
```

#### Responses

##### Status: 200 検索成功

###### Content-Type: application/json

- **`data` (required)**

  `object` — 一括サイト調査結果データ

  - **`items` (required)**

    `array` — 一括サイト調査結果のリスト。入力 urls と同数・同順。

    **Items:**

    - **`distributions` (required)**

      `object` — ランク帯・流入数帯の分布

      - **`pageTraffic` (required)**

        `object` — ページの推定流入数分布（整形ラベル別の生の件数）。

        - **`0` (required)**

          `number`

        - **`1-100` (required)**

          `number`

        - **`1+` (required)**

          `number`

        - **`100+` (required)**

          `number`

        - **`1000+` (required)**

          `number`

        - **`10001+` (required)**

          `number`

        - **`1001-10000` (required)**

          `number`

        - **`101-1000` (required)**

          `number`

      - **`rankingPosition` (required)**

        `object` — キーワードの検索順位分布（整形ラベル別の生の件数）。

        - **`1-10` (required)**

          `number`

        - **`1-20` (required)**

          `number`

        - **`1-3` (required)**

          `number`

        - **`1-30` (required)**

          `number`

        - **`11-20` (required)**

          `number`

        - **`21-50` (required)**

          `number`

        - **`4-10` (required)**

          `number`

        - **`51-100` (required)**

          `number`

    - **`histories` (required)**

      `array` — 推移データ（0〜100指数・小数第2位・12点）。常に返却される。

      **Items:**

      - **`date` (required)**

        `string` — 各月末日（YYYY-MM-DD）。取得済み履歴中の最新月末を末尾に11ヶ月前までの12点。

      - **`etvIndex` (required)**

        `number` — 推定流入数の推移指数（0〜100・小数第2位）。系列内最大月を100とする比例スケール。

      - **`keywordCountIndex` (required)**

        `number` — 獲得キーワード数の推移指数（0〜100・小数第2位）。系列内最大月を100とする比例スケール。

      - **`pageCountIndex` (required)**

        `number` — ページ数の推移指数（0〜100・小数第2位）。系列内最大月を100とする比例スケール。

    - **`metrics` (required)**

      `object` — 現在値の各種指標（実数）。推移の指数（histories）とは別量。

      - **`averageEstimatedTrafficPerPage` (required)**

        `number` — 1ページ平均の推定流入数

      - **`averageRankingKeywordCountPerPage` (required)**

        `number` — 1ページ平均のランクインキーワード数

      - **`averageTrafficValuePerPage` (required)**

        `number` — 1ページ平均の集客価値（USD）

      - **`estimatedTraffic` (required)**

        `number` — 推定検索流入数（月間・生値・現在集計）

      - **`estimatedTrafficChangeRate` (required)**

        `object` — 推定流入数の前年同月比（生値ベース）。パーセントではなく比率（0.1 = +10%、1.0 = +100%）。算出不能時は null。

      - **`keywordCount` (required)**

        `number` — 獲得しているキーワード数（生値）

      - **`pageCount` (required)**

        `number` — インデックスされているページ数（生値）

      - **`pagesWithTrafficCount` (required)**

        `number` — 検索流入があるページ数

      - **`pagesWithTrafficRate` (required)**

        `number` — 検索流入があるページの比率。パーセントではなく比率（0.8235 = 82.35%）。

      - **`trafficValue` (required)**

        `number` — 集客価値の合計（USD・生値）。推定流入数×CPC で算出される広告換算価値。

    - **`site` (required)**

      `object` — 調査対象サイト（urlMatchType で整形した検索パターン）

      - **`target` (required)**

        `string` — urlMatchType で整形した検索対象パターン（url: host/path / forward\_url: host/path\* / domain: host/\* / sub\_domain: \*.host/\*）

  - **`query` (required)**

    `object` — リクエストで指定されたクエリ情報

    - **`targets` (required)**

      `array` — urlMatchType で整形した検索対象パターン一覧（items と同数・同順）

      **Items:**

      `string`

    - **`urlMatchType` (required)**

      `string`, possible values: `"url", "forward_url", "domain", "sub_domain"` — リクエストで指定された（または既定の）URLマッチタイプ

  - **`summary` (required)**

    `object` — 件数サマリー（全体件数とレスポンスに含まれる件数。入力URLと1:1）

    - **`returnedCount` (required)**

      `number` — このレスポンスに含まれている件数

    - **`totalCount` (required)**

      `number` — 取得対象全体の件数

- **`errors` (required)**

  `array` — エラーメッセージの配列。正常時は空配列。

  **Items:**

  `string`

- **`meta` (required)**

  `object` — リクエストに関するメタ情報（課金・消費リソースなど）

  - **`consumedCredit` (required)**

    `number` — このリクエストで消費されたクレジット数。

- **`result` (required)**

  `boolean` — API 呼び出しの成否。正常時は true、エラー時は false。

**Example:**

```json
{
  "result": true,
  "meta": {
    "consumedCredit": 4.5
  },
  "data": {
    "query": {
      "targets": [
        "*.rakkokeyword.com/*"
      ],
      "urlMatchType": "domain"
    },
    "summary": {
      "totalCount": 150,
      "returnedCount": 100
    },
    "items": [
      {
        "site": {
          "target": "*.rakkokeyword.com/*"
        },
        "metrics": {
          "estimatedTraffic": 15803,
          "estimatedTrafficChangeRate": 0.125,
          "keywordCount": 119,
          "pageCount": 51,
          "trafficValue": 51386,
          "pagesWithTrafficCount": 42,
          "pagesWithTrafficRate": 0.8235,
          "averageEstimatedTrafficPerPage": 309.86,
          "averageRankingKeywordCountPerPage": 2.33,
          "averageTrafficValuePerPage": 1007.57
        },
        "histories": [
          {
            "date": "2026-06-30",
            "etvIndex": 100,
            "keywordCountIndex": 82.35,
            "pageCountIndex": 90.12
          }
        ],
        "distributions": {
          "rankingPosition": {
            "1-3": 1,
            "4-10": 1,
            "11-20": 1,
            "21-50": 1,
            "51-100": 1,
            "1-10": 1,
            "1-20": 1,
            "1-30": 1
          },
          "pageTraffic": {
            "0": 1,
            "10001+": 1,
            "1001-10000": 1,
            "101-1000": 1,
            "1-100": 1,
            "1000+": 1,
            "100+": 1,
            "1+": 1
          }
        }
      }
    ]
  },
  "errors": []
}
```

##### Status: 400 バリデーションエラー

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "urls must be a string"
  ]
}
```

##### Status: 402 クレジット不足

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Insufficient credits. Required: 1, Available: 0"
  ]
}
```

##### Status: 403 認証エラー

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Forbidden"
  ]
}
```

##### Status: 429 レート制限超過

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Rate limit exceeded. Please try again later."
  ]
}
```

##### Status: 500 Internal Server Error

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "500 Internal Server Error"
  ]
}
```

##### Status: 503 Service Unavailable - データベース接続エラーなど

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Service Unavailable"
  ]
}
```

### 集客コンテンツ検索

- **Method:** `POST`
- **Path:** `/v1/content-search`
- **Tags:** 集客コンテンツ検索

集客コンテンツ検索。

指定キーワードをタイトル/ディスクリプション/主なSEO流入キーワードに含むWEBページを検索する。 寄稿/広告掲載先探しや、参考記事探し・競合コンテンツの把握に使う。

最大5,000件取得。各ページの推定流入数・集客価値・ランクインキーワード数・トップキーワードなどのSEO指標を返す。 SEO指標は最新でない可能性がある。 この指標を重要視する用途なら、データ取得後にPOST /v1/search-volumeで最新のSEO指標を取得すること。

topKeywordCollapseをtrueにした際のトップキーワードには、元キーワードをタイトル/ディスクリプションに含むページが、SEO流入を獲得している主要なキーワードが重複無しで抽出される。 検索意図の近いキーワードを探したり、弱いサイトが意図せずランクインしているニッチキーワードを抽出したりするのに役立つ。

当機能で発見したページの、2位以降のSEO流入キーワードを確認したい場合はPOST /v1/influx-keywordsをmatchType=urlで使う。

1リクエストあたり4.5クレジットを消費。

#### Request Body

##### Content-Type: application/json

- **`keyword` (required)**

  `string` — 集客コンテンツ検索の検索キーワード。指定キーワードに関連する上位表示コンテンツを検索する。1文字以上の文字列を指定する。

- **`filter`**

  `object` — 結果のフィルタリング条件。推定流入数・ランクインキーワード数・集客価値・タイトル・URL・トップキーワード・ディスクリプション・SEO難易度で絞り込む。

  - **`description`**

    `object` — ディスクリプションフィルタ（含む/含まないキーワード指定）

    - **`includes`**

      `array` — 含む単語のリスト（複数入力時はOR）

      **Items:**

      `string`

    - **`notIncludes`**

      `array` — 含まない単語のリスト（複数入力時はOR）

      **Items:**

      `string`

  - **`estimatedTraffic`**

    `object` — 推定流入数フィルタ（範囲指定）

    - **`max`**

      `integer` — 最大値

    - **`min`**

      `integer` — 最小値

  - **`rankingKeywordCount`**

    `object` — ランクインキーワード数フィルタ（0〜の範囲指定）

    - **`max`**

      `integer` — 最大値

    - **`min`**

      `integer` — 最小値

  - **`seoDifficulty`**

    `object` — SEO難易度フィルタ（0〜100の範囲指定）

    - **`max`**

      `integer` — 最大値

    - **`min`**

      `integer` — 最小値

  - **`title`**

    `object` — タイトルフィルタ（含む/含まないキーワード指定）

    - **`includes`**

      `array` — 含む単語のリスト（複数入力時はOR）

      **Items:**

      `string`

    - **`notIncludes`**

      `array` — 含まない単語のリスト（複数入力時はOR）

      **Items:**

      `string`

  - **`topKeyword`**

    `object` — トップキーワードフィルタ（含む/含まないキーワード指定）

    - **`includes`**

      `array` — 含む単語のリスト（複数入力時はOR）

      **Items:**

      `string`

    - **`notIncludes`**

      `array` — 含まない単語のリスト（複数入力時はOR）

      **Items:**

      `string`

  - **`trafficValue`**

    `object` — 集客価値（USD）フィルタ（範囲指定）

    - **`max`**

      `integer` — 最大値

    - **`min`**

      `integer` — 最小値

  - **`url`**

    `object` — URLフィルタ（含む/含まないURL指定）

    - **`includes`**

      `array` — 含むURLのリスト（複数入力時はOR）

      **Items:**

      `string`

    - **`notIncludes`**

      `array` — 含まないURLのリスト（複数入力時はOR）

      **Items:**

      `string`

- **`isAdvancedSearch`**

  `boolean`, default: `true` — 拡張検索の有効/無効。true にするとキーワードを形態素解析して検索精度を高める。省略時は true。

- **`limit`**

  `integer`, default: `100` — 取得件数。1〜5000 の整数を指定する。省略時は 100。

- **`orderBy`**

  `string`, possible values: `"asc", "desc"`, default: `"desc"` — ソート順。asc: 昇順 / desc: 降順。省略時は desc。

- **`searchTarget`**

  `string`, possible values: `"title", "keyword", "description", "titleAndKeyword", "titleAndKeywordAndDescription"`, default: `"titleAndKeywordAndDescription"` — 検索対象。title / keyword / description / titleAndKeyword / titleAndKeywordAndDescription。省略時は titleAndKeywordAndDescription。

- **`sortBy`**

  `string`, possible values: `"estimatedTraffic", "trafficValue", "rankingKeywordCount"`, default: `"trafficValue"` — 結果のソート項目。estimatedTraffic / trafficValue / rankingKeywordCount。省略時は trafficValue。

- **`topKeywordCollapse`**

  `boolean`, default: `false` — トップキーワード除去の有効/無効。true にすると同一トップキーワードの重複を除去する。省略時は false。

**Example:**

```json
{
  "keyword": "ラッコ",
  "searchTarget": "titleAndKeywordAndDescription",
  "isAdvancedSearch": true,
  "topKeywordCollapse": false,
  "filter": {
    "estimatedTraffic": {
      "min": 100,
      "max": 10000
    },
    "rankingKeywordCount": {
      "min": 1,
      "max": 100
    },
    "trafficValue": {
      "min": 100,
      "max": 10000
    },
    "title": {
      "includes": [
        "水族館"
      ],
      "notIncludes": [
        "グッズ"
      ]
    },
    "url": {
      "includes": [
        "https://rakkokeyword.com/"
      ],
      "notIncludes": [
        "https://rakkokeyword.com/result/"
      ]
    },
    "topKeyword": {
      "includes": [
        "水族館"
      ],
      "notIncludes": [
        "グッズ"
      ]
    },
    "description": {
      "includes": [
        "水族館"
      ],
      "notIncludes": [
        "グッズ"
      ]
    },
    "seoDifficulty": {
      "min": 1,
      "max": 100
    }
  },
  "sortBy": "trafficValue",
  "orderBy": "desc",
  "limit": 100
}
```

#### Responses

##### Status: 200 検索成功

###### Content-Type: application/json

- **`data` (required)**

  `object` — 集客コンテンツ検索結果データ

  - **`items` (required)**

    `array` — 集客コンテンツ検索結果のリスト。各アイテムにページ情報・指標・トップキーワードを含む。

    **Items:**

    - **`metrics` (required)**

      `object` — ページの各種指標（推定流入数・集客価値・ランクインキーワード数）

      - **`estimatedTraffic` (required)**

        `number` — このページの推定検索流入数（月間）

      - **`rankingKeywordCount` (required)**

        `number` — このページでランクインしているキーワード数

      - **`trafficValue` (required)**

        `number` — このページの集客価値（USD）。推定流入数×CPC で算出される広告換算価値。

    - **`page` (required)**

      `object` — ページ情報（ドメイン・URL・タイトル・ディスクリプション）

      - **`description` (required)**

        `string` — ページの説明文

      - **`domain` (required)**

        `string` — ページのドメイン名

      - **`title` (required)**

        `string` — ページのタイトル

      - **`url` (required)**

        `string` — ページの完全なURL

    - **`topKeyword` (required)**

      `object` — トップキーワード情報（キーワード・単語数・順位・指標）

      - **`keyword` (required)**

        `string` — このページで最もSEO流入を獲得しているトップキーワード

      - **`metrics` (required)**

        `object` — トップキーワードの各種指標（SEO難易度・月間検索数）

        - **`searchVolume` (required)**

          `number` — トップキーワードの月間検索数（年平均）

        - **`seoDifficulty` (required)**

          `object` — SEO難易度。1–100で表し、高いほど難易度が高い（1–33:低 / 34–66:中 / 67–100:高）。不明な場合は null。

      - **`position` (required)**

        `number` — トップキーワードでの検索順位

      - **`wordCount` (required)**

        `number` — トップキーワードを構成する単語数（スペース区切り）

  - **`query` (required)**

    `object` — リクエストで指定された検索クエリ情報

    - **`keyword` (required)**

      `string` — 集客コンテンツ検索の元になった検索キーワード

  - **`summary` (required)**

    `object` — 件数サマリー（全体件数とレスポンスに含まれる件数）

    - **`returnedCount` (required)**

      `number` — このレスポンスに含まれている件数

    - **`totalCount` (required)**

      `number` — 取得対象全体の件数

- **`errors` (required)**

  `array` — エラーメッセージの配列。正常時は空配列。

  **Items:**

  `string`

- **`meta` (required)**

  `object` — リクエストに関するメタ情報（課金・消費リソースなど）

  - **`consumedCredit` (required)**

    `number` — このリクエストで消費されたクレジット数。

- **`result` (required)**

  `boolean` — API 呼び出しの成否。正常時は true、エラー時は false。

**Example:**

```json
{
  "result": true,
  "meta": {
    "consumedCredit": 4.5
  },
  "data": {
    "query": {
      "keyword": "ラッコ"
    },
    "summary": {
      "totalCount": 150,
      "returnedCount": 100
    },
    "items": [
      {
        "page": {
          "domain": "rakkokeyword.com",
          "url": "https://rakkokeyword.com/result/contentSearch?q=%E3%83%A9%E3%83%83%E3%82%B3",
          "title": "ラッコキーワード",
          "description": "多機能でサクサク使えるキーワードリサーチツール。生成AIによる記事生成機能搭載。SEO/市場ニーズ調査/競合分析/コンテンツ制作/商品開発にお役立ていただけます。無料でも使えます！"
        },
        "metrics": {
          "estimatedTraffic": 14000,
          "trafficValue": 2266,
          "rankingKeywordCount": 18
        },
        "topKeyword": {
          "keyword": "ラッコ",
          "wordCount": 1,
          "position": 2,
          "metrics": {
            "seoDifficulty": 37,
            "searchVolume": 5000
          }
        }
      }
    ]
  },
  "errors": []
}
```

##### Status: 400 バリデーションエラー

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "keyword is a required field"
  ]
}
```

##### Status: 402 クレジット不足

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Insufficient credits. Required: 1, Available: 0"
  ]
}
```

##### Status: 403 認証エラー

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Forbidden"
  ]
}
```

##### Status: 429 レート制限超過

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Rate limit exceeded. Please try again later."
  ]
}
```

##### Status: 500 Internal Server Error

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "500 Internal Server Error"
  ]
}
```

##### Status: 503 Service Unavailable - データベース接続エラーなど

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Service Unavailable"
  ]
}
```

### 見出し抽出取得

- **Method:** `POST`
- **Path:** `/v1/headline`
- **Tags:** 見出し抽出

見出し抽出。指定キーワードのGoogle検索上位ページの見出し（h1〜h6）を抽出する。

そのキーワードでSEO上位表示するために必要な情報を分析したいときや、 競合上位ページがどのようなオリジナルコンテンツを記事に含めているのかを把握するのに役立つ。 SEO記事のタイトル/見出し/本文を作成する前に使うべき機能。

ページごと・上位ページ平均の文字数・見出し数なども返す。

Googleは、そのキーワードで検索するユーザーの悩みを解決できる可能性の高い記事を上位表示する。 このため上位ページ中で共通して出現する見出し/トピックは、そのキーワードを調べるユーザーにとって必要な情報である可能性が高い。

上位ページの頻出単語（共起語）も欲しい場合はPOST /v1/co-occurrenceを併用する。

1リクエストあたり3クレジットを消費。

#### Request Body

##### Content-Type: application/json

- **`keyword` (required)**

  `string` — 見出し抽出を行う検索キーワード。1文字以上の文字列を指定する。

- **`h1`**

  `boolean`, default: `true` — h1タグの見出しを含めるかどうか。省略時は true。

- **`h2`**

  `boolean`, default: `true` — h2タグの見出しを含めるかどうか。省略時は true。

- **`h3`**

  `boolean`, default: `true` — h3タグの見出しを含めるかどうか。省略時は true。

- **`h4`**

  `boolean`, default: `true` — h4タグの見出しを含めるかどうか。省略時は true。

- **`h5`**

  `boolean`, default: `false` — h5タグの見出しを含めるかどうか。省略時は false。

- **`h6`**

  `boolean`, default: `false` — h6タグの見出しを含めるかどうか。省略時は false。

- **`lessCharacters`**

  `boolean`, default: `false` — 文字数1,000未満のページを除外するかどうか。true で除外する。省略時は false。

- **`lessHeadlines`**

  `boolean`, default: `false` — 見出し5件未満のページを除外するかどうか。true で除外する。省略時は false。

- **`limit`**

  `integer`, default: `20` — 取得件数。1〜20 の整数を指定する。省略時は 20。

- **`orderBy`**

  `string`, possible values: `"asc", "desc"`, default: `"asc"` — ソート順。asc: 昇順 / desc: 降順。省略時は asc。

- **`sortBy`**

  `string`, possible values: `"position", "title", "headlineCount", "wordCount"`, default: `"position"` — ソート項目。position / title / headlineCount / wordCount。省略時は position。

**Example:**

```json
{
  "keyword": "ラッコ",
  "lessHeadlines": false,
  "lessCharacters": false,
  "h1": true,
  "h2": true,
  "h3": true,
  "h4": true,
  "h5": false,
  "h6": false,
  "sortBy": "position",
  "orderBy": "asc",
  "limit": 20
}
```

#### Responses

##### Status: 200 検索成功

###### Content-Type: application/json

- **`data` (required)**

  `object` — 見出し抽出の検索結果データ

  - **`items` (required)**

    `array` — 見出し抽出アイテムのリスト。各アイテムにページ情報・指標・見出し一覧を含む。

    **Items:**

    - **`headlines` (required)**

      `array` — ページ内の見出し一覧。指定した見出しレベル（h1–h6）に応じてフィルタされる。

      **Items:**

      - **`level` (required)**

        `string` — 見出しレベル（h1, h2, h3, h4 など）

      - **`text` (required)**

        `string` — 見出しテキスト

    - **`metrics` (required)**

      `object` — ページの各種指標（検索順位・見出し数・文字数）

      - **`headlineCount` (required)**

        `number` — このページに含まれる見出し数

      - **`position` (required)**

        `number` — 検索順位

      - **`wordCount` (required)**

        `number` — このページの文字数

    - **`page` (required)**

      `object` — 検索結果ページの基本情報（URL・タイトル・ディスクリプション）

      - **`description` (required)**

        `string` — 検索結果ページのディスクリプション

      - **`title` (required)**

        `string` — 検索結果ページのタイトル

      - **`url` (required)**

        `string` — 検索結果ページの URL

  - **`query` (required)**

    `object` — リクエストで指定された検索クエリ情報

    - **`keyword` (required)**

      `string` — 見出し抽出の元になった検索キーワード

  - **`summary` (required)**

    `object` — 件数・文字数・見出し数のサマリー情報

    - **`averageHeadlineCount` (required)**

      `number` — 1ページあたりの平均見出し数

    - **`averageWordCount` (required)**

      `number` — 1ページあたりの平均文字数

    - **`maxWordCount` (required)**

      `number` — ページ文字数の最大値

    - **`minWordCount` (required)**

      `number` — ページ文字数の最小値

    - **`returnedCount` (required)**

      `number` — このレスポンスに含まれている件数

    - **`totalCount` (required)**

      `number` — 取得対象全体の件数

- **`errors` (required)**

  `array` — エラーメッセージの配列。正常時は空配列。

  **Items:**

  `string`

- **`meta` (required)**

  `object` — リクエストに関するメタ情報（課金・消費リソースなど）

  - **`consumedCredit` (required)**

    `number` — このリクエストで消費されたクレジット数。

- **`result` (required)**

  `boolean` — API 呼び出しの成否。正常時は true、エラー時は false。

**Example:**

```json
{
  "result": true,
  "meta": {
    "consumedCredit": 3
  },
  "data": {
    "query": {
      "keyword": "ラッコ"
    },
    "summary": {
      "totalCount": 150,
      "returnedCount": 100,
      "averageHeadlineCount": 19.5,
      "averageWordCount": 7782,
      "minWordCount": 2935,
      "maxWordCount": 12629
    },
    "items": [
      {
        "page": {
          "url": "https://ja.wikipedia.org/wiki/%E3%83%A9%E3%83%83%E3%82%B3",
          "title": "ラッコ - Wikipedia",
          "description": "ラッコは、..."
        },
        "metrics": {
          "position": 1,
          "headlineCount": 19,
          "wordCount": 14190
        },
        "headlines": [
          {
            "level": "h1",
            "text": "ラッコ"
          }
        ]
      }
    ]
  },
  "errors": []
}
```

##### Status: 400 バリデーションエラー

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "keyword is a required field"
  ]
}
```

##### Status: 402 クレジット不足

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Insufficient credits. Required: 1, Available: 0"
  ]
}
```

##### Status: 403 認証エラー

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Forbidden"
  ]
}
```

##### Status: 429 レート制限超過

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Rate limit exceeded. Please try again later."
  ]
}
```

##### Status: 500 Internal Server Error

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "500 Internal Server Error"
  ]
}
```

##### Status: 503 Service Unavailable - データベース接続エラーなど

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Service Unavailable"
  ]
}
```

### 共起語取得

- **Method:** `POST`
- **Path:** `/v1/co-occurrence`
- **Tags:** 共起語取得

共起語取得。 指定キーワードのGoogle検索上位ページから共起語（一緒に使われることが多い語）を抽出する。

SEO記事を上位表示させるために記事に含めるべき単語を把握できる。 SEO記事タイトル/見出し/記事本文を作成する前に使う。

検索上位表示できているページを対象に、本文・タイトル・見出しでの出現回数やサイト数などの指標付きで返す。

Googleは、そのキーワードで検索するユーザーの悩みを解決できる可能性の高い記事を上位表示する。 このため検索上位ページ中で共通して出現する単語は、上位表示のために記事に盛り込むべき単語である可能性が高い。

上位ページの見出し情報も欲しい場合はPOST /v1/headlineを併用する。

1リクエストあたり3クレジットを消費。

#### Request Body

##### Content-Type: application/json

- **`keyword` (required)**

  `string` — 共起語取得の元となる検索キーワード。1文字以上の文字列を指定する。

- **`getDetails`**

  `boolean`, default: `true` — URLごとの詳細情報を取得するかどうか。true にすると各共起語について検索上位ページごとの出現情報を返す。省略時は true。

- **`limit`**

  `integer` — 取得件数の上限。正の整数を指定。省略時はすべての結果を返す。

- **`orderBy`**

  `string`, possible values: `"asc", "desc"`, default: `"desc"` — ソート順。asc: 昇順 / desc: 降順。省略時は desc。

- **`sortBy`**

  `string`, possible values: `"word", "occurrencePageCount", "occurrenceTitleCount", "occurrenceHeadingCount", "siteCountTotal", "siteCountHeading"`, default: `"siteCountTotal"` — ソート項目。word / occurrencePageCount / occurrenceTitleCount / occurrenceHeadingCount / siteCountTotal / siteCountHeading。省略時は siteCountTotal。

**Example:**

```json
{
  "keyword": "ラッコ",
  "getDetails": true,
  "sortBy": "siteCountTotal",
  "orderBy": "desc",
  "limit": 10
}
```

#### Responses

##### Status: 200 検索成功

###### Content-Type: application/json

- **`data` (required)**

  `object` — 共起語検索結果データ

  - **`items` (required)**

    `array` — 共起語アイテムのリスト。各アイテムに共起語・指標・詳細情報を含む。

    **Items:**

    - **`metrics` (required)**

      `object` — 共起語の各種指標（本文・タイトル・見出しの出現回数、出現サイト数）

      - **`occurrenceHeadingCount` (required)**

        `number` — 検索上位ページの見出し内でこの共起語が出現した回数

      - **`occurrencePageCount` (required)**

        `number` — 検索上位ページ内でこの共起語が出現した回数

      - **`occurrenceTitleCount` (required)**

        `number` — 検索上位ページのタイトル内でこの共起語が出現した回数

      - **`siteCountHeading` (required)**

        `number` — 検索上位サイトのうち、この共起語が見出し内に出現したサイト数

      - **`siteCountTotal` (required)**

        `number` — 検索上位サイトのうち、この共起語が本文内で出現したサイト数

    - **`word` (required)**

      `string` — 検索上位ページから抽出した共起語

    - **`pageDetails`**

      `array` — URLごとの詳細情報（getDetails=true の場合のみ）

      **Items:**

      - **`count` (required)**

        `number` — 共起語の本文内出現回数

      - **`countInHeadline` (required)**

        `number` — 共起語の見出し内出現回数

      - **`countInTitle` (required)**

        `number` — 共起語のタイトル内出現回数

      - **`pageCount` (required)**

        `number` — 共起語が出現したページ数

      - **`pageCountInHeadline` (required)**

        `number` — 見出しに共起語が出現したページ数

      - **`rank` (required)**

        `number` — 検索結果における順位

      - **`title` (required)**

        `string` — ページタイトル

      - **`url` (required)**

        `string` — ページURL

  - **`query` (required)**

    `object` — リクエストで指定された検索クエリ情報

    - **`keyword` (required)**

      `string` — 共起語取得の元になった検索キーワード

  - **`summary` (required)**

    `object` — 件数サマリー（全体件数とレスポンスに含まれる件数）

    - **`returnedCount` (required)**

      `number` — このレスポンスに含まれている件数

    - **`totalCount` (required)**

      `number` — 取得対象全体の件数

- **`errors` (required)**

  `array` — エラーメッセージの配列。正常時は空配列。

  **Items:**

  `string`

- **`meta` (required)**

  `object` — リクエストに関するメタ情報（課金・消費リソースなど）

  - **`consumedCredit` (required)**

    `number` — このリクエストで消費されたクレジット数。

- **`result` (required)**

  `boolean` — API 呼び出しの成否。正常時は true、エラー時は false。

**Example:**

```json
{
  "result": true,
  "meta": {
    "consumedCredit": 3
  },
  "data": {
    "query": {
      "keyword": "ラッコ"
    },
    "summary": {
      "totalCount": 150,
      "returnedCount": 100
    },
    "items": [
      {
        "word": "水族館",
        "metrics": {
          "occurrencePageCount": 230,
          "occurrenceTitleCount": 8,
          "occurrenceHeadingCount": 21,
          "siteCountTotal": 13,
          "siteCountHeading": 7
        },
        "pageDetails": [
          {
            "rank": 1,
            "title": "ラッコ",
            "url": "https://ja.wikipedia.org/wiki/%E3%83%A9%E3%83%83%E3%82%B3",
            "count": 3,
            "countInHeadline": 0,
            "countInTitle": 0,
            "pageCount": 1,
            "pageCountInHeadline": 0
          }
        ]
      }
    ]
  },
  "errors": []
}
```

##### Status: 400 バリデーションエラー

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "keyword is a required field"
  ]
}
```

##### Status: 402 クレジット不足

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Insufficient credits. Required: 1, Available: 0"
  ]
}
```

##### Status: 403 認証エラー

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Forbidden"
  ]
}
```

##### Status: 429 レート制限超過

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Rate limit exceeded. Please try again later."
  ]
}
```

##### Status: 500 Internal Server Error

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "500 Internal Server Error"
  ]
}
```

##### Status: 503 Service Unavailable - データベース接続エラーなど

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Service Unavailable"
  ]
}
```

### 検索順位チェック登録

- **Method:** `POST`
- **Path:** `/v1/search-rank`
- **Tags:** 検索順位チェック

検索順位チェック登録。 Google検索における検索順位および、KWの月間検索数/SEO難易度を取得できる（isSearchVolumeAndSeoDifficultyEnabledがONの場合）。 キーワードリストとURL/ドメインを渡すと非同期で検索順位を調査開始する。

処理はバックグラウンドで行われるため、以下の手順で結果を取得すること:

1. 戻り値の requestId を控える
2. GET /v1/search-rank/{requestId}/status で完了を待つ（ポーリング推奨: 初回は30秒後、以降30秒間隔）
3. isCompleted=true になったら POST /v1/search-rank/{requestId}/results で結果を取得する

通常は10件以内は数分以内、それ以外は60分以内程度で取得される。 混雑時は数時間以上かかる場合もあるため、数分経過しても処理が完了しない場合は、 一定の時間を置いてから処理ステータスを確認することを推奨する。

1キーワードあたり0.9クレジットを消費（1〜30位取得）。31〜100位まで取得範囲を拡張する場合は、取得範囲を10位追加するごとに、1キーワードあたり0.3クレジットを追加消費する。

#### Request Body

##### Content-Type: application/json

- **`keywords` (required)**

  `array` — 順位チェックするキーワードの配列

  **Items:**

  `string`

- **`urls` (required)**

  `array` — 順位チェックするURL/ドメインの配列。最大50件まで指定可能。

  **Items:**

  `string`

- **`deduplicate`**

  `boolean`, default: `true` — キーワードの重複除去を行うかどうか。省略時は true。

- **`depth`**

  `number`, possible values: `30, 40, 50, 60, 70, 80, 90, 100`, default: `30` — 検索上位何位までデータ取得するかを指定する。30 / 40 / 50 / 60 / 70 / 80 / 90 / 100 のいずれかを指定。省略時は 30。

- **`device`**

  `string`, possible values: `"desktop", "mobile"`, default: `"desktop"` — SERP取得対象のデバイス。desktop / mobile のいずれか。省略時は desktop。

- **`isSearchVolumeAndSeoDifficultyEnabled`**

  `boolean`, default: `false` — 月間検索数/SEO難易度を取得するかどうか。省略時は false。

- **`language`**

  `string`, default: `"Japanese"` — SERP取得対象の言語名。指定可能な言語名は metadata の languages 一覧を参照。省略時は Japanese。

- **`location`**

  `string`, default: `"Japan"` — SERP取得対象の地域名。省略時は Japan。 - 指定可能な地域名は metadata の locations 一覧を参照（一覧は国レベルのみ） - 市区町村レベルの地域も指定可能。「市区町村名,上位地域名,国名」のようにカンマ区切りの正式名で指定する（例: Shibuya,Tokyo,Japan） - 途中の階層のみ（例: 都道府県のみ）の指定は未サポート

- **`matchType`**

  `string`, possible values: `"url", "forward_url", "domain", "sub_domain"`, default: `"sub_domain"` — マッチタイプ。url: 完全一致URL / forward\_url: 前方一致URL / domain: ドメイン完全一致 / sub\_domain: サブドメイン含むドメイン一致。省略時は sub\_domain。

- **`os`**

  `string`, possible values: `"windows", "macos", "android", "ios"` — SERP取得対象のOS。デスクトップは windows / macos、モバイルは android / ios を指定。省略時は desktop→windows / mobile→android。

**Example:**

```json
{
  "keywords": [
    "ラッコ",
    "カワウソ"
  ],
  "urls": [
    "https://rakkokeyword.com",
    "https://rakkokeyword.com/result/contentSearch?q=%E3%83%A9%E3%83%83%E3%82%B3"
  ],
  "matchType": "sub_domain",
  "depth": 30,
  "isSearchVolumeAndSeoDifficultyEnabled": false,
  "deduplicate": true,
  "location": "Japan",
  "language": "Japanese",
  "device": "desktop",
  "os": "windows"
}
```

#### Responses

##### Status: 201 登録成功

###### Content-Type: application/json

- **`data` (required)**

  `object` — 履歴登録結果

  - **`requestId`**

    `string` — リクエストID

- **`errors` (required)**

  `array` — エラーメッセージの配列。正常時は空配列。

  **Items:**

  `string`

- **`meta` (required)**

  `object` — リクエストに関するメタ情報（課金・消費リソースなど）

  - **`consumedCredit` (required)**

    `number` — このリクエストで消費されたクレジット数。

- **`result` (required)**

  `boolean` — API 呼び出しの成否。正常時は true、エラー時は false。

**Example:**

```json
{
  "result": true,
  "meta": {
    "consumedCredit": 1.2
  },
  "data": {
    "requestId": "01HQZX5Y4JMQK8XNQ7WVZXZ5Y4"
  },
  "errors": []
}
```

##### Status: 400 バリデーションエラー

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "keywords is a required field"
  ]
}
```

##### Status: 402 クレジット不足

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Insufficient credits. Required: 1, Available: 0"
  ]
}
```

##### Status: 403 認証エラー

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Forbidden"
  ]
}
```

##### Status: 429 レート制限超過

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Rate limit exceeded. Please try again later."
  ]
}
```

##### Status: 500 Internal Server Error

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "500 Internal Server Error"
  ]
}
```

##### Status: 503 Service Unavailable - データベース接続エラーなど

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Service Unavailable"
  ]
}
```

### 検索順位チェック履歴一覧取得

- **Method:** `GET`
- **Path:** `/v1/search-rank/histories`
- **Tags:** 検索順位チェック

検索順位チェック履歴一覧取得。検索順位チェックの過去リクエスト履歴一覧を取得する。取得結果から requestId を取り出し、GET /v1/search-rank/{requestId}/status で完了確認 / POST /v1/search-rank/{requestId}/results で結果取得が可能。

- ソート順: createdAt 降順固定(新しい順)
- 全体ステータス (status) は serp が processed かつ searchVolumeAndSeoDifficulty が processed（順位のみ取得＝isSearchVolumeAndSeoDifficultyEnabled が false の場合は serp のみ）なら completed、それ以外は processing。

クレジットは消費しない。

#### Parameters

##### `limit`

- **In:** `query`

取得件数。1〜100の整数を指定する。省略時は 100。

`number`, default: `100`

##### `offset`

- **In:** `query`

取得開始位置。0以上の整数を指定する。省略時は 0。offset + limit が 50000 を超える指定は不可。

`number`, default: `0`

##### `status`

- **In:** `query`

ステータスフィルタ。completed: 全処理完了 / processing: 処理中。省略時は全件取得。

`string`, possible values: `"completed", "processing"`

#### Responses

##### Status: 200 取得成功

###### Content-Type: application/json

- **`data` (required)**

  `object` — 検索順位チェック履歴一覧データ

  - **`items` (required)**

    `array` — 検索順位チェック履歴アイテムのリスト

    **Items:**

    - **`completedAt` (required)**

      `object` — 全処理完了日時（ISO 8601、UTC）。未完了時は null。

    - **`createdAt` (required)**

      `string`, format: `date-time` — リクエスト作成日時（ISO 8601、UTC）

    - **`depth` (required)**

      `object` — 検索結果の取得深度。30 / 40 / 50 / 60 / 70 / 80 / 90 / 100 のいずれか。取得深度が記録されていない古い履歴では null を返す。

    - **`isSearchVolumeAndSeoDifficultyEnabled` (required)**

      `boolean` — 月間検索数/SEO難易度の取得が有効かどうか

    - **`keywordCount` (required)**

      `number` — キーワードの件数

    - **`keywordSummary` (required)**

      `string` — キーワードのサマリ（カンマ区切り、先頭20件・255文字以内で切り詰め）

    - **`matchType` (required)**

      `string`, possible values: `"url", "forward_url", "domain", "sub_domain"` — マッチタイプ。url: 完全一致URL / forward\_url: 前方一致URL / domain: ドメイン完全一致 / sub\_domain: サブドメイン含むドメイン一致。

    - **`requestId` (required)**

      `string` — リクエストID

    - **`status` (required)**

      `string`, possible values: `"completed", "processing"` — 全体ステータス。statuses の両方が processed の場合に completed（月間検索数/SEO難易度取得 OFF の場合は serp のみで判定）。

    - **`statuses` (required)**

      `object` — 各処理のステータス情報

      - **`serp` (required)**

        `string`, possible values: `"unprocessed", "processing", "processed"` — SERP取得ステータス。unprocessed: 未処理 / processing: 処理中 / processed: 完了。

      - **`searchVolumeAndSeoDifficulty`**

        `string`, possible values: `"unprocessed", "processing", "processed", "failed", "integration_failed"` — 月間検索数/SEO難易度ステータス。月間検索数/SEO難易度取得 OFF のリクエストでは欠落する。unprocessed: 未処理 / processing: 処理中 / processed: 完了 / failed: 失敗 / integration\_failed: 統合失敗。

    - **`urlCount` (required)**

      `number` — URLの件数

    - **`urlSummary` (required)**

      `string` — URLのサマリ（カンマ区切り、先頭20件・255文字以内で切り詰め）

  - **`query` (required)**

    `object` — リクエストで指定されたクエリパラメータ

    - **`limit` (required)**

      `number` — リクエストで指定された取得件数

    - **`offset` (required)**

      `number` — リクエストで指定された取得開始位置

    - **`status` (required)**

      `object` — リクエストで指定されたステータスフィルタ

  - **`summary` (required)**

    `object` — 件数サマリ

    - **`returnedCount` (required)**

      `number` — このレスポンスに含まれている件数

    - **`totalCount` (required)**

      `number` — 取得対象全体の件数

- **`errors` (required)**

  `array` — エラーメッセージの配列。正常時は空配列。

  **Items:**

  `string`

- **`meta` (required)**

  `object` — リクエストに関するメタ情報（課金・消費リソースなど）

  - **`consumedCredit` (required)**

    `number` — このリクエストで消費されたクレジット数。

- **`result` (required)**

  `boolean` — API 呼び出しの成否。正常時は true、エラー時は false。

**Example:**

```json
{
  "result": true,
  "meta": {
    "consumedCredit": 0
  },
  "data": {
    "query": {
      "limit": 100,
      "offset": 0,
      "status": null
    },
    "summary": {
      "totalCount": 1,
      "returnedCount": 1
    },
    "items": [
      {
        "requestId": "01HQZX5Y4JMQK8XNQ7WVZXZ5Y4",
        "createdAt": "2026-05-31T01:00:00.000Z",
        "completedAt": null,
        "status": "processing",
        "statuses": {
          "serp": "processed",
          "searchVolumeAndSeoDifficulty": "processing"
        },
        "keywordSummary": "ラッコ,カワウソ",
        "urlSummary": "https://rakkokeyword.com,https://rakko.inc",
        "keywordCount": 2,
        "urlCount": 2,
        "matchType": "sub_domain",
        "depth": 30,
        "isSearchVolumeAndSeoDifficultyEnabled": true
      }
    ]
  },
  "errors": []
}
```

##### Status: 400 バリデーションエラー

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Invalid query parameters"
  ]
}
```

##### Status: 403 認証失敗

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Forbidden"
  ]
}
```

##### Status: 429 レート制限超過

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Rate limit exceeded. Please try again later."
  ]
}
```

##### Status: 500 Internal Server Error

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "500 Internal Server Error"
  ]
}
```

### 検索順位チェックステータス取得

- **Method:** `GET`
- **Path:** `/v1/search-rank/{requestId}/status`
- **Tags:** 検索順位チェック

検索順位チェック処理ステータス確認。POST /v1/search-rank で登録した検索順位チェックの処理ステータスを確認する。 isCompleted が true になるまでポーリングすること（推奨間隔: 30秒）。 isCompleted=true になったら POST /v1/search-rank/{requestId}/results で結果を取得できる。

何回かステータスをチェックしてもisCompletedがtrueにならない場合は一定の時間が経ってから再度結果をチェックすることを推奨する。 （利用が混雑している場合は、取得完了まで数時間以上時間がかかるケースがあるため）

クレジットは消費しない。

#### Parameters

##### `requestId` required

- **In:** `path`

POST /v1/search-rank で取得したリクエストID

`string`

#### Responses

##### Status: 200 ステータス取得成功

###### Content-Type: application/json

- **`data` (required)**

  `object` — ステータス情報

  - **`isCompleted`**

    `boolean` — 全処理完了フラグ。statuses.serp が processed かつ statuses.searchVolumeAndSeoDifficulty が processed またはなし の場合に true。failed または integration\_failed の場合は false。

  - **`statuses`**

    `object` — 各処理のステータス情報

    - **`searchVolumeAndSeoDifficulty`**

      `string`, possible values: `"unprocessed", "processing", "processed", "failed", "integration_failed"` — 月間検索数/SEO難易度ステータス。unprocessed: 未処理 / processing: 処理中 / processed: 完了 / failed: 失敗 / integration\_failed: 統合失敗。

    - **`serp`**

      `string`, possible values: `"unprocessed", "processing", "processed"` — SERP取得ステータス。unprocessed: 未処理 / processing: 処理中 / processed: 完了。

- **`errors` (required)**

  `array` — エラーメッセージの配列。正常時は空配列。

  **Items:**

  `string`

- **`meta` (required)**

  `object` — リクエストに関するメタ情報（課金・消費リソースなど）

  - **`consumedCredit` (required)**

    `number` — このリクエストで消費されたクレジット数。

- **`result` (required)**

  `boolean` — API 呼び出しの成否。正常時は true、エラー時は false。

**Example:**

```json
{
  "result": true,
  "meta": {
    "consumedCredit": 0
  },
  "data": {
    "isCompleted": true,
    "statuses": {
      "serp": "processed",
      "searchVolumeAndSeoDifficulty": "processing"
    }
  },
  "errors": []
}
```

##### Status: 400 バリデーションエラー

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Invalid requestId"
  ]
}
```

##### Status: 403 認証失敗

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Forbidden"
  ]
}
```

##### Status: 429 レート制限超過

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Rate limit exceeded. Please try again later."
  ]
}
```

##### Status: 500 Internal Server Error

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "500 Internal Server Error"
  ]
}
```

### 検索順位チェック結果データ取得

- **Method:** `POST`
- **Path:** `/v1/search-rank/{requestId}/results`
- **Tags:** 検索順位チェック

検索順位チェック結果取得。POST /v1/search-rank で登録した検索順位チェックの結果を取得する。事前に GET /v1/search-rank/{requestId}/status で処理完了を確認してから呼び出す。フィルタ・ソート・件数制限が可能。 ※ 非同期API（登録 → ステータス確認 → 結果取得の3ステップ）

クレジットは消費しない。

#### Parameters

##### `requestId` required

- **In:** `path`

POST /v1/search-rank で取得したリクエストID

`string`

#### Request Body

##### Content-Type: application/json

- **`filter`**

  `object` — 結果のフィルタリング条件。キーワード・SEO難易度・月間検索数で絞り込む。

  - **`keyword`**

    `object` — キーワードフィルタ（含む/含まないキーワード指定）

    - **`includes`**

      `array` — 含む単語のリスト（複数入力時はOR）

      **Items:**

      `string`

    - **`notIncludes`**

      `array` — 含まない単語のリスト（複数入力時はOR）

      **Items:**

      `string`

  - **`searchVolume`**

    `object` — 月間検索数フィルタ（範囲指定）

    - **`max`**

      `integer` — 最大値

    - **`min`**

      `integer` — 最小値

  - **`seoDifficulty`**

    `object` — SEO難易度フィルタ（0〜100の範囲指定）

    - **`max`**

      `integer` — 最大値

    - **`min`**

      `integer` — 最小値

- **`limit`**

  `integer`, default: `100` — 取得件数。1以上の整数を指定する。省略時は 100。

- **`orderBy`**

  `string`, possible values: `"asc", "desc"`, default: `"desc"` — ソート順。asc: 昇順 / desc: 降順。省略時は desc。

- **`sortBy`**

  `string`, possible values: `"keyword", "seoDifficulty", "searchVolume"`, default: `"searchVolume"` — ソート項目。keyword / seoDifficulty / searchVolume。省略時は searchVolume。

- **`withAggregation`**

  `boolean`, default: `false` — ターゲットごとの集計情報（推定流入数）を出力するかどうか。省略時は false。

**Example:**

```json
{
  "filter": {
    "keyword": {
      "includes": [
        "水族館"
      ],
      "notIncludes": [
        "グッズ"
      ]
    },
    "seoDifficulty": {
      "min": 1,
      "max": 100
    },
    "searchVolume": {
      "min": 100,
      "max": 10000
    }
  },
  "sortBy": "searchVolume",
  "orderBy": "desc",
  "limit": 100,
  "withAggregation": false
}
```

#### Responses

##### Status: 200 データ取得成功

###### Content-Type: application/json

- **`data` (required)**

  `object` — 検索順位チェック結果データ

  - **`items` (required)**

    `array` — 検索順位チェック結果アイテムのリスト

    **Items:**

    - **`entryNo` (required)**

      `number` — リクエスト内でのキーワードの登録順

    - **`keyword` (required)**

      `string` — 検索順位を確認したキーワード

    - **`metrics` (required)**

      `object` — 各種指標（SEO難易度・月間検索数・CPC・広告競合性）

      - **`competition` (required)**

        `object` — 広告競合性。0–100で表し、高いほど競合性が高い（0–33:低 / 34–66:中 / 67–100:高）。無効な場合は null。

      - **`cpc` (required)**

        `object` — 推定クリック単価（USD）。無効な場合は null。

      - **`searchVolume` (required)**

        `object` — 月間検索数（年平均）。無効な場合は null。

      - **`seoDifficulty` (required)**

        `object` — SEO難易度。1–100で表し、高いほど難易度が高い（1–33:低 / 34–66:中 / 67–100:高）。不明な場合は null。

    - **`rankings` (required)**

      `array` — ターゲットごとの検索順位情報

      **Items:**

      - **`estimatedTraffic` (required)**

        `number` — このキーワードでの推定検索流入数（月間）

      - **`position` (required)**

        `object` — 検索順位。圏外または未検出の場合は null。

      - **`rankedUrl` (required)**

        `object` — 実際にランクインしたURL。未検出の場合は null。

      - **`target` (required)**

        `string` — 順位チェック対象のURLパターンまたはドメイン

  - **`query` (required)**

    `object` — 検索クエリ情報

    - **`limit` (required)**

      `integer` — リクエストで指定された取得件数

    - **`orderBy` (required)**

      `string`, possible values: `"asc", "desc"` — リクエストで指定されたソート順。asc: 昇順 / desc: 降順。

    - **`requestId` (required)**

      `string` — 検索順位チェック結果を識別するリクエストID

    - **`sortBy` (required)**

      `string`, possible values: `"keyword", "seoDifficulty", "searchVolume"` — リクエストで指定されたソート項目。keyword / seoDifficulty / searchVolume。

    - **`withAggregation` (required)**

      `boolean` — ターゲットごとの集計情報（推定流入数）を出力するかどうか

    - **`filter`**

      `object` — リクエストで指定された絞り込み条件（キーワード・SEO難易度・月間検索数）。指定がない場合は省略される。

      - **`keyword`**

        `object` — キーワードフィルタ（含む/含まないキーワード指定）

        - **`includes`**

          `array` — 含む単語のリスト（複数入力時はOR）

          **Items:**

          `string`

        - **`notIncludes`**

          `array` — 含まない単語のリスト（複数入力時はOR）

          **Items:**

          `string`

      - **`searchVolume`**

        `object` — 月間検索数フィルタ（範囲指定）

        - **`max`**

          `integer` — 最大値

        - **`min`**

          `integer` — 最小値

      - **`seoDifficulty`**

        `object` — SEO難易度フィルタ（0〜100の範囲指定）

        - **`max`**

          `integer` — 最大値

        - **`min`**

          `integer` — 最小値

  - **`summary` (required)**

    `object` — 件数サマリー

    - **`returnedCount` (required)**

      `number` — このレスポンスに含まれている件数

    - **`targets` (required)**

      `array` — ターゲットごとの検索順位分布と推定流入数（フィルター条件にマッチした全件の集計）

      **Items:**

      - **`estimatedTraffic` (required)**

        `number` — 推定検索流入数の合計（withAggregation=false の場合は0）

      - **`rankingPositionDistribution` (required)**

        `object` — フィルター条件にマッチした全件の順位分布

        - **`1-3` (required)**

          `number` — 順位1〜3位のキーワード数

        - **`101+` (required)**

          `number` — 順位101位以降のキーワード数

        - **`11-20` (required)**

          `number` — 順位11〜20位のキーワード数

        - **`21-30` (required)**

          `number` — 順位21〜30位のキーワード数

        - **`31-50` (required)**

          `number` — 順位31〜50位のキーワード数

        - **`4-10` (required)**

          `number` — 順位4〜10位のキーワード数

        - **`51-100` (required)**

          `number` — 順位51〜100位のキーワード数

      - **`target` (required)**

        `string` — 順位チェック対象のURLパターンまたはドメイン

    - **`totalCount` (required)**

      `number` — 取得対象全体の件数

- **`errors` (required)**

  `array` — エラーメッセージの配列。正常時は空配列。

  **Items:**

  `string`

- **`meta` (required)**

  `object` — リクエストに関するメタ情報（課金・消費リソースなど）

  - **`consumedCredit` (required)**

    `number` — このリクエストで消費されたクレジット数。

- **`result` (required)**

  `boolean` — API 呼び出しの成否。正常時は true、エラー時は false。

**Example:**

```json
{
  "result": true,
  "meta": {
    "consumedCredit": 0
  },
  "data": {
    "query": {
      "requestId": "sr_20260309_001",
      "filter": {
        "keyword": {
          "includes": [
            "水族館"
          ],
          "notIncludes": [
            "グッズ"
          ]
        },
        "seoDifficulty": {
          "min": 1,
          "max": 100
        },
        "searchVolume": {
          "min": 100,
          "max": 10000
        }
      },
      "sortBy": "searchVolume",
      "orderBy": "desc",
      "limit": 100,
      "withAggregation": false
    },
    "summary": {
      "totalCount": 2,
      "returnedCount": 2,
      "targets": [
        {
          "target": "*.rakkoma.com/*",
          "estimatedTraffic": 7391,
          "rankingPositionDistribution": {
            "1-3": 40,
            "4-10": 15,
            "11-20": 5,
            "21-30": 4,
            "31-50": 5,
            "51-100": 3,
            "101+": 10
          }
        }
      ]
    },
    "items": [
      {
        "entryNo": 3,
        "keyword": "サイト売買 個人",
        "metrics": {
          "seoDifficulty": 23,
          "searchVolume": 70,
          "cpc": 3.47,
          "competition": 41
        },
        "rankings": [
          {
            "target": "*.rakkoma.com/*",
            "position": 3,
            "rankedUrl": "https://rakkoma.com/",
            "estimatedTraffic": 9
          }
        ]
      }
    ]
  },
  "errors": []
}
```

##### Status: 400 バリデーションエラー

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Invalid request parameters"
  ]
}
```

##### Status: 403 認証失敗

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Forbidden"
  ]
}
```

##### Status: 429 レート制限超過

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Rate limit exceeded. Please try again later."
  ]
}
```

##### Status: 500 Internal Server Error

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "500 Internal Server Error"
  ]
}
```

### 検索順位チェック結果詳細データ取得

- **Method:** `GET`
- **Path:** `/v1/search-rank/{requestId}/results/{entryNo}/serp`
- **Tags:** 検索順位チェック

検索順位チェックSERP取得。POST /v1/search-rank/{requestId}/results で取得した entryNo を指定して、その検索順位チェック結果1件分の検索結果一覧（順位チェック実行時に取得したSERPデータ）を取得する。

SERP上のposition、page.title/url/descriptionは、POST /v1/search-rank で順位チェックが完了した時点のデータを返す。 これ以外のSEO指標（topKeyword.position含む）は、本機能でデータ取得した時点でラッコキーワードデータベースが保持している値を返す。 ラッコキーワードデータベースの情報は最新ではない・データがない可能性がある。 SEO指標を重要視する用途なら、データ取得後にPOST /v1/search-volumeで最新のSEO指標を取得するか、POST /v1/search-rankで最新の検索順位を取得すること。

クレジットは消費しない。

#### Parameters

##### `requestId` required

- **In:** `path`

POST /v1/search-rank で取得したリクエストID

`string`

##### `entryNo` required

- **In:** `path`

POST /v1/search-rank/{requestId}/results で取得したentryNo

`integer`

#### Responses

##### Status: 200 データ取得成功

###### Content-Type: application/json

- **`data` (required)**

  `object` — 検索結果データ

  - **`items` (required)**

    `array` — 検索結果アイテムのリスト

    **Items:**

    - **`metrics` (required)**

      `object` — 付加情報の指標

      - **`estimatedTraffic` (required)**

        `object` — このページの推定検索流入数。不明な場合は null。

      - **`rankingKeywordCount` (required)**

        `object` — このページでランクインしているキーワード数。不明な場合は null。

      - **`trafficValue` (required)**

        `object` — このページの集客価値（USD）。不明な場合は null。

    - **`page` (required)**

      `object` — ページ情報

      - **`description` (required)**

        `string` — ページの説明文

      - **`title` (required)**

        `string` — ページタイトル

      - **`url` (required)**

        `string`, format: `uri` — ページURL

    - **`position` (required)**

      `number` — 検索結果の表示順位

    - **`topKeyword` (required)**

      `object` — トップキーワード情報

      - **`keyword` (required)**

        `object` — このページで最もSEO流入を獲得しているトップキーワード。不明な場合は null。

      - **`metrics` (required)**

        `object` — トップキーワードの指標

        - **`searchVolume` (required)**

          `object` — トップキーワードの月間検索数（年平均）。不明な場合は null。

        - **`seoDifficulty` (required)**

          `object` — SEO難易度。1–100で表し、高いほど難易度が高い（1–33:低 / 34–66:中 / 67–100:高）。不明な場合は null。

      - **`position` (required)**

        `object` — トップキーワードでの検索順位。不明な場合は null。

  - **`query` (required)**

    `object` — 検索クエリ情報

    - **`entryNo` (required)**

      `number` — リクエスト内でのキーワードの登録順

    - **`requestId` (required)**

      `string` — 検索順位チェック履歴を識別するID

  - **`summary` (required)**

    `object` — 件数サマリー

    - **`fetchedDate` (required)**

      `string`, format: `date` — 検索結果の取得日（YYYY-MM-DD）

    - **`keyword` (required)**

      `string` — 検索順位を確認したキーワード

    - **`returnedCount` (required)**

      `number` — このレスポンスに含まれている件数

- **`errors` (required)**

  `array` — エラーメッセージの配列。正常時は空配列。

  **Items:**

  `string`

- **`meta` (required)**

  `object` — リクエストに関するメタ情報（課金・消費リソースなど）

  - **`consumedCredit` (required)**

    `number` — このリクエストで消費されたクレジット数。

- **`result` (required)**

  `boolean` — API 呼び出しの成否。正常時は true、エラー時は false。

**Example:**

```json
{
  "result": true,
  "meta": {
    "consumedCredit": 0
  },
  "data": {
    "query": {
      "requestId": "01HQZX5Y4JMQK8XNQ7WVZXZ5Y4",
      "entryNo": 3
    },
    "summary": {
      "keyword": "サイト売買 個人",
      "returnedCount": 42,
      "fetchedDate": "2026-06-30"
    },
    "items": [
      {
        "position": 1,
        "page": {
          "url": "https://example.com/",
          "title": "サイト売買の個人間取引ガイド",
          "description": "個人でサイト売買を行う際の注意点..."
        },
        "metrics": {
          "estimatedTraffic": 120,
          "trafficValue": 45,
          "rankingKeywordCount": 8
        },
        "topKeyword": {
          "keyword": "サイト 売却 方法",
          "position": 3,
          "metrics": {
            "seoDifficulty": 42,
            "searchVolume": 500
          }
        }
      }
    ]
  },
  "errors": []
}
```

##### Status: 400 バリデーションエラー

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Invalid request parameters"
  ]
}
```

##### Status: 403 認証失敗

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Forbidden"
  ]
}
```

##### Status: 429 レート制限超過

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Rate limit exceeded. Please try again later."
  ]
}
```

##### Status: 500 Internal Server Error

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "500 Internal Server Error"
  ]
}
```

### サイト検索

- **Method:** `POST`
- **Path:** `/v1/site-search`
- **Tags:** サイト検索

サイト検索。 コンテンツ・ドメイン・各種指標で関連サイトを検索し、推定流入数が多い順に取得する。

最大100件取得。各サイトの推定検索流入数・集客価値(USD)・ランクインキーワード数・ページ数を返す。

コンテンツフィルタ（filter.keyword）を使った場合、まず関連サイトを検索流入が多い順に最大100件抽出した後に他のフィルタが適用される。 そのため、フィルタを掛け合わせて101〜200件目を取得することはできない（フィルタは抽出済み100件に対して掛かるため）。 コンテンツフィルタ指定時は、関連コンテンツの推定流入数（relatedContent.estimatedTraffic）とコンテンツ関連性スコア（relatedContent.relevanceScore）も返す。

特定サイトがSEO流入を獲得しているキーワードを調べたい場合は POST /v1/influx-keywords を、 指定ドメインの競合サイトを抽出したい場合は POST /v1/competitive を使う。

1リクエストあたり1.5クレジットを消費。

#### Request Body

##### Content-Type: application/json

- **`filter`**

  `object`, default: `{}` — 絞り込み条件。コンテンツ・ドメイン・推定流入数・キーワード数・ページ数・価値・関連コンテンツ推定流入数・コンテンツ関連性で絞り込む。省略時は全サイトを流入が多い順に取得する。

  - **`contentRelevance`**

    `object` — コンテンツ関連性フィルタ（0〜100の範囲指定）。コンテンツフィルタ指定時のみ有効。

    - **`max`**

      `integer` — 最大値

    - **`min`**

      `integer` — 最小値

  - **`domain`**

    `object` — ドメインフィルタ（含む/含まないドメインとマッチタイプ）

    - **`includes`**

      `array` — 含むドメインのリスト

      **Items:**

      `string`

    - **`matchType`**

      `string`, possible values: `"partialMatch", "prefixMatch", "suffixMatch"`, default: `"partialMatch"` — ドメインのマッチタイプ。partialMatch: 部分一致 / prefixMatch: 前方一致 / suffixMatch: 後方一致。省略時は partialMatch。

    - **`notIncludes`**

      `array` — 含まないドメインのリスト

      **Items:**

      `string`

  - **`keyword`**

    `object` — コンテンツフィルタ（含む/含まないキーワード）。指定すると、まず関連サイトを流入が多い順に最大100件抽出した後に他フィルタが適用される。

    - **`includes` (required)**

      `array` — 含む単語のリスト（1件以上必須）

      **Items:**

      `string`

    - **`notIncludes`**

      `array` — 含まない単語のリスト

      **Items:**

      `string`

  - **`keywordCount`**

    `object` — キーワード数フィルタ（範囲指定）

    - **`max`**

      `integer` — 最大値

    - **`min`**

      `integer` — 最小値

  - **`pageCount`**

    `object` — ページ数フィルタ（範囲指定）

    - **`max`**

      `integer` — 最大値

    - **`min`**

      `integer` — 最小値

  - **`relatedContentEtv`**

    `object` — 関連コンテンツ推定流入数フィルタ（範囲指定）。コンテンツフィルタ指定時のみ有効。

    - **`max`**

      `integer` — 最大値

    - **`min`**

      `integer` — 最小値

  - **`totalEtv`**

    `object` — 推定流入数フィルタ（範囲指定）

    - **`max`**

      `integer` — 最大値

    - **`min`**

      `integer` — 最小値

  - **`totalTrafficValue`**

    `object` — 価値（USD）フィルタ（範囲指定）

    - **`max`**

      `integer` — 最大値

    - **`min`**

      `integer` — 最小値

- **`limit`**

  `integer`, default: `100` — 取得件数。1〜100 の整数を指定する。省略時は 100。

**Example:**

```json
{
  "filter": {},
  "limit": 100
}
```

#### Responses

##### Status: 200 検索成功

###### Content-Type: application/json

- **`data` (required)**

  `object` — サイト検索結果データ

  - **`items` (required)**

    `array` — サイト検索結果のリスト。流入が多い順（コンテンツフィルタ指定時は関連コンテンツ流入が多い順）。

    **Items:**

    - **`metrics` (required)**

      `object` — サイトの各種指標（推定流入数・価値・キーワード数・ページ数）

      - **`estimatedTraffic` (required)**

        `number` — サイト全体の推定検索流入数（月間）

      - **`pageCount` (required)**

        `number` — ランクインしているページ数

      - **`rankingKeywordCount` (required)**

        `number` — サイト全体でランクインしているキーワード数

      - **`trafficValue` (required)**

        `number` — サイト全体の集客価値（USD）

    - **`no` (required)**

      `number` — 結果内の連番（1始まり）

    - **`relatedContent` (required)**

      `object` — コンテンツフィルタ関連の指標。コンテンツフィルタ未指定時は null。

    - **`site` (required)**

      `object` — サイト情報（ドメイン・URL・タイトル・説明文）

      - **`description` (required)**

        `string` — トップページの説明文

      - **`domain` (required)**

        `string` — サイトのドメイン名

      - **`title` (required)**

        `string` — トップページのタイトル

      - **`url` (required)**

        `string` — サイトのトップページURL

  - **`query` (required)**

    `object` — リクエストで指定された検索条件

    - **`filter` (required)**

      `object` — リクエストで適用された絞り込み条件

      - **`contentRelevance`**

        `object` — コンテンツ関連性フィルタ（0〜100の範囲指定）。コンテンツフィルタ指定時のみ有効。

        - **`max`**

          `integer` — 最大値

        - **`min`**

          `integer` — 最小値

      - **`domain`**

        `object` — ドメインフィルタ（含む/含まないドメインとマッチタイプ）

        - **`includes`**

          `array` — 含むドメインのリスト

          **Items:**

          `string`

        - **`matchType`**

          `string`, possible values: `"partialMatch", "prefixMatch", "suffixMatch"`, default: `"partialMatch"` — ドメインのマッチタイプ。partialMatch: 部分一致 / prefixMatch: 前方一致 / suffixMatch: 後方一致。省略時は partialMatch。

        - **`notIncludes`**

          `array` — 含まないドメインのリスト

          **Items:**

          `string`

      - **`keyword`**

        `object` — コンテンツフィルタ（含む/含まないキーワード）。指定すると、まず関連サイトを流入が多い順に最大100件抽出した後に他フィルタが適用される。

        - **`includes` (required)**

          `array` — 含む単語のリスト（1件以上必須）

          **Items:**

          `string`

        - **`notIncludes`**

          `array` — 含まない単語のリスト

          **Items:**

          `string`

      - **`keywordCount`**

        `object` — キーワード数フィルタ（範囲指定）

        - **`max`**

          `integer` — 最大値

        - **`min`**

          `integer` — 最小値

      - **`pageCount`**

        `object` — ページ数フィルタ（範囲指定）

        - **`max`**

          `integer` — 最大値

        - **`min`**

          `integer` — 最小値

      - **`relatedContentEtv`**

        `object` — 関連コンテンツ推定流入数フィルタ（範囲指定）。コンテンツフィルタ指定時のみ有効。

        - **`max`**

          `integer` — 最大値

        - **`min`**

          `integer` — 最小値

      - **`totalEtv`**

        `object` — 推定流入数フィルタ（範囲指定）

        - **`max`**

          `integer` — 最大値

        - **`min`**

          `integer` — 最小値

      - **`totalTrafficValue`**

        `object` — 価値（USD）フィルタ（範囲指定）

        - **`max`**

          `integer` — 最大値

        - **`min`**

          `integer` — 最小値

  - **`summary` (required)**

    `object` — 件数サマリー（全体件数とレスポンスに含まれる件数）

    - **`returnedCount` (required)**

      `number` — このレスポンスに含まれている件数

    - **`totalCount` (required)**

      `number` — 取得対象全体の件数

- **`errors` (required)**

  `array` — エラーメッセージの配列。正常時は空配列。

  **Items:**

  `string`

- **`meta` (required)**

  `object` — リクエストに関するメタ情報（課金・消費リソースなど）

  - **`consumedCredit` (required)**

    `number` — このリクエストで消費されたクレジット数。

- **`result` (required)**

  `boolean` — API 呼び出しの成否。正常時は true、エラー時は false。

**Example:**

```json
{
  "result": true,
  "meta": {
    "consumedCredit": 1.5
  },
  "data": {
    "query": {
      "filter": {
        "keyword": {
          "includes": [
            "水族館"
          ],
          "notIncludes": [
            "グッズ"
          ]
        },
        "domain": {
          "includes": [
            "example.com"
          ],
          "notIncludes": [
            "example.net"
          ],
          "matchType": "partialMatch"
        },
        "totalEtv": {
          "min": 100,
          "max": 10000
        },
        "keywordCount": {
          "min": 100,
          "max": 10000
        },
        "pageCount": {
          "min": 100,
          "max": 10000
        },
        "totalTrafficValue": {
          "min": 100,
          "max": 10000
        },
        "relatedContentEtv": {
          "min": 100,
          "max": 10000
        },
        "contentRelevance": {
          "min": 1,
          "max": 100
        }
      }
    },
    "summary": {
      "totalCount": 150,
      "returnedCount": 100
    },
    "items": [
      {
        "no": 1,
        "site": {
          "domain": "rakkokeyword.com",
          "url": "https://rakkokeyword.com/",
          "title": "ラッコキーワード",
          "description": "多機能でサクサク使えるキーワードリサーチツール。"
        },
        "metrics": {
          "estimatedTraffic": 140000,
          "trafficValue": 22660,
          "rankingKeywordCount": 1800,
          "pageCount": 320
        },
        "relatedContent": {
          "estimatedTraffic": 12000,
          "relevanceScore": 42
        }
      }
    ]
  },
  "errors": []
}
```

##### Status: 400 バリデーションエラー

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "filter must be an object"
  ]
}
```

##### Status: 402 クレジット不足

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Insufficient credits. Required: 1, Available: 0"
  ]
}
```

##### Status: 403 認証エラー

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Forbidden"
  ]
}
```

##### Status: 429 レート制限超過

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Rate limit exceeded. Please try again later."
  ]
}
```

##### Status: 500 Internal Server Error

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "500 Internal Server Error"
  ]
}
```

##### Status: 503 Service Unavailable - データベース接続エラーなど

###### Content-Type: application/json

- **`data`**

  `object`

- **`errors`**

  `array`

  **Items:**

  `string`

- **`result`**

  `boolean`

**Example:**

```json
{
  "result": false,
  "data": {},
  "errors": [
    "Service Unavailable"
  ]
}
```

## Schemas

### SuggestKeywordsDto

- **Type:**`object`

* **`keyword` (required)**

  `string` — サジェスト取得の元となる検索キーワード。1文字以上の文字列を指定する。

* **`filter`**

  `object` — 結果のフィルタリング条件。月間検索数・SEO難易度・CPC・競合性・出現時期・サジェストクラスなどで絞り込む。

  - **`competition`**

    `object` — 競合性フィルタ（0〜100の範囲指定）

    - **`max`**

      `integer` — 最大値

    - **`min`**

      `integer` — 最小値

  - **`cpc`**

    `object` — クリック単価（CPC）フィルタ（USD、範囲指定）

    - **`max`**

      `number` — 最大CPC

    - **`min`**

      `number` — 最小CPC

  - **`firstSeenRange`**

    `object` — 出現時期フィルタ

    - **`include`**

      `string`, possible values: `"last_7_days", "last_30_days", "last_90_days", "within_6_months", "within_1_year", "over_1_year"` — 出現時期の選択肢

  - **`keyword`**

    `object` — キーワードフィルタ

    - **`includes`**

      `array` — 含む単語のリスト（複数入力時はOR）

      **Items:**

      `string`

    - **`notIncludes`**

      `array` — 含まない単語のリスト（複数入力時はOR）

      **Items:**

      `string`

  - **`searchVolume`**

    `object` — 月間検索数フィルタ（範囲指定）

    - **`max`**

      `integer` — 最大値

    - **`min`**

      `integer` — 最小値

  - **`seoDifficulty`**

    `object` — SEO難易度フィルタ（0〜100の範囲指定）

    - **`max`**

      `integer` — 最大値

    - **`min`**

      `integer` — 最小値

  - **`suggestClass`**

    `array` — サジェストクラスフィルタ（0-3の配列）。0: ＋（サジェスト）, 1: ＋＋（サジェストのサジェスト）, 2: ＋α（元キーワードにあいうえお...・abcde...・12345...を付与した際に表示されるサジェスト）, 3: ＋＋＋（「＋＋」または「＋α」からさらに展開されたサジェスト）

    **Items:**

    `integer`

* **`increaseKeyword`**

  `boolean`, default: `false` — キーワード増量オプション。true にすると、より多くのサジェストキーワードを取得する。SEOキーワードを網羅的に取得したい場合は、trueにすること。省略時は false。

* **`limit`**

  `integer` — 取得件数の上限。正の整数を指定。省略時はすべての結果を返す。

* **`modes`**

  `array`, default: `["google"]` — サジェストキーワードを取得する検索エンジン（複数選択可）。google / bing / youtube / googleVideo / amazon / rakuten / googleShopping / googleImage から選択。省略時は google のみ。

  **Items:**

  `string`, possible values: `"google", "bing", "youtube", "googleVideo", "amazon", "rakuten", "googleShopping", "googleImage"`

* **`orderBy`**

  `string`, possible values: `"asc", "desc"`, default: `"desc"` — ソート順。asc: 昇順 / desc: 降順。省略時は desc。

* **`sortBy`**

  `string`, possible values: `"keyword", "suggestClass", "seoDifficulty", "searchVolume", "cpc", "competition", "firstSeenRange"`, default: `"searchVolume"` — 結果のソート項目。keyword / suggestClass / seoDifficulty / searchVolume / cpc / competition / firstSeenRange。省略時は searchVolume。

**Example:**

```json
{
  "keyword": "ラッコ",
  "modes": [
    "google",
    "bing"
  ],
  "increaseKeyword": false,
  "filter": {
    "suggestClass": [
      0,
      1
    ],
    "keyword": {
      "includes": [
        "水族館"
      ],
      "notIncludes": [
        "グッズ"
      ]
    },
    "seoDifficulty": {
      "min": 1,
      "max": 100
    },
    "searchVolume": {
      "min": 100,
      "max": 10000
    },
    "cpc": {
      "min": 0.5,
      "max": 10
    },
    "competition": {
      "min": 1,
      "max": 100
    },
    "firstSeenRange": {
      "include": "last_30_days"
    }
  },
  "sortBy": "searchVolume",
  "orderBy": "desc",
  "limit": 10
}
```

### SuggestKeywordsResponseDto

- **Type:**`object`

* **`data` (required)**

  `object` — サジェストキーワード検索結果データ

  - **`items` (required)**

    `array` — サジェストキーワードのリスト。各アイテムにキーワード・サジェスト分類・SEO指標・取得エンジン情報を含む。

    **Items:**

    - **`keyword` (required)**

      `string` — サジェストキーワード文字列

    - **`metrics` (required)**

      `object` — SEO関連の各種指標（検索ボリューム・SEO難易度・CPC・競合性・出現時期）

      - **`competition` (required)**

        `object` — 広告競合性。0–100で表し、高いほど競合性が高い（0–33:低 / 34–66:中 / 67–100:高）。

      - **`cpc` (required)**

        `object` — 推定クリック単価（USD）

      - **`firstSeenRange` (required)**

        `object` — 出現時期。キーワードが最初にラッコキーワードデータベースで検出された時期を日付範囲ラベルで表す。不明な場合は null。

      - **`searchVolume` (required)**

        `object` — 月間検索数（年平均）

      - **`seoDifficulty` (required)**

        `object` — SEO難易度。1–100で表し、高いほど難易度が高い（1–33:低 / 34–66:中 / 67–100:高）。不明な場合は null。

    - **`suggestClass` (required)**

      `string` — サジェストキーワードの区分ラベル。＋（0: サジェスト）, ＋＋（1: サジェストのサジェスト）, ＋α（2: 元キーワードにあいうえお...・abcde...・12345...を付与した際に表示されるサジェスト）, ＋＋＋（3: 「＋＋」または「＋α」からさらに展開されたサジェスト）

    - **`suggestEngines` (required)**

      `object` — このサジェストキーワードを返した検索エンジンの情報（エンジン数と一覧）

      - **`active` (required)**

        `array` — このキーワードが取得できたサーチエンジン一覧

        **Items:**

        `string`, possible values: `"google", "bing", "youtube", "googleVideo", "amazon", "rakuten", "googleShopping", "googleImage"`

      - **`count` (required)**

        `number` — このキーワードが取得できたサーチエンジン数

  - **`query` (required)**

    `object` — リクエストで指定された検索クエリ情報（キーワードと対象エンジン）

    - **`keyword` (required)**

      `string` — サジェスト取得の元になった検索キーワード

    - **`suggestEngines` (required)**

      `array` — サジェストキーワードの取得対象としたサーチエンジン一覧。単一取得の場合も配列で出力されます。

      **Items:**

      `string`, possible values: `"google", "bing", "youtube", "googleVideo", "amazon", "rakuten", "googleShopping", "googleImage"`

  - **`summary` (required)**

    `object` — 件数サマリー（全体件数とレスポンスに含まれる件数）

    - **`returnedCount` (required)**

      `number` — このレスポンスに含まれている件数

    - **`totalCount` (required)**

      `number` — 取得対象全体の件数

* **`errors` (required)**

  `array` — エラーメッセージの配列。正常時は空配列。

  **Items:**

  `string`

* **`meta` (required)**

  `object` — リクエストに関するメタ情報（課金・消費リソースなど）

  - **`consumedCredit` (required)**

    `number` — このリクエストで消費されたクレジット数。

* **`result` (required)**

  `boolean` — API 呼び出しの成否。正常時は true、エラー時は false。

**Example:**

```json
{
  "result": true,
  "meta": {
    "consumedCredit": 1.5
  },
  "data": {
    "query": {
      "keyword": "ラッコ",
      "suggestEngines": [
        "google"
      ]
    },
    "summary": {
      "totalCount": 150,
      "returnedCount": 100
    },
    "items": [
      {
        "keyword": "ラッコ 水族館",
        "suggestClass": "＋",
        "metrics": {
          "seoDifficulty": 45,
          "searchVolume": 12000,
          "cpc": 1.5,
          "competition": 2,
          "firstSeenRange": "last_30_days"
        },
        "suggestEngines": {
          "count": 2,
          "active": [
            "google",
            "youtube"
          ]
        }
      }
    ]
  },
  "errors": []
}
```

### RelatedKeywordsDto

- **Type:**`object`

* **`keyword` (required)**

  `string` — 関連キーワード取得の元となる検索キーワード。1文字以上の文字列を指定する。

* **`filter`**

  `object` — 結果のフィルタリング条件。月間検索数・SEO難易度・CPC・競合性・出現時期などで絞り込む。

  - **`competition`**

    `object` — 競合性フィルタ（0〜100の範囲指定）

    - **`max`**

      `integer` — 最大値

    - **`min`**

      `integer` — 最小値

  - **`cpc`**

    `object` — クリック単価（CPC）フィルタ（USD、範囲指定）

    - **`max`**

      `number` — 最大CPC

    - **`min`**

      `number` — 最小CPC

  - **`firstSeenRange`**

    `object` — 出現時期フィルタ

    - **`include`**

      `string`, possible values: `"last_7_days", "last_30_days", "last_90_days", "within_6_months", "within_1_year", "over_1_year"` — 出現時期の選択肢

  - **`keyword`**

    `object` — キーワードフィルタ（含む/含まないキーワード指定）

    - **`includes`**

      `array` — 含む単語のリスト（複数入力時はOR）

      **Items:**

      `string`

    - **`notIncludes`**

      `array` — 含まない単語のリスト（複数入力時はOR）

      **Items:**

      `string`

  - **`searchVolume`**

    `object` — 月間検索数フィルタ（範囲指定）

    - **`max`**

      `integer` — 最大値

    - **`min`**

      `integer` — 最小値

  - **`seoDifficulty`**

    `object` — SEO難易度フィルタ（0〜100の範囲指定）

    - **`max`**

      `integer` — 最大値

    - **`min`**

      `integer` — 最小値

* **`limit`**

  `integer`, default: `1000` — 取得件数の上限。1〜25000 の整数を指定。省略時は 1000 件。

* **`matchType`**

  `string`, possible values: `"partialMatch", "phraseMatch", "prefixMatch", "suffixMatch", "wordMatch"`, default: `"partialMatch"` — キーワードのマッチタイプ。partialMatch: 部分一致 / phraseMatch: フレーズ一致 / prefixMatch: 前方一致 / suffixMatch: 後方一致 / wordMatch: 単語一致。省略時は partialMatch。

* **`orderBy`**

  `string`, possible values: `"asc", "desc"`, default: `"desc"` — ソート順。asc: 昇順 / desc: 降順。省略時は desc。

* **`sortBy`**

  `string`, possible values: `"seoDifficulty", "searchVolume", "cpc", "competition", "firstSeenRange"`, default: `"searchVolume"` — 結果のソート項目。seoDifficulty / searchVolume / cpc / competition / firstSeenRange。省略時は searchVolume。

**Example:**

```json
{
  "keyword": "ラッコ",
  "matchType": "partialMatch",
  "filter": {
    "keyword": {
      "includes": [
        "水族館"
      ],
      "notIncludes": [
        "グッズ"
      ]
    },
    "seoDifficulty": {
      "min": 1,
      "max": 100
    },
    "searchVolume": {
      "min": 100,
      "max": 10000
    },
    "cpc": {
      "min": 0.5,
      "max": 10
    },
    "competition": {
      "min": 1,
      "max": 100
    },
    "firstSeenRange": {
      "include": "last_30_days"
    }
  },
  "sortBy": "searchVolume",
  "orderBy": "desc",
  "limit": 100
}
```

### RelatedKeywordsResponseDto

- **Type:**`object`

* **`data` (required)**

  `object` — 関連キーワード検索結果データ

  - **`items` (required)**

    `array` — 関連キーワードのリスト。各アイテムにキーワード・SEO指標を含む。

    **Items:**

    - **`keyword` (required)**

      `string` — 検索キーワードを元に取得した関連キーワード

    - **`metrics` (required)**

      `object` — SEO関連の各種指標（検索ボリューム・SEO難易度・CPC・競合性・出現時期）

      - **`competition` (required)**

        `object` — 広告競合性。0–100で表し、高いほど競合性が高い（0–33:低 / 34–66:中 / 67–100:高）。

      - **`cpc` (required)**

        `object` — 推定クリック単価（USD）

      - **`firstSeenRange` (required)**

        `object` — 出現時期。キーワードが最初にラッコキーワードデータベースで検出された時期を日付範囲ラベルで表す。不明な場合は null。

      - **`searchVolume` (required)**

        `object` — 月間検索数（年平均）

      - **`seoDifficulty` (required)**

        `object` — SEO難易度。1–100で表し、高いほど難易度が高い（1–33:低 / 34–66:中 / 67–100:高）。不明な場合は null。

  - **`query` (required)**

    `object` — リクエストで指定された検索クエリ情報

    - **`keyword` (required)**

      `string` — 関連キーワード取得の元になった検索キーワード

  - **`summary` (required)**

    `object` — 件数サマリー（全体件数とレスポンスに含まれる件数）

    - **`returnedCount` (required)**

      `number` — このレスポンスに含まれている件数

    - **`totalCount` (required)**

      `number` — 取得対象全体の件数

* **`errors` (required)**

  `array` — エラーメッセージの配列。正常時は空配列。

  **Items:**

  `string`

* **`meta` (required)**

  `object` — リクエストに関するメタ情報（課金・消費リソースなど）

  - **`consumedCredit` (required)**

    `number` — このリクエストで消費されたクレジット数。

* **`result` (required)**

  `boolean` — API 呼び出しの成否。正常時は true、エラー時は false。

**Example:**

```json
{
  "result": true,
  "meta": {
    "consumedCredit": 1.5
  },
  "data": {
    "query": {
      "keyword": "ラッコ"
    },
    "summary": {
      "totalCount": 150,
      "returnedCount": 100
    },
    "items": [
      {
        "keyword": "ラッコ 水族館",
        "metrics": {
          "seoDifficulty": 40,
          "searchVolume": 90500,
          "cpc": 0,
          "competition": 1,
          "firstSeenRange": "last_30_days"
        }
      }
    ]
  },
  "errors": []
}
```

### OtherKeywordsDto

- **Type:**`object`

* **`keyword` (required)**

  `string` — 潜在的な検索キーワード（LSI）および関連する質問（PAA）を取得するための検索キーワード。1文字以上の文字列を指定する。

* **`orderBy`**

  `string`, possible values: `"asc", "desc"`, default: `"desc"` — ソート順。asc: 昇順 / desc: 降順。省略時は desc。

* **`sortBy`**

  `string`, possible values: `"importance", "seoDifficulty", "searchVolume", "cpc", "competition", "firstSeenRange"`, default: `"importance"` — 結果のソート項目。importance / seoDifficulty / searchVolume / cpc / competition / firstSeenRange。省略時は importance。

**Example:**

```json
{
  "keyword": "ラッコ",
  "sortBy": "importance",
  "orderBy": "desc"
}
```

### OtherKeywordsResponseDto

- **Type:**`object`

* **`data` (required)**

  `object` — 潜在的な検索キーワード/関連する質問の検索結果データ

  - **`items` (required)**

    `array` — LSI/PAA アイテムのリスト。LSI アイテムが先に、PAA アイテムが後に並ぶ。各アイテムに種別・重要度・取得元キーワードを含み、LSI の場合は SEO 指標も含まれる。

    **Items:**

    - **`importance` (required)**

      `string`, possible values: `"low", "medium", "high"` — 重要度。高いほど関連性や注目度が高いことを示す。high: 高 / medium: 中 / low: 低。

    - **`sourceKeyword` (required)**

      `string` — このキーワードまたは質問の取得元となったキーワード

    - **`type` (required)**

      `string`, possible values: `"lsi", "paa"` — データ種別。lsi: 潜在的な検索キーワード / paa: 関連する質問。

    - **`keyword`**

      `string` — 取得した潜在的な検索キーワード。type が lsi の場合に含まれる。

    - **`metrics`**

      `object` — SEO関連の各種指標。type が lsi の場合のみ含まれる。

      - **`competition` (required)**

        `object` — 広告競合性。0–100で表し、高いほど競合性が高い（0–33:低 / 34–66:中 / 67–100:高）。

      - **`cpc` (required)**

        `object` — 推定クリック単価（USD）

      - **`firstSeenRange` (required)**

        `object` — 出現時期。キーワードが最初にラッコキーワードデータベースで検出された時期を日付範囲ラベルで表す。不明な場合は null。

      - **`searchVolume` (required)**

        `object` — 月間検索数（年平均）

      - **`seoDifficulty` (required)**

        `object` — SEO難易度。1–100で表し、高いほど難易度が高い（1–33:低 / 34–66:中 / 67–100:高）。不明な場合は null。

    - **`question`**

      `string` — 取得した関連する質問。type が paa の場合に含まれる。

  - **`query` (required)**

    `object` — リクエストで指定された検索クエリ情報

    - **`keyword` (required)**

      `string` — 潜在的な検索キーワード/質問（LSI/PAA）取得の元になった検索キーワード

  - **`summary` (required)**

    `object` — LSI/PAA の件数サマリー

    - **`lsiCount` (required)**

      `number` — LSI（潜在的な検索キーワード）の件数

    - **`paaCount` (required)**

      `number` — PAA（People Also Ask / 関連する質問）の件数

* **`errors` (required)**

  `array` — エラーメッセージの配列。正常時は空配列。

  **Items:**

  `string`

* **`meta` (required)**

  `object` — リクエストに関するメタ情報（課金・消費リソースなど）

  - **`consumedCredit` (required)**

    `number` — このリクエストで消費されたクレジット数。

* **`result` (required)**

  `boolean` — API 呼び出しの成否。正常時は true、エラー時は false。

**Example:**

```json
{
  "result": true,
  "meta": {
    "consumedCredit": 22.5
  },
  "data": {
    "query": {
      "keyword": "ラッコ"
    },
    "summary": {
      "lsiCount": 1,
      "paaCount": 1
    },
    "items": [
      {
        "type": "lsi",
        "keyword": "ラッコ 水族館",
        "question": "ラッコはどこで見れますか？",
        "importance": "high",
        "sourceKeyword": "ラッコ",
        "metrics": {
          "seoDifficulty": 30,
          "searchVolume": 33100,
          "cpc": 2.17,
          "competition": 5,
          "firstSeenRange": "last_30_days"
        }
      }
    ]
  },
  "errors": []
}
```

### SearchQuestionDto

- **Type:**`object`

* **`keyword` (required)**

  `string` — よくある質問検索の元となる検索キーワード。1文字以上の文字列を指定する。

* **`filter`**

  `object` — 結果のフィルタリング条件。質問文・相対需要・出現時期などで絞り込む。

  - **`firstSeenRange`**

    `object` — 出現時期フィルタ

    - **`include`**

      `string`, possible values: `"last_7_days", "last_30_days", "last_90_days", "within_6_months", "within_1_year", "over_1_year"` — 出現時期の選択肢

  - **`keyword`**

    `object` — キーワードフィルタ（含む/含まない質問文の指定）

    - **`includes`**

      `array` — 含む単語のリスト（複数入力時はOR）

      **Items:**

      `string`

    - **`notIncludes`**

      `array` — 含まない単語のリスト（複数入力時はOR）

      **Items:**

      `string`

  - **`relativeDemand`**

    `object` — 相対需要フィルタ（1〜100の範囲指定）

    - **`max`**

      `integer` — 相対需要スコアの最大値

    - **`min`**

      `integer` — 相対需要スコアの最小値

* **`limit`**

  `integer`, default: `100` — 出力数の上限。1〜1000 の整数を指定。省略時は 100。

* **`orderBy`**

  `string`, possible values: `"asc", "desc"`, default: `"desc"` — ソート順。asc: 昇順 / desc: 降順。省略時は desc。

* **`sortBy`**

  `string`, possible values: `"relativeDemand", "firstSeenRange"`, default: `"relativeDemand"` — 結果のソート項目。relativeDemand: 相対需要 / firstSeenRange: 出現時期。省略時は relativeDemand。

**Example:**

```json
{
  "keyword": "ラッコ",
  "filter": {
    "keyword": {
      "includes": [
        "水族館"
      ],
      "notIncludes": [
        "グッズ"
      ]
    },
    "relativeDemand": {
      "min": 34,
      "max": 66
    },
    "firstSeenRange": {
      "include": "last_30_days"
    }
  },
  "sortBy": "relativeDemand",
  "orderBy": "desc",
  "limit": 100
}
```

### SearchQuestionResponseDto

- **Type:**`object`

* **`data` (required)**

  `object` — よくある質問検索結果データ

  - **`items` (required)**

    `array` — 質問アイテムのリスト

    **Items:**

    - **`metrics` (required)**

      `object` — 質問の各種指標（相対需要・出現時期）

      - **`firstSeenRange` (required)**

        `object` — 出現時期。質問が最初にラッコキーワードデータベースで検出された時期を日付範囲ラベルで表す。不明な場合は null。

      - **`relativeDemand` (required)**

        `number` — 相対需要。検索結果内での相対的な需要の高さ（1〜100）。高いほどよく見られている質問。

    - **`question` (required)**

      `string` — 検索キーワードに関連する質問

  - **`query` (required)**

    `object` — 検索クエリ情報

    - **`keyword` (required)**

      `string` — よくある質問検索の元になった検索キーワード

    - **`limit` (required)**

      `integer` — リクエストで指定された出力数の上限

    - **`orderBy` (required)**

      `string`, possible values: `"asc", "desc"` — リクエストで指定されたソート順。asc: 昇順 / desc: 降順。

    - **`sortBy` (required)**

      `string`, possible values: `"relativeDemand", "firstSeenRange"` — リクエストで指定されたソート項目。relativeDemand: 相対需要 / firstSeenRange: 出現時期。

    - **`filter`**

      `object` — リクエストで指定された絞り込み条件（質問文・相対需要・出現時期）。指定がない場合は省略される。

      - **`firstSeenRange`**

        `object` — 出現時期フィルタ

        - **`include`**

          `string`, possible values: `"last_7_days", "last_30_days", "last_90_days", "within_6_months", "within_1_year", "over_1_year"` — 出現時期の選択肢

      - **`keyword`**

        `object` — キーワードフィルタ（含む/含まない質問文の指定）

        - **`includes`**

          `array` — 含む単語のリスト（複数入力時はOR）

          **Items:**

          `string`

        - **`notIncludes`**

          `array` — 含まない単語のリスト（複数入力時はOR）

          **Items:**

          `string`

      - **`relativeDemand`**

        `object` — 相対需要フィルタ（1〜100の範囲指定）

        - **`max`**

          `integer` — 相対需要スコアの最大値

        - **`min`**

          `integer` — 相対需要スコアの最小値

  - **`summary` (required)**

    `object` — 件数サマリー（全体件数とレスポンスに含まれる件数）

    - **`returnedCount` (required)**

      `number` — このレスポンスに含まれている件数

    - **`totalCount` (required)**

      `number` — 取得対象全体の件数

* **`errors` (required)**

  `array` — エラーメッセージの配列。正常時は空配列。

  **Items:**

  `string`

* **`meta` (required)**

  `object` — リクエストに関するメタ情報（課金・消費リソースなど）

  - **`consumedCredit` (required)**

    `number` — このリクエストで消費されたクレジット数

* **`result` (required)**

  `boolean` — API 呼び出しの成否。正常時は true、エラー時は false。

**Example:**

```json
{
  "result": true,
  "meta": {
    "consumedCredit": 1.5
  },
  "data": {
    "query": {
      "keyword": "ラッコ",
      "filter": {
        "keyword": {
          "includes": [
            "水族館"
          ],
          "notIncludes": [
            "グッズ"
          ]
        },
        "relativeDemand": {
          "min": 34,
          "max": 66
        },
        "firstSeenRange": {
          "include": "last_30_days"
        }
      },
      "sortBy": "relativeDemand",
      "orderBy": "desc",
      "limit": 100
    },
    "summary": {
      "totalCount": 150,
      "returnedCount": 100
    },
    "items": [
      {
        "question": "ラッコが絶滅しそうな理由は何ですか?",
        "metrics": {
          "relativeDemand": 87,
          "firstSeenRange": "last_30_days"
        }
      }
    ]
  },
  "errors": []
}
```

### RankingKeywordsDto

- **Type:**`object`

* **`keyword` (required)**

  `string` — 同時ランクインキーワード取得の元となる検索キーワード。指定キーワードの検索上位URLが他にランクインしているキーワードを取得する。1文字以上の文字列を指定する。

* **`filter`**

  `object` — 結果のフィルタリング条件。キーワード・SEO難易度・月間検索数・CPC・競合性・関連度で絞り込む。

  - **`competition`**

    `object` — 競合性フィルタ（0〜100の範囲指定）

    - **`max`**

      `integer` — 最大値

    - **`min`**

      `integer` — 最小値

  - **`cpc`**

    `object` — クリック単価（CPC）フィルタ（USD、範囲指定）

    - **`max`**

      `number` — 最大CPC

    - **`min`**

      `number` — 最小CPC

  - **`keyword`**

    `object` — キーワードフィルタ

    - **`includes`**

      `array` — 含む単語のリスト（複数入力時はOR）

      **Items:**

      `string`

    - **`notIncludes`**

      `array` — 含まない単語のリスト（複数入力時はOR）

      **Items:**

      `string`

  - **`relevance`**

    `object` — 関連度フィルタ（0〜100の範囲指定）

    - **`max`**

      `integer` — 最大値

    - **`min`**

      `integer` — 最小値

  - **`searchVolume`**

    `object` — 月間検索数フィルタ（範囲指定）

    - **`max`**

      `integer` — 最大値

    - **`min`**

      `integer` — 最小値

  - **`seoDifficulty`**

    `object` — SEO難易度フィルタ（0〜100の範囲指定）

    - **`max`**

      `integer` — 最大値

    - **`min`**

      `integer` — 最小値

* **`limit`**

  `integer`, default: `500` — 取得件数。1〜5000 の整数を指定する。省略時は 500。

* **`orderBy`**

  `string`, possible values: `"asc", "desc"`, default: `"desc"` — ソート順。asc: 昇順 / desc: 降順。省略時は desc。

* **`searchRange`**

  `object`, default: `50` — 検索順位範囲。searchTopで指定したページがGoogle上位表示できているキーワードのうち、この順位以内にランクインしているキーワードを対象にする。選択肢: 10 / 20 / 30 / 50 / 100。省略時は 50。

* **`searchTop`**

  `object`, default: `20` — 検索上位ページの参照数。そのキーワードでGoogle検索で上位表示できているページのうち、上位何件のURLを同時ランクイン判定に使用するかを指定する。選択肢: 3 / 5 / 10 / 20 / 30 / 50。省略時は 20。値を大きくすると、Google検索でより下位にランクインしている＝検索意図とズレのより大きいページが調査対象となる。値を小さくすると、Google検索でより上位にランクインしている＝検索意図に一致するページのみが調査対象となる。

* **`sortBy`**

  `string`, possible values: `"seoDifficulty", "searchVolume", "cpc", "competition", "relevance"`, default: `"relevance"` — 結果のソート項目。seoDifficulty / searchVolume / cpc / competition / relevance。省略時は relevance。

**Example:**

```json
{
  "keyword": "ラッコ",
  "searchTop": 20,
  "searchRange": 50,
  "filter": {
    "keyword": {
      "includes": [
        "水族館"
      ],
      "notIncludes": [
        "グッズ"
      ]
    },
    "seoDifficulty": {
      "min": 1,
      "max": 100
    },
    "searchVolume": {
      "min": 100,
      "max": 10000
    },
    "cpc": {
      "min": 0.5,
      "max": 10
    },
    "competition": {
      "min": 1,
      "max": 100
    },
    "relevance": {
      "min": 1,
      "max": 100
    }
  },
  "sortBy": "relevance",
  "orderBy": "desc",
  "limit": 500
}
```

### RankingKeywordsResponseDto

- **Type:**`object`

* **`data` (required)**

  `object` — 同時ランクインキーワード検索結果データ

  - **`items` (required)**

    `array` — 同時ランクインキーワード結果のリスト。各アイテムにキーワード・単語数・SEO指標を含む。

    **Items:**

    - **`keyword` (required)**

      `string` — 同時ランクインしているキーワード

    - **`metrics` (required)**

      `object` — SEO関連の各種指標（SEO難易度・月間検索数・CPC・競合性・関連度）

      - **`competition` (required)**

        `number` — 広告競合性。0–100で表し、高いほど競合性が高い（0–33:低 / 34–66:中 / 67–100:高）。

      - **`cpc` (required)**

        `number` — 推定クリック単価（USD）

      - **`relevance` (required)**

        `number` — 同時ランクイン度。1–100で表し、高いほど元キーワードと検索結果の重複度が高いことを示す。

      - **`searchVolume` (required)**

        `number` — 月間検索数（年平均）

      - **`seoDifficulty` (required)**

        `object` — SEO難易度。1–100で表し、高いほど難易度が高い（1–33:低 / 34–66:中 / 67–100:高）。不明な場合は null。

    - **`wordCount` (required)**

      `number` — キーワードのスペース区切りの単語数

  - **`query` (required)**

    `object` — リクエストで指定された検索クエリ情報

    - **`keyword` (required)**

      `string` — 同時ランクインキーワード取得の元になった検索キーワード

  - **`summary` (required)**

    `object` — 件数サマリー（全体件数とレスポンスに含まれる件数）

    - **`returnedCount` (required)**

      `number` — このレスポンスに含まれている件数

    - **`totalCount` (required)**

      `number` — 取得対象全体の件数

* **`errors` (required)**

  `array` — エラーメッセージの配列。正常時は空配列。

  **Items:**

  `string`

* **`meta` (required)**

  `object` — リクエストに関するメタ情報（課金・消費リソースなど）

  - **`consumedCredit` (required)**

    `number` — このリクエストで消費されたクレジット数。

* **`result` (required)**

  `boolean` — API 呼び出しの成否。正常時は true、エラー時は false。

**Example:**

```json
{
  "result": true,
  "meta": {
    "consumedCredit": 4.5
  },
  "data": {
    "query": {
      "keyword": "ラッコ"
    },
    "summary": {
      "totalCount": 150,
      "returnedCount": 100
    },
    "items": [
      {
        "keyword": "ラッコ 水族館",
        "wordCount": 2,
        "metrics": {
          "seoDifficulty": 30,
          "searchVolume": 10000,
          "cpc": 0.5,
          "competition": 32,
          "relevance": 5
        }
      }
    ]
  },
  "errors": []
}
```

### SearchVolumeHistoryDto

- **Type:**`object`

* **`keywords` (required)**

  `array` — キーワード（入力上限50,000件）

  **Items:**

  `string`

* **`aggregationPeriodMonths`**

  `object`, default: `12` — 集計期間（月数）。12/24/36/48 のいずれか。省略時は 12。

* **`dataCompletion`**

  `boolean`, default: `true` — データ補完フラグ。true の場合にデータ補完を行う。省略時は true。

* **`deduplicate`**

  `boolean`, default: `true` — キーワードの重複除去を行うかどうか。省略時は true。

* **`language`**

  `string`, default: `"Japanese"` — 言語名。指定可能な言語名は metadata の languages 一覧を参照。省略時は Japanese。

* **`location`**

  `string`, default: `"Japan"` — 地域名。省略時は Japan。 - 指定可能な地域名は metadata の locations 一覧を参照（一覧は国レベルのみ） - 市区町村レベルの地域も指定可能。「市区町村名,上位地域名,国名」のようにカンマ区切りの正式名で指定する（例: Shibuya,Tokyo,Japan） - 途中の階層のみ（例: 都道府県のみ）の指定は未サポート

* **`seoDifficulty`**

  `boolean`, default: `false` — SEO難易度取得フラグ。true の場合にSEO難易度を取得する。省略時は false。

**Example:**

```json
{
  "keywords": [
    "ラッコ",
    "カワウソ"
  ],
  "seoDifficulty": false,
  "dataCompletion": true,
  "location": "Japan",
  "language": "Japanese",
  "deduplicate": true,
  "aggregationPeriodMonths": 12
}
```

### SearchVolumeHistoryResponseDto

- **Type:**`object`

* **`data` (required)**

  `object` — 履歴登録結果

  - **`requestId`**

    `number` — リクエストID

* **`errors` (required)**

  `array` — エラーメッセージの配列。正常時は空配列。

  **Items:**

  `string`

* **`meta` (required)**

  `object` — リクエストに関するメタ情報（課金・消費リソースなど）

  - **`consumedCredit` (required)**

    `number` — このリクエストで消費されたクレジット数。

* **`result` (required)**

  `boolean` — API 呼び出しの成否。正常時は true、エラー時は false。

**Example:**

```json
{
  "result": true,
  "meta": {
    "consumedCredit": 10
  },
  "data": {
    "requestId": 1234567
  },
  "errors": []
}
```

### SearchVolumeHistoryOverallStatus

- **Type:**`string`

**Example:**

### SearchVolumeHistoriesResponseDto

- **Type:**`object`

* **`data` (required)**

  `object` — 一括キーワード調査履歴一覧データ

  - **`items` (required)**

    `array` — 一括キーワード調査履歴アイテムのリスト

    **Items:**

    - **`aggregationPeriodMonths` (required)**

      `number` — 集計期間（月数）

    - **`completedAt` (required)**

      `object` — 全処理完了日時（ISO 8601、UTC）。未完了時は null。

    - **`createdAt` (required)**

      `string`, format: `date-time` — リクエスト作成日時（ISO 8601、UTC）

    - **`dataCompletion` (required)**

      `boolean` — データ補完が有効かどうか

    - **`keywordCount` (required)**

      `number` — キーワードの件数

    - **`keywordSummary` (required)**

      `string` — キーワードのサマリ（カンマ区切り、先頭20件・255文字以内で切り詰め）

    - **`language` (required)**

      `string` — 言語名。Google Ads API の LanguageCriterion に準拠。

    - **`location` (required)**

      `string` — 地域名。Google Ads API の LocationCriterion に準拠。

    - **`requestId` (required)**

      `number` — リクエストID

    - **`seoDifficulty` (required)**

      `boolean` — SEO難易度取得が有効かどうか

    - **`status` (required)**

      `string`, possible values: `"completed", "processing"` — 全体ステータス。statuses の searchVolume と seoDifficulty の両方が processed の場合に completed（seoDifficulty が skip の場合も完了扱い）、それ以外は processing。noiseReduction は判定対象外。

    - **`statuses` (required)**

      `object` — 各処理のステータス情報

      - **`noiseReduction` (required)**

        `string`, possible values: `"unprocessed", "processing", "processed"` — ノイズ除去ステータス。unprocessed: 未処理 / processing: 処理中 / processed: 完了。ノイズ除去には時間がかかる可能性があります。

      - **`searchVolume` (required)**

        `string`, possible values: `"unprocessed", "processing", "processed"` — 月間検索数取得ステータス。unprocessed: 未処理 / processing: 処理中 / processed: 完了。

      - **`seoDifficulty` (required)**

        `string`, possible values: `"skip", "unprocessed", "processing", "processed"` — SEO難易度取得ステータス。unprocessed: 未処理 / processing: 処理中 / processed: 完了 / skip: スキップ（SEO難易度取得OFFの場合）。

  - **`query` (required)**

    `object` — リクエストで指定されたクエリパラメータ

    - **`limit` (required)**

      `number` — リクエストで指定された取得件数

    - **`offset` (required)**

      `number` — リクエストで指定された取得開始位置

    - **`status` (required)**

      `object` — リクエストで指定されたステータスフィルタ

  - **`summary` (required)**

    `object` — 件数サマリ

    - **`returnedCount` (required)**

      `number` — このレスポンスに含まれている件数

    - **`totalCount` (required)**

      `number` — 取得対象全体の件数

* **`errors` (required)**

  `array` — エラーメッセージの配列。正常時は空配列。

  **Items:**

  `string`

* **`meta` (required)**

  `object` — リクエストに関するメタ情報（課金・消費リソースなど）

  - **`consumedCredit` (required)**

    `number` — このリクエストで消費されたクレジット数。

* **`result` (required)**

  `boolean` — API 呼び出しの成否。正常時は true、エラー時は false。

**Example:**

```json
{
  "result": true,
  "meta": {
    "consumedCredit": 0
  },
  "data": {
    "query": {
      "limit": 100,
      "offset": 0,
      "status": null
    },
    "summary": {
      "totalCount": 1,
      "returnedCount": 1
    },
    "items": [
      {
        "requestId": 1500,
        "createdAt": "2026-05-31T01:00:00.000Z",
        "completedAt": null,
        "status": "processing",
        "statuses": {
          "searchVolume": "processed",
          "seoDifficulty": "unprocessed",
          "noiseReduction": "processing"
        },
        "keywordSummary": "ラッコ,カワウソ",
        "keywordCount": 2,
        "seoDifficulty": true,
        "location": "Japan",
        "language": "Japanese",
        "aggregationPeriodMonths": 12,
        "dataCompletion": true
      }
    ]
  },
  "errors": []
}
```

### SearchVolumeStatusResponseDto

- **Type:**`object`

* **`data` (required)**

  `object` — ステータス情報

  - **`isCompleted`**

    `boolean` — 全処理完了フラグ。searchVolume が processed かつ seoDifficulty が processed または skip の場合に true。noiseReduction は判定対象外。

  - **`statuses`**

    `object` — 各処理のステータス情報

    - **`noiseReduction`**

      `string`, possible values: `"unprocessed", "processing", "processed"` — ノイズ除去ステータス。unprocessed: 未処理 / processing: 処理中 / processed: 完了。ノイズ除去には時間がかかる可能性があります。

    - **`searchVolume`**

      `string`, possible values: `"unprocessed", "processing", "processed"` — 月間検索数取得ステータス。unprocessed: 未処理 / processing: 処理中 / processed: 完了。

    - **`seoDifficulty`**

      `string`, possible values: `"skip", "unprocessed", "processing", "processed"` — SEO難易度取得ステータス。unprocessed: 未処理 / processing: 処理中 / processed: 完了 / skip: スキップ。

* **`errors` (required)**

  `array` — エラーメッセージの配列。正常時は空配列。

  **Items:**

  `string`

* **`meta` (required)**

  `object` — リクエストに関するメタ情報（課金・消費リソースなど）

  - **`consumedCredit` (required)**

    `number` — このリクエストで消費されたクレジット数。

* **`result` (required)**

  `boolean` — API 呼び出しの成否。正常時は true、エラー時は false。

**Example:**

```json
{
  "result": true,
  "meta": {
    "consumedCredit": 0
  },
  "data": {
    "isCompleted": true,
    "statuses": {
      "searchVolume": "processed",
      "noiseReduction": "processing",
      "seoDifficulty": "skip"
    }
  },
  "errors": []
}
```

### SearchVolumeResultsDto

- **Type:**`object`

* **`filter`**

  `object` — 結果のフィルタリング条件。キーワード・SEO難易度・月間検索数・CPC・競合性で絞り込む。

  - **`competition`**

    `object` — 競合性フィルタ（0〜100の範囲指定）

    - **`max`**

      `integer` — 最大値

    - **`min`**

      `integer` — 最小値

  - **`cpc`**

    `object` — CPC（$）フィルタ（範囲指定）

    - **`max`**

      `number` — 最大CPC

    - **`min`**

      `number` — 最小CPC

  - **`firstSeenRange`**

    `object` — 出現時期フィルタ

    - **`include`**

      `string`, possible values: `"last_7_days", "last_30_days", "last_90_days", "within_6_months", "within_1_year", "over_1_year"` — 出現時期の選択肢

  - **`keyword`**

    `object` — キーワードフィルタ（含む/含まないキーワード指定）

    - **`includes`**

      `array` — 含む単語のリスト（複数入力時はOR）

      **Items:**

      `string`

    - **`notIncludes`**

      `array` — 含まない単語のリスト（複数入力時はOR）

      **Items:**

      `string`

  - **`searchVolume`**

    `object` — 月間検索数フィルタ（範囲指定）

    - **`max`**

      `integer` — 最大値

    - **`min`**

      `integer` — 最小値

  - **`seoDifficulty`**

    `object` — SEO難易度フィルタ（0〜100の範囲指定）

    - **`max`**

      `integer` — 最大値

    - **`min`**

      `integer` — 最小値

* **`limit`**

  `integer`, default: `100` — 取得件数。1〜50,000の整数を指定する。省略時は 100。

* **`noiseReduction`**

  `boolean`, default: `true` — ノイズ除去フラグ。true の場合にノイズ除去を適用する。省略時は true。

* **`orderBy`**

  `string`, possible values: `"asc", "desc"`, default: `"desc"` — ソート順。asc: 昇順 / desc: 降順。省略時は desc。

* **`sortBy`**

  `string`, possible values: `"keyword", "seoDifficulty", "searchVolume", "rateOfChange", "cpc", "competition", "firstSeenRange"`, default: `"searchVolume"` — ソート項目。keyword / seoDifficulty / searchVolume / rateOfChange / cpc / competition / firstSeenRange。省略時は searchVolume。

**Example:**

```json
{
  "noiseReduction": true,
  "filter": {
    "keyword": {
      "includes": [
        "水族館"
      ],
      "notIncludes": [
        "グッズ"
      ]
    },
    "seoDifficulty": {
      "min": 1,
      "max": 100
    },
    "searchVolume": {
      "min": 100,
      "max": 10000
    },
    "cpc": {
      "min": 0.5,
      "max": 10
    },
    "competition": {
      "min": 1,
      "max": 100
    },
    "firstSeenRange": {
      "include": "last_30_days"
    }
  },
  "sortBy": "searchVolume",
  "orderBy": "desc",
  "limit": 100
}
```

### SearchVolumeResultsResponseDto

- **Type:**`object`

* **`data` (required)**

  `object` — 検索ボリューム結果データ

  - **`items`**

    `array` — 検索結果アイテムのリスト

    **Items:**

    - **`dataSource` (required)**

      `object` — 検索数データの取得元。取得できなかった場合は null。

    - **`keyword` (required)**

      `string` — キーワード

    - **`metrics` (required)**

      `object` — 各種指標（SEO難易度・月間検索数・CPC・広告競合性）

      - **`competition` (required)**

        `object` — 広告競合性。0–100で表し、高いほど競合性が高い。（0–33:低 / 34–66:中 / 67–100:高） 無効な場合は null。

      - **`cpc` (required)**

        `object` — 推定クリック単価（USD）。無効な場合は null。

      - **`firstSeenRange` (required)**

        `object` — 出現時期。キーワードが最初にラッコキーワードデータベースで検出された時期を日付範囲ラベルで表す。不明な場合は null。

      - **`searchVolume` (required)**

        `object` — 月間検索数（年平均）。無効な場合は null。

      - **`seoDifficulty` (required)**

        `object` — SEO難易度。1–100で表し、高いほど難易度が高い。（1–33:低 / 34–66:中 / 67–100:高）不明な場合は null。

    - **`trends` (required)**

      `object` — 検索数トレンド（増減率・月別検索数）

      - **`changeRate` (required)**

        `object` — 検索数の増減率（3か月・6か月・12か月）

        - **`12m` (required)**

          `object` — 直近12か月（直近月を含む）の平均に対する直近月の検索数増減率。集計期間に関わらず固定12か月。パーセントではなく比率（0.1 = +10%、1.0 = +100%）。12か月分のデータが無い場合は null。対象期間の検索数がすべて0の場合は0。

        - **`3m` (required)**

          `object` — 直近3か月（直近月を含む）の平均に対する直近月の検索数増減率。集計期間に関わらず固定3か月。パーセントではなく比率（0.1 = +10%、1.0 = +100%）。3か月分のデータが無い場合は null。対象期間の検索数がすべて0の場合は0。

        - **`6m` (required)**

          `object` — 直近6か月（直近月を含む）の平均に対する直近月の検索数増減率。集計期間に関わらず固定6か月。パーセントではなく比率（0.1 = +10%、1.0 = +100%）。6か月分のデータが無い場合は null。対象期間の検索数がすべて0の場合は0。

        - **`yoy1y` (required)**

          `object` — 1年前同月比（集計期間24か月以上で算出）

        - **`yoy2y` (required)**

          `object` — 2年前同月比（集計期間36か月以上で算出）

        - **`yoy3y` (required)**

          `object` — 3年前同月比（集計期間48か月以上で算出）

      - **`monthlySearchVolume` (required)**

        `object` — 月ごとの検索数。キーは YYYY-MM 形式。データがない場合は null。

  - **`query`**

    `object` — クエリ情報（リクエストID・地域・言語）

    - **`aggregationPeriodMonths` (required)**

      `number` — 集計期間（月数）

    - **`language` (required)**

      `string` — 月間検索数取得対象の言語

    - **`location` (required)**

      `string` — 月間検索数取得対象の地域

    - **`requestId` (required)**

      `number` — リクエストID

  - **`summary`**

    `object` — 件数サマリー

    - **`returnedCount` (required)**

      `number` — このレスポンスに含まれている件数

    - **`totalCount` (required)**

      `number` — 取得対象全体の件数

* **`errors` (required)**

  `array` — エラーメッセージの配列。正常時は空配列。

  **Items:**

  `string`

* **`meta` (required)**

  `object` — リクエストに関するメタ情報（課金・消費リソースなど）

  - **`consumedCredit` (required)**

    `number` — このリクエストで消費されたクレジット数。

* **`result` (required)**

  `boolean` — API 呼び出しの成否。正常時は true、エラー時は false。

**Example:**

```json
{
  "result": true,
  "meta": {
    "consumedCredit": 0
  },
  "data": {
    "query": {
      "requestId": 1234567,
      "location": "Japan",
      "language": "Japanese",
      "aggregationPeriodMonths": 12
    },
    "summary": {
      "totalCount": 150,
      "returnedCount": 100
    },
    "items": [
      {
        "keyword": "ラッコ",
        "dataSource": "GoogleLive",
        "metrics": {
          "seoDifficulty": 40,
          "searchVolume": 90500,
          "cpc": 0,
          "competition": 1,
          "firstSeenRange": "last_30_days"
        },
        "trends": {
          "changeRate": {
            "12m": 0.4159,
            "6m": 0.0796,
            "3m": -0.0695,
            "yoy1y": 0.1523,
            "yoy2y": -0.0845,
            "yoy3y": 0.2311
          },
          "monthlySearchVolume": {
            "2025-01": 2740000,
            "2025-02": 2240000
          }
        }
      }
    ]
  },
  "errors": []
}
```

### MetadataLocationsResponseDto

- **Type:**`object`

* **`data` (required)**

  `object` — 地域一覧

  - **`locations` (required)**

    `array` — 指定可能な地域の一覧（フィルタ未指定時は国レベルのみ・フィルタ指定時は市区町村レベルも含む）

    **Items:**

    - **`countryIsoCode` (required)**

      `string` — ISO 3166-1 alpha-2 国コード

    - **`name` (required)**

      `string` — 地域名

* **`errors` (required)**

  `array` — エラーメッセージの配列。正常時は空配列。

  **Items:**

  `string`

* **`meta` (required)**

  `object` — リクエストに関するメタ情報（課金・消費リソースなど）

  - **`consumedCredit` (required)**

    `number` — このリクエストで消費されたクレジット数。無料ツールのため常に 0。

* **`result` (required)**

  `boolean` — API 呼び出しの成否。正常時は true、エラー時は false。

**Example:**

```json
{
  "result": true,
  "meta": {
    "consumedCredit": 0
  },
  "data": {
    "locations": [
      {
        "name": "Japan",
        "countryIsoCode": "JP"
      }
    ]
  },
  "errors": []
}
```

### MetadataLanguagesResponseDto

- **Type:**`object`

* **`data` (required)**

  `object` — 言語一覧

  - **`languages` (required)**

    `array` — 指定可能な言語の一覧

    **Items:**

    - **`name` (required)**

      `string` — 言語名

* **`errors` (required)**

  `array` — エラーメッセージの配列。正常時は空配列。

  **Items:**

  `string`

* **`meta` (required)**

  `object` — リクエストに関するメタ情報（課金・消費リソースなど）

  - **`consumedCredit` (required)**

    `number` — このリクエストで消費されたクレジット数。無料ツールのため常に 0。

* **`result` (required)**

  `boolean` — API 呼び出しの成否。正常時は true、エラー時は false。

**Example:**

```json
{
  "result": true,
  "meta": {
    "consumedCredit": 0
  },
  "data": {
    "languages": [
      {
        "name": "Japanese"
      }
    ]
  },
  "errors": []
}
```

### InfluxKeywordsKeywordDto

- **Type:**`object`

* **`targets` (required)**

  `array` — 獲得キーワード調査の対象ドメインまたはURLとマッチタイプの配列。最大20件まで指定可能。各要素は { url, matchType } のオブジェクト。

  **Items:**

  - **`url` (required)**

    `string` — ドメインまたはURL

  - **`matchType`**

    `string`, possible values: `"url", "forward_url", "domain", "sub_domain"`, default: `"sub_domain"` — マッチタイプ。url: 完全一致URL / forward\_url: 前方一致URL / domain: ドメイン完全一致 / sub\_domain: サブドメイン含むドメイン一致。省略時は sub\_domain。

* **`filter`**

  `object` — 結果のフィルタリング条件。キーワード・SEO難易度・検索順位・月間検索数・CPC・競合性・推定流入数で絞り込む。

  - **`competition`**

    `object` — 広告競合性フィルタ（0〜100の範囲指定）

    - **`max`**

      `integer` — 最大値

    - **`min`**

      `integer` — 最小値

  - **`cpc`**

    `object` — CPC（$）フィルタ（範囲指定）

    - **`max`**

      `number` — 最大CPC

    - **`min`**

      `number` — 最小CPC

  - **`etv`**

    `object` — 推定流入数フィルタ（範囲指定）

    - **`max`**

      `integer` — 最大値

    - **`min`**

      `integer` — 最小値

  - **`keyword`**

    `object` — キーワードフィルタ（含む/含まないキーワード指定）

    - **`includes`**

      `array` — 含む単語のリスト（複数入力時はOR）

      **Items:**

      `string`

    - **`notIncludes`**

      `array` — 含まない単語のリスト（複数入力時はOR）

      **Items:**

      `string`

  - **`rank`**

    `object` — 検索順位フィルタ（1〜100の範囲指定）

    - **`max`**

      `integer` — 最大値

    - **`min`**

      `integer` — 最小値

  - **`searchVolume`**

    `object` — 月間検索数フィルタ（範囲指定）

    - **`max`**

      `integer` — 最大値

    - **`min`**

      `integer` — 最小値

  - **`seoDifficulty`**

    `object` — SEO難易度フィルタ（0〜100の範囲指定）

    - **`max`**

      `integer` — 最大値

    - **`min`**

      `integer` — 最小値

* **`keywordCollapse`**

  `boolean`, default: `false` — キーワード重複除去の有効/無効。true にすると同一キーワードの重複を除去する。省略時は false。

* **`limit`**

  `integer`, default: `100` — 取得件数。1〜10000 の整数を指定する。省略時は 100。

* **`orderBy`**

  `string`, possible values: `"asc", "desc"`, default: `"desc"` — ソート順。asc: 昇順 / desc: 降順。省略時は desc。

* **`sortBy`**

  `string`, possible values: `"keyword", "seoDifficulty", "rank", "searchVolume", "cpc", "competition", "etv"`, default: `"etv"` — ソート項目。keyword / seoDifficulty / rank / searchVolume / cpc / competition / etv。省略時は etv。

**Example:**

```json
{
  "targets": [
    {
      "url": "https://rakkokeyword.com/",
      "matchType": "sub_domain"
    }
  ],
  "keywordCollapse": false,
  "filter": {
    "keyword": {
      "includes": [
        "水族館"
      ],
      "notIncludes": [
        "グッズ"
      ]
    },
    "seoDifficulty": {
      "min": 1,
      "max": 100
    },
    "rank": {
      "min": 1,
      "max": 100
    },
    "searchVolume": {
      "min": 100,
      "max": 10000
    },
    "cpc": {
      "min": 0.5,
      "max": 10
    },
    "competition": {
      "min": 1,
      "max": 100
    },
    "etv": {
      "min": 100,
      "max": 10000
    }
  },
  "sortBy": "etv",
  "orderBy": "desc",
  "limit": 100
}
```

### InfluxKeywordsKeywordResponseDto

- **Type:**`object`

* **`data` (required)**

  `object` — 獲得キーワード調査結果データ

  - **`items` (required)**

    `array` — 獲得キーワード調査結果のリスト。各アイテムに対象・キーワード・指標・順位情報を含む。

    **Items:**

    - **`keyword` (required)**

      `string` — 対象が獲得しているSEOキーワード

    - **`metrics` (required)**

      `object` — キーワードの各種指標（SEO難易度・月間検索数・CPC・広告競合性）

      - **`competition` (required)**

        `number` — 広告競合性。0〜100 で表し、高いほど競合性が高い（0–33:低 / 34–66:中 / 67–100:高）

      - **`cpc` (required)**

        `number` — 推定クリック単価（USD）

      - **`searchVolume` (required)**

        `number` — 月間検索数（年平均）

      - **`seoDifficulty` (required)**

        `object` — SEO難易度。1–100で表し、高いほど難易度が高い（1–33:低 / 34–66:中 / 67–100:高）。不明な場合は null。

    - **`ranking` (required)**

      `object` — 検索順位情報（順位・推定流入数・ランクインURL）

      - **`estimatedTraffic` (required)**

        `number` — このキーワードからの推定検索流入数（月間）

      - **`position` (required)**

        `number` — 検索順位

      - **`url` (required)**

        `string` — ランクインしているURL

    - **`target` (required)**

      `string` — このキーワードを獲得している対象URLまたはドメイン

  - **`query` (required)**

    `object` — リクエストで指定されたクエリ情報

    - **`targets` (required)**

      `array` — 獲得キーワード調査の対象URLまたはドメイン一覧

      **Items:**

      `string`

  - **`summary` (required)**

    `object` — 集計サマリー（件数・推定流入数・キーワード数）

    - **`estimatedTraffic` (required)**

      `number` — 対象全体の推定検索流入数（月間）

    - **`keywordCount` (required)**

      `number` — ランクインしているキーワード数

    - **`returnedCount` (required)**

      `number` — このレスポンスに含まれている件数

    - **`totalCount` (required)**

      `number` — 取得対象全体の件数

* **`errors` (required)**

  `array` — エラーメッセージの配列。正常時は空配列。

  **Items:**

  `string`

* **`meta` (required)**

  `object` — リクエストに関するメタ情報（課金・消費リソースなど）

  - **`consumedCredit` (required)**

    `number` — このリクエストで消費されたクレジット数。

* **`result` (required)**

  `boolean` — API 呼び出しの成否。正常時は true、エラー時は false。

**Example:**

```json
{
  "result": true,
  "meta": {
    "consumedCredit": 4.5
  },
  "data": {
    "query": {
      "targets": [
        "https://example.com/"
      ]
    },
    "summary": {
      "totalCount": 983,
      "returnedCount": 100,
      "estimatedTraffic": 2824,
      "keywordCount": 983
    },
    "items": [
      {
        "target": "https://example.com/",
        "keyword": "ラッコ",
        "metrics": {
          "seoDifficulty": 30,
          "searchVolume": 10000,
          "cpc": 0,
          "competition": 0
        },
        "ranking": {
          "position": 1,
          "estimatedTraffic": 438,
          "url": "https://example.com/page"
        }
      }
    ]
  },
  "errors": []
}
```

### InfluxPagesDto

- **Type:**`object`

* **`targets` (required)**

  `array` — 獲得キーワード調査（ページ軸）の対象ドメインまたはURLとマッチタイプの配列。最大20件まで指定可能。

  **Items:**

  - **`url` (required)**

    `string` — ドメインまたはURL

  - **`matchType`**

    `string`, possible values: `"url", "forward_url", "domain", "sub_domain"`, default: `"sub_domain"` — マッチタイプ。url: 完全一致URL / forward\_url: 前方一致URL / domain: ドメイン完全一致 / sub\_domain: サブドメイン含むドメイン一致。省略時は sub\_domain。

* **`filter`**

  `object` — 結果のフィルタリング条件。合計推定流入数・キーワード数・合計集客価値・タイトル・URL・トップキーワード・SEO難易度で絞り込む。

  - **`keywordCount`**

    `object` — キーワード数フィルタ（範囲指定）

    - **`max`**

      `integer` — 最大値

    - **`min`**

      `integer` — 最小値

  - **`title`**

    `object` — タイトルフィルタ（含む/含まないキーワード指定）

    - **`includes`**

      `array` — 含む単語のリスト（複数入力時はOR）

      **Items:**

      `string`

    - **`notIncludes`**

      `array` — 含まない単語のリスト（複数入力時はOR）

      **Items:**

      `string`

  - **`topKeyword`**

    `object` — トップキーワードフィルタ（含む/含まないキーワード指定）

    - **`includes`**

      `array` — 含む単語のリスト（複数入力時はOR）

      **Items:**

      `string`

    - **`notIncludes`**

      `array` — 含まない単語のリスト（複数入力時はOR）

      **Items:**

      `string`

  - **`topSeoDifficulty`**

    `object` — SEO難易度フィルタ（0〜100の範囲指定）

    - **`max`**

      `integer` — 最大値

    - **`min`**

      `integer` — 最小値

  - **`totalEtv`**

    `object` — 合計推定流入数フィルタ（範囲指定）

    - **`max`**

      `integer` — 最大値

    - **`min`**

      `integer` — 最小値

  - **`totalTrafficValue`**

    `object` — 合計集客価値（USD）フィルタ（範囲指定）

    - **`max`**

      `integer` — 最大値

    - **`min`**

      `integer` — 最小値

  - **`url`**

    `object` — URLフィルタ（含む/含まないURL指定）

    - **`includes`**

      `array` — 含むURLのリスト（複数入力時はOR）

      **Items:**

      `string`

    - **`notIncludes`**

      `array` — 含まないURLのリスト（複数入力時はOR）

      **Items:**

      `string`

* **`limit`**

  `integer`, default: `100` — 取得件数。1〜10000 の整数を指定する。省略時は 100。

* **`orderBy`**

  `string`, possible values: `"asc", "desc"`, default: `"desc"` — ソート順。asc: 昇順 / desc: 降順。省略時は desc。

* **`sortBy`**

  `string`, possible values: `"totalEtv", "totalTrafficValue", "keywordCount"`, default: `"totalEtv"` — ソート項目。totalEtv / totalTrafficValue / keywordCount。省略時は totalEtv。

* **`topKeywordCollapse`**

  `boolean`, default: `false` — トップキーワード重複除去の有効/無効。true にすると同一トップキーワードの重複を除去する。省略時は false。

**Example:**

```json
{
  "targets": [
    {
      "url": "https://rakkokeyword.com/",
      "matchType": "sub_domain"
    }
  ],
  "topKeywordCollapse": false,
  "filter": {
    "totalEtv": {
      "min": 100,
      "max": 10000
    },
    "keywordCount": {
      "min": 100,
      "max": 10000
    },
    "totalTrafficValue": {
      "min": 100,
      "max": 10000
    },
    "title": {
      "includes": [
        "水族館"
      ],
      "notIncludes": [
        "グッズ"
      ]
    },
    "url": {
      "includes": [
        "https://rakkokeyword.com/"
      ],
      "notIncludes": [
        "https://rakkokeyword.com/result/"
      ]
    },
    "topKeyword": {
      "includes": [
        "水族館"
      ],
      "notIncludes": [
        "グッズ"
      ]
    },
    "topSeoDifficulty": {
      "min": 1,
      "max": 100
    }
  },
  "sortBy": "totalEtv",
  "orderBy": "desc",
  "limit": 100
}
```

### InfluxPagesResponseDto

- **Type:**`object`

* **`data` (required)**

  `object` — 獲得キーワード調査結果（ページ軸）データ

  - **`items` (required)**

    `array` — 獲得キーワード調査結果（ページ軸）のリスト。各アイテムに対象・ページ情報・パフォーマンス指標・トップキーワードを含む。

    **Items:**

    - **`page` (required)**

      `object` — ページ情報（タイトル・URL）

      - **`title` (required)**

        `string` — ページタイトル

      - **`url` (required)**

        `string` — ページURL

    - **`performance` (required)**

      `object` — パフォーマンス指標（ランクインキーワード数・推定流入数・集客価値）

      - **`estimatedTraffic` (required)**

        `number` — このページの推定検索流入数（月間）

      - **`rankingKeywordCount` (required)**

        `number` — このページでランクインしているキーワード数

      - **`trafficValue` (required)**

        `number` — このページの集客価値（USD）。推定流入数×CPC で算出される広告換算価値。

    - **`target` (required)**

      `string` — このページが属する対象URLまたはドメイン

    - **`topKeyword` (required)**

      `object` — トップキーワード情報（キーワード・順位・指標）

      - **`keyword` (required)**

        `string` — このページで最もSEO流入を獲得しているトップキーワード

      - **`metrics` (required)**

        `object` — トップキーワードの各種指標（SEO難易度・月間検索数）

        - **`searchVolume` (required)**

          `number` — トップキーワードの月間検索数（年平均）

        - **`seoDifficulty` (required)**

          `object` — SEO難易度。1–100で表し、高いほど難易度が高い（1–33:低 / 34–66:中 / 67–100:高）。不明な場合は null。

      - **`position` (required)**

        `number` — トップキーワードでの検索順位

  - **`query` (required)**

    `object` — リクエストで指定されたクエリ情報

    - **`targets` (required)**

      `array` — 獲得キーワード調査の対象URLまたはドメイン一覧

      **Items:**

      `string`

  - **`summary` (required)**

    `object` — 集計サマリー（件数・推定流入数・キーワード数）

    - **`estimatedTraffic` (required)**

      `number` — 対象全体の推定検索流入数（月間）

    - **`keywordCount` (required)**

      `number` — ランクインしているキーワード数

    - **`returnedCount` (required)**

      `number` — このレスポンスに含まれている件数

    - **`totalCount` (required)**

      `number` — 取得対象全体の件数

* **`errors` (required)**

  `array` — エラーメッセージの配列。正常時は空配列。

  **Items:**

  `string`

* **`meta` (required)**

  `object` — リクエストに関するメタ情報（課金・消費リソースなど）

  - **`consumedCredit` (required)**

    `number` — このリクエストで消費されたクレジット数。

* **`result` (required)**

  `boolean` — API 呼び出しの成否。正常時は true、エラー時は false。

**Example:**

```json
{
  "result": true,
  "meta": {
    "consumedCredit": 4.5
  },
  "data": {
    "query": {
      "targets": [
        "https://example.com/"
      ]
    },
    "summary": {
      "totalCount": 319,
      "returnedCount": 100,
      "estimatedTraffic": 2824,
      "keywordCount": 983
    },
    "items": [
      {
        "target": "https://example.com/",
        "page": {
          "title": "ラッコキーワード｜キーワード分析ツール",
          "url": "https://rakkokeyword.com/"
        },
        "performance": {
          "rankingKeywordCount": 2173,
          "estimatedTraffic": 10000,
          "trafficValue": 5000
        },
        "topKeyword": {
          "keyword": "ラッコ",
          "position": 1,
          "metrics": {
            "seoDifficulty": 30,
            "searchVolume": 10000
          }
        }
      }
    ]
  },
  "errors": []
}
```

### CompetitiveDto

- **Type:**`object`

* **`url` (required)**

  `string` — 競合分析を行う対象のドメインURL。対象サイトの競合サイトを抽出し、キーワード重複率や流入数などの指標を比較する。

* **`orderBy`**

  `string`, possible values: `"asc", "desc"`, default: `"desc"` — ソート順。asc: 昇順 / desc: 降順。省略時は desc。

* **`sortBy`**

  `string`, possible values: `"duplicate", "duplicateRate", "competitorUnique", "targetUnique", "etv", "keywordCount", "trafficValue", "pageCount"`, default: `"etv"` — ソート項目。duplicate / duplicateRate / competitorUnique / targetUnique / etv / keywordCount / trafficValue / pageCount。省略時は etv。

**Example:**

```json
{
  "url": "https://rakkokeyword.com/",
  "sortBy": "etv",
  "orderBy": "desc"
}
```

### CompetitiveResponseDto

- **Type:**`object`

* **`data` (required)**

  `object` — 競合サイト抽出結果データ

  - **`items` (required)**

    `array` — 競合サイト抽出結果のリスト。各アイテムにサイト情報と各種指標を含む。

    **Items:**

    - **`metrics` (required)**

      `object` — 競合サイトの各種指標（流入数・集客価値・キーワード数・重複率など）

      - **`competitorUniqueKeywordCount` (required)**

        `number` — 競合サイトにのみ存在し、入力対象サイトには存在しないキーワード数

      - **`duplicateKeywordCount` (required)**

        `number` — 入力対象サイトと競合サイトで重複しているキーワード数

      - **`duplicateRate` (required)**

        `number` — 重複キーワード率。0〜1 で表し、高いほど入力対象とのキーワード重複率が高い。

      - **`estimatedTraffic` (required)**

        `number` — 競合サイト全体の推定検索流入数（月間）

      - **`keywordCount` (required)**

        `number` — 競合サイトが獲得しているキーワード数

      - **`pageCount` (required)**

        `number` — 競合サイトのインデックスされたページ数

      - **`targetUniqueKeywordCount` (required)**

        `number` — 入力対象サイトにのみ存在し、競合サイトには存在しないキーワード数

      - **`trafficValue` (required)**

        `number` — 競合サイト全体の集客価値（USD）。推定流入数×CPC で算出される広告換算価値。

    - **`site` (required)**

      `object` — 競合サイト情報（ドメイン・タイトル）

      - **`domain` (required)**

        `string` — 競合サイトのドメイン名

      - **`title` (required)**

        `string` — 競合サイトのタイトル。SERP データから取得できない場合は空文字。

  - **`query` (required)**

    `object` — リクエストで指定されたクエリ情報

    - **`targets` (required)**

      `array` — 競合サイト抽出の対象URLまたはドメイン一覧

      **Items:**

      `string`

  - **`summary` (required)**

    `object` — 件数サマリー（全体件数とレスポンスに含まれる件数）

    - **`returnedCount` (required)**

      `number` — このレスポンスに含まれている件数

    - **`totalCount` (required)**

      `number` — 取得対象全体の件数

* **`errors` (required)**

  `array` — エラーメッセージの配列。正常時は空配列。

  **Items:**

  `string`

* **`meta` (required)**

  `object` — リクエストに関するメタ情報（課金・消費リソースなど）

  - **`consumedCredit` (required)**

    `number` — このリクエストで消費されたクレジット数。

* **`result` (required)**

  `boolean` — API 呼び出しの成否。正常時は true、エラー時は false。

**Example:**

```json
{
  "result": true,
  "meta": {
    "consumedCredit": 4.5
  },
  "data": {
    "query": {
      "targets": [
        "https://rakkoma.com/"
      ]
    },
    "summary": {
      "totalCount": 150,
      "returnedCount": 100
    },
    "items": [
      {
        "site": {
          "domain": "rakko.inc",
          "title": "ラッコ株式会社"
        },
        "metrics": {
          "estimatedTraffic": 15803,
          "trafficValue": 51386,
          "keywordCount": 119,
          "pageCount": 51,
          "duplicateKeywordCount": 119,
          "duplicateRate": 1,
          "competitorUniqueKeywordCount": 0,
          "targetUniqueKeywordCount": 596
        }
      }
    ]
  },
  "errors": []
}
```

### BulkSiteResearchDto

- **Type:**`object`

* **`urls` (required)**

  `array` — 一括サイト調査の対象URL一覧（1〜100件）。各URLの推定流入数・獲得キーワード数・ページ数の現在値と、その推移（0〜100指数）を取得する。

  **Items:**

  `string`

* **`urlMatchType`**

  `string`, possible values: `"url", "forward_url", "domain", "sub_domain"`, default: `"domain"` — URLのマッチタイプ。url: 完全一致 / forward\_url: 前方一致 / domain: ドメイン一致 / sub\_domain: サブドメイン一致。省略時は domain。

**Example:**

```json
{
  "urls": [
    "https://rakkokeyword.com/"
  ],
  "urlMatchType": "domain"
}
```

### BulkSiteResearchResponseDto

- **Type:**`object`

* **`data` (required)**

  `object` — 一括サイト調査結果データ

  - **`items` (required)**

    `array` — 一括サイト調査結果のリスト。入力 urls と同数・同順。

    **Items:**

    - **`distributions` (required)**

      `object` — ランク帯・流入数帯の分布

      - **`pageTraffic` (required)**

        `object` — ページの推定流入数分布（整形ラベル別の生の件数）。

        - **`0` (required)**

          `number`

        - **`1-100` (required)**

          `number`

        - **`1+` (required)**

          `number`

        - **`100+` (required)**

          `number`

        - **`1000+` (required)**

          `number`

        - **`10001+` (required)**

          `number`

        - **`1001-10000` (required)**

          `number`

        - **`101-1000` (required)**

          `number`

      - **`rankingPosition` (required)**

        `object` — キーワードの検索順位分布（整形ラベル別の生の件数）。

        - **`1-10` (required)**

          `number`

        - **`1-20` (required)**

          `number`

        - **`1-3` (required)**

          `number`

        - **`1-30` (required)**

          `number`

        - **`11-20` (required)**

          `number`

        - **`21-50` (required)**

          `number`

        - **`4-10` (required)**

          `number`

        - **`51-100` (required)**

          `number`

    - **`histories` (required)**

      `array` — 推移データ（0〜100指数・小数第2位・12点）。常に返却される。

      **Items:**

      - **`date` (required)**

        `string` — 各月末日（YYYY-MM-DD）。取得済み履歴中の最新月末を末尾に11ヶ月前までの12点。

      - **`etvIndex` (required)**

        `number` — 推定流入数の推移指数（0〜100・小数第2位）。系列内最大月を100とする比例スケール。

      - **`keywordCountIndex` (required)**

        `number` — 獲得キーワード数の推移指数（0〜100・小数第2位）。系列内最大月を100とする比例スケール。

      - **`pageCountIndex` (required)**

        `number` — ページ数の推移指数（0〜100・小数第2位）。系列内最大月を100とする比例スケール。

    - **`metrics` (required)**

      `object` — 現在値の各種指標（実数）。推移の指数（histories）とは別量。

      - **`averageEstimatedTrafficPerPage` (required)**

        `number` — 1ページ平均の推定流入数

      - **`averageRankingKeywordCountPerPage` (required)**

        `number` — 1ページ平均のランクインキーワード数

      - **`averageTrafficValuePerPage` (required)**

        `number` — 1ページ平均の集客価値（USD）

      - **`estimatedTraffic` (required)**

        `number` — 推定検索流入数（月間・生値・現在集計）

      - **`estimatedTrafficChangeRate` (required)**

        `object` — 推定流入数の前年同月比（生値ベース）。パーセントではなく比率（0.1 = +10%、1.0 = +100%）。算出不能時は null。

      - **`keywordCount` (required)**

        `number` — 獲得しているキーワード数（生値）

      - **`pageCount` (required)**

        `number` — インデックスされているページ数（生値）

      - **`pagesWithTrafficCount` (required)**

        `number` — 検索流入があるページ数

      - **`pagesWithTrafficRate` (required)**

        `number` — 検索流入があるページの比率。パーセントではなく比率（0.8235 = 82.35%）。

      - **`trafficValue` (required)**

        `number` — 集客価値の合計（USD・生値）。推定流入数×CPC で算出される広告換算価値。

    - **`site` (required)**

      `object` — 調査対象サイト（urlMatchType で整形した検索パターン）

      - **`target` (required)**

        `string` — urlMatchType で整形した検索対象パターン（url: host/path / forward\_url: host/path\* / domain: host/\* / sub\_domain: \*.host/\*）

  - **`query` (required)**

    `object` — リクエストで指定されたクエリ情報

    - **`targets` (required)**

      `array` — urlMatchType で整形した検索対象パターン一覧（items と同数・同順）

      **Items:**

      `string`

    - **`urlMatchType` (required)**

      `string`, possible values: `"url", "forward_url", "domain", "sub_domain"` — リクエストで指定された（または既定の）URLマッチタイプ

  - **`summary` (required)**

    `object` — 件数サマリー（全体件数とレスポンスに含まれる件数。入力URLと1:1）

    - **`returnedCount` (required)**

      `number` — このレスポンスに含まれている件数

    - **`totalCount` (required)**

      `number` — 取得対象全体の件数

* **`errors` (required)**

  `array` — エラーメッセージの配列。正常時は空配列。

  **Items:**

  `string`

* **`meta` (required)**

  `object` — リクエストに関するメタ情報（課金・消費リソースなど）

  - **`consumedCredit` (required)**

    `number` — このリクエストで消費されたクレジット数。

* **`result` (required)**

  `boolean` — API 呼び出しの成否。正常時は true、エラー時は false。

**Example:**

```json
{
  "result": true,
  "meta": {
    "consumedCredit": 4.5
  },
  "data": {
    "query": {
      "targets": [
        "*.rakkokeyword.com/*"
      ],
      "urlMatchType": "domain"
    },
    "summary": {
      "totalCount": 150,
      "returnedCount": 100
    },
    "items": [
      {
        "site": {
          "target": "*.rakkokeyword.com/*"
        },
        "metrics": {
          "estimatedTraffic": 15803,
          "estimatedTrafficChangeRate": 0.125,
          "keywordCount": 119,
          "pageCount": 51,
          "trafficValue": 51386,
          "pagesWithTrafficCount": 42,
          "pagesWithTrafficRate": 0.8235,
          "averageEstimatedTrafficPerPage": 309.86,
          "averageRankingKeywordCountPerPage": 2.33,
          "averageTrafficValuePerPage": 1007.57
        },
        "histories": [
          {
            "date": "2026-06-30",
            "etvIndex": 100,
            "keywordCountIndex": 82.35,
            "pageCountIndex": 90.12
          }
        ],
        "distributions": {
          "rankingPosition": {
            "1-3": 1,
            "4-10": 1,
            "11-20": 1,
            "21-50": 1,
            "51-100": 1,
            "1-10": 1,
            "1-20": 1,
            "1-30": 1
          },
          "pageTraffic": {
            "0": 1,
            "10001+": 1,
            "1001-10000": 1,
            "101-1000": 1,
            "1-100": 1,
            "1000+": 1,
            "100+": 1,
            "1+": 1
          }
        }
      }
    ]
  },
  "errors": []
}
```

### ContentSearchDto

- **Type:**`object`

* **`keyword` (required)**

  `string` — 集客コンテンツ検索の検索キーワード。指定キーワードに関連する上位表示コンテンツを検索する。1文字以上の文字列を指定する。

* **`filter`**

  `object` — 結果のフィルタリング条件。推定流入数・ランクインキーワード数・集客価値・タイトル・URL・トップキーワード・ディスクリプション・SEO難易度で絞り込む。

  - **`description`**

    `object` — ディスクリプションフィルタ（含む/含まないキーワード指定）

    - **`includes`**

      `array` — 含む単語のリスト（複数入力時はOR）

      **Items:**

      `string`

    - **`notIncludes`**

      `array` — 含まない単語のリスト（複数入力時はOR）

      **Items:**

      `string`

  - **`estimatedTraffic`**

    `object` — 推定流入数フィルタ（範囲指定）

    - **`max`**

      `integer` — 最大値

    - **`min`**

      `integer` — 最小値

  - **`rankingKeywordCount`**

    `object` — ランクインキーワード数フィルタ（0〜の範囲指定）

    - **`max`**

      `integer` — 最大値

    - **`min`**

      `integer` — 最小値

  - **`seoDifficulty`**

    `object` — SEO難易度フィルタ（0〜100の範囲指定）

    - **`max`**

      `integer` — 最大値

    - **`min`**

      `integer` — 最小値

  - **`title`**

    `object` — タイトルフィルタ（含む/含まないキーワード指定）

    - **`includes`**

      `array` — 含む単語のリスト（複数入力時はOR）

      **Items:**

      `string`

    - **`notIncludes`**

      `array` — 含まない単語のリスト（複数入力時はOR）

      **Items:**

      `string`

  - **`topKeyword`**

    `object` — トップキーワードフィルタ（含む/含まないキーワード指定）

    - **`includes`**

      `array` — 含む単語のリスト（複数入力時はOR）

      **Items:**

      `string`

    - **`notIncludes`**

      `array` — 含まない単語のリスト（複数入力時はOR）

      **Items:**

      `string`

  - **`trafficValue`**

    `object` — 集客価値（USD）フィルタ（範囲指定）

    - **`max`**

      `integer` — 最大値

    - **`min`**

      `integer` — 最小値

  - **`url`**

    `object` — URLフィルタ（含む/含まないURL指定）

    - **`includes`**

      `array` — 含むURLのリスト（複数入力時はOR）

      **Items:**

      `string`

    - **`notIncludes`**

      `array` — 含まないURLのリスト（複数入力時はOR）

      **Items:**

      `string`

* **`isAdvancedSearch`**

  `boolean`, default: `true` — 拡張検索の有効/無効。true にするとキーワードを形態素解析して検索精度を高める。省略時は true。

* **`limit`**

  `integer`, default: `100` — 取得件数。1〜5000 の整数を指定する。省略時は 100。

* **`orderBy`**

  `string`, possible values: `"asc", "desc"`, default: `"desc"` — ソート順。asc: 昇順 / desc: 降順。省略時は desc。

* **`searchTarget`**

  `string`, possible values: `"title", "keyword", "description", "titleAndKeyword", "titleAndKeywordAndDescription"`, default: `"titleAndKeywordAndDescription"` — 検索対象。title / keyword / description / titleAndKeyword / titleAndKeywordAndDescription。省略時は titleAndKeywordAndDescription。

* **`sortBy`**

  `string`, possible values: `"estimatedTraffic", "trafficValue", "rankingKeywordCount"`, default: `"trafficValue"` — 結果のソート項目。estimatedTraffic / trafficValue / rankingKeywordCount。省略時は trafficValue。

* **`topKeywordCollapse`**

  `boolean`, default: `false` — トップキーワード除去の有効/無効。true にすると同一トップキーワードの重複を除去する。省略時は false。

**Example:**

```json
{
  "keyword": "ラッコ",
  "searchTarget": "titleAndKeywordAndDescription",
  "isAdvancedSearch": true,
  "topKeywordCollapse": false,
  "filter": {
    "estimatedTraffic": {
      "min": 100,
      "max": 10000
    },
    "rankingKeywordCount": {
      "min": 1,
      "max": 100
    },
    "trafficValue": {
      "min": 100,
      "max": 10000
    },
    "title": {
      "includes": [
        "水族館"
      ],
      "notIncludes": [
        "グッズ"
      ]
    },
    "url": {
      "includes": [
        "https://rakkokeyword.com/"
      ],
      "notIncludes": [
        "https://rakkokeyword.com/result/"
      ]
    },
    "topKeyword": {
      "includes": [
        "水族館"
      ],
      "notIncludes": [
        "グッズ"
      ]
    },
    "description": {
      "includes": [
        "水族館"
      ],
      "notIncludes": [
        "グッズ"
      ]
    },
    "seoDifficulty": {
      "min": 1,
      "max": 100
    }
  },
  "sortBy": "trafficValue",
  "orderBy": "desc",
  "limit": 100
}
```

### ContentSearchResponseDto

- **Type:**`object`

* **`data` (required)**

  `object` — 集客コンテンツ検索結果データ

  - **`items` (required)**

    `array` — 集客コンテンツ検索結果のリスト。各アイテムにページ情報・指標・トップキーワードを含む。

    **Items:**

    - **`metrics` (required)**

      `object` — ページの各種指標（推定流入数・集客価値・ランクインキーワード数）

      - **`estimatedTraffic` (required)**

        `number` — このページの推定検索流入数（月間）

      - **`rankingKeywordCount` (required)**

        `number` — このページでランクインしているキーワード数

      - **`trafficValue` (required)**

        `number` — このページの集客価値（USD）。推定流入数×CPC で算出される広告換算価値。

    - **`page` (required)**

      `object` — ページ情報（ドメイン・URL・タイトル・ディスクリプション）

      - **`description` (required)**

        `string` — ページの説明文

      - **`domain` (required)**

        `string` — ページのドメイン名

      - **`title` (required)**

        `string` — ページのタイトル

      - **`url` (required)**

        `string` — ページの完全なURL

    - **`topKeyword` (required)**

      `object` — トップキーワード情報（キーワード・単語数・順位・指標）

      - **`keyword` (required)**

        `string` — このページで最もSEO流入を獲得しているトップキーワード

      - **`metrics` (required)**

        `object` — トップキーワードの各種指標（SEO難易度・月間検索数）

        - **`searchVolume` (required)**

          `number` — トップキーワードの月間検索数（年平均）

        - **`seoDifficulty` (required)**

          `object` — SEO難易度。1–100で表し、高いほど難易度が高い（1–33:低 / 34–66:中 / 67–100:高）。不明な場合は null。

      - **`position` (required)**

        `number` — トップキーワードでの検索順位

      - **`wordCount` (required)**

        `number` — トップキーワードを構成する単語数（スペース区切り）

  - **`query` (required)**

    `object` — リクエストで指定された検索クエリ情報

    - **`keyword` (required)**

      `string` — 集客コンテンツ検索の元になった検索キーワード

  - **`summary` (required)**

    `object` — 件数サマリー（全体件数とレスポンスに含まれる件数）

    - **`returnedCount` (required)**

      `number` — このレスポンスに含まれている件数

    - **`totalCount` (required)**

      `number` — 取得対象全体の件数

* **`errors` (required)**

  `array` — エラーメッセージの配列。正常時は空配列。

  **Items:**

  `string`

* **`meta` (required)**

  `object` — リクエストに関するメタ情報（課金・消費リソースなど）

  - **`consumedCredit` (required)**

    `number` — このリクエストで消費されたクレジット数。

* **`result` (required)**

  `boolean` — API 呼び出しの成否。正常時は true、エラー時は false。

**Example:**

```json
{
  "result": true,
  "meta": {
    "consumedCredit": 4.5
  },
  "data": {
    "query": {
      "keyword": "ラッコ"
    },
    "summary": {
      "totalCount": 150,
      "returnedCount": 100
    },
    "items": [
      {
        "page": {
          "domain": "rakkokeyword.com",
          "url": "https://rakkokeyword.com/result/contentSearch?q=%E3%83%A9%E3%83%83%E3%82%B3",
          "title": "ラッコキーワード",
          "description": "多機能でサクサク使えるキーワードリサーチツール。生成AIによる記事生成機能搭載。SEO/市場ニーズ調査/競合分析/コンテンツ制作/商品開発にお役立ていただけます。無料でも使えます！"
        },
        "metrics": {
          "estimatedTraffic": 14000,
          "trafficValue": 2266,
          "rankingKeywordCount": 18
        },
        "topKeyword": {
          "keyword": "ラッコ",
          "wordCount": 1,
          "position": 2,
          "metrics": {
            "seoDifficulty": 37,
            "searchVolume": 5000
          }
        }
      }
    ]
  },
  "errors": []
}
```

### HeadlineDto

- **Type:**`object`

* **`keyword` (required)**

  `string` — 見出し抽出を行う検索キーワード。1文字以上の文字列を指定する。

* **`h1`**

  `boolean`, default: `true` — h1タグの見出しを含めるかどうか。省略時は true。

* **`h2`**

  `boolean`, default: `true` — h2タグの見出しを含めるかどうか。省略時は true。

* **`h3`**

  `boolean`, default: `true` — h3タグの見出しを含めるかどうか。省略時は true。

* **`h4`**

  `boolean`, default: `true` — h4タグの見出しを含めるかどうか。省略時は true。

* **`h5`**

  `boolean`, default: `false` — h5タグの見出しを含めるかどうか。省略時は false。

* **`h6`**

  `boolean`, default: `false` — h6タグの見出しを含めるかどうか。省略時は false。

* **`lessCharacters`**

  `boolean`, default: `false` — 文字数1,000未満のページを除外するかどうか。true で除外する。省略時は false。

* **`lessHeadlines`**

  `boolean`, default: `false` — 見出し5件未満のページを除外するかどうか。true で除外する。省略時は false。

* **`limit`**

  `integer`, default: `20` — 取得件数。1〜20 の整数を指定する。省略時は 20。

* **`orderBy`**

  `string`, possible values: `"asc", "desc"`, default: `"asc"` — ソート順。asc: 昇順 / desc: 降順。省略時は asc。

* **`sortBy`**

  `string`, possible values: `"position", "title", "headlineCount", "wordCount"`, default: `"position"` — ソート項目。position / title / headlineCount / wordCount。省略時は position。

**Example:**

```json
{
  "keyword": "ラッコ",
  "lessHeadlines": false,
  "lessCharacters": false,
  "h1": true,
  "h2": true,
  "h3": true,
  "h4": true,
  "h5": false,
  "h6": false,
  "sortBy": "position",
  "orderBy": "asc",
  "limit": 20
}
```

### HeadlineResponseDto

- **Type:**`object`

* **`data` (required)**

  `object` — 見出し抽出の検索結果データ

  - **`items` (required)**

    `array` — 見出し抽出アイテムのリスト。各アイテムにページ情報・指標・見出し一覧を含む。

    **Items:**

    - **`headlines` (required)**

      `array` — ページ内の見出し一覧。指定した見出しレベル（h1–h6）に応じてフィルタされる。

      **Items:**

      - **`level` (required)**

        `string` — 見出しレベル（h1, h2, h3, h4 など）

      - **`text` (required)**

        `string` — 見出しテキスト

    - **`metrics` (required)**

      `object` — ページの各種指標（検索順位・見出し数・文字数）

      - **`headlineCount` (required)**

        `number` — このページに含まれる見出し数

      - **`position` (required)**

        `number` — 検索順位

      - **`wordCount` (required)**

        `number` — このページの文字数

    - **`page` (required)**

      `object` — 検索結果ページの基本情報（URL・タイトル・ディスクリプション）

      - **`description` (required)**

        `string` — 検索結果ページのディスクリプション

      - **`title` (required)**

        `string` — 検索結果ページのタイトル

      - **`url` (required)**

        `string` — 検索結果ページの URL

  - **`query` (required)**

    `object` — リクエストで指定された検索クエリ情報

    - **`keyword` (required)**

      `string` — 見出し抽出の元になった検索キーワード

  - **`summary` (required)**

    `object` — 件数・文字数・見出し数のサマリー情報

    - **`averageHeadlineCount` (required)**

      `number` — 1ページあたりの平均見出し数

    - **`averageWordCount` (required)**

      `number` — 1ページあたりの平均文字数

    - **`maxWordCount` (required)**

      `number` — ページ文字数の最大値

    - **`minWordCount` (required)**

      `number` — ページ文字数の最小値

    - **`returnedCount` (required)**

      `number` — このレスポンスに含まれている件数

    - **`totalCount` (required)**

      `number` — 取得対象全体の件数

* **`errors` (required)**

  `array` — エラーメッセージの配列。正常時は空配列。

  **Items:**

  `string`

* **`meta` (required)**

  `object` — リクエストに関するメタ情報（課金・消費リソースなど）

  - **`consumedCredit` (required)**

    `number` — このリクエストで消費されたクレジット数。

* **`result` (required)**

  `boolean` — API 呼び出しの成否。正常時は true、エラー時は false。

**Example:**

```json
{
  "result": true,
  "meta": {
    "consumedCredit": 3
  },
  "data": {
    "query": {
      "keyword": "ラッコ"
    },
    "summary": {
      "totalCount": 150,
      "returnedCount": 100,
      "averageHeadlineCount": 19.5,
      "averageWordCount": 7782,
      "minWordCount": 2935,
      "maxWordCount": 12629
    },
    "items": [
      {
        "page": {
          "url": "https://ja.wikipedia.org/wiki/%E3%83%A9%E3%83%83%E3%82%B3",
          "title": "ラッコ - Wikipedia",
          "description": "ラッコは、..."
        },
        "metrics": {
          "position": 1,
          "headlineCount": 19,
          "wordCount": 14190
        },
        "headlines": [
          {
            "level": "h1",
            "text": "ラッコ"
          }
        ]
      }
    ]
  },
  "errors": []
}
```

### CoOccurrenceDto

- **Type:**`object`

* **`keyword` (required)**

  `string` — 共起語取得の元となる検索キーワード。1文字以上の文字列を指定する。

* **`getDetails`**

  `boolean`, default: `true` — URLごとの詳細情報を取得するかどうか。true にすると各共起語について検索上位ページごとの出現情報を返す。省略時は true。

* **`limit`**

  `integer` — 取得件数の上限。正の整数を指定。省略時はすべての結果を返す。

* **`orderBy`**

  `string`, possible values: `"asc", "desc"`, default: `"desc"` — ソート順。asc: 昇順 / desc: 降順。省略時は desc。

* **`sortBy`**

  `string`, possible values: `"word", "occurrencePageCount", "occurrenceTitleCount", "occurrenceHeadingCount", "siteCountTotal", "siteCountHeading"`, default: `"siteCountTotal"` — ソート項目。word / occurrencePageCount / occurrenceTitleCount / occurrenceHeadingCount / siteCountTotal / siteCountHeading。省略時は siteCountTotal。

**Example:**

```json
{
  "keyword": "ラッコ",
  "getDetails": true,
  "sortBy": "siteCountTotal",
  "orderBy": "desc",
  "limit": 10
}
```

### CoOccurrenceResponseDto

- **Type:**`object`

* **`data` (required)**

  `object` — 共起語検索結果データ

  - **`items` (required)**

    `array` — 共起語アイテムのリスト。各アイテムに共起語・指標・詳細情報を含む。

    **Items:**

    - **`metrics` (required)**

      `object` — 共起語の各種指標（本文・タイトル・見出しの出現回数、出現サイト数）

      - **`occurrenceHeadingCount` (required)**

        `number` — 検索上位ページの見出し内でこの共起語が出現した回数

      - **`occurrencePageCount` (required)**

        `number` — 検索上位ページ内でこの共起語が出現した回数

      - **`occurrenceTitleCount` (required)**

        `number` — 検索上位ページのタイトル内でこの共起語が出現した回数

      - **`siteCountHeading` (required)**

        `number` — 検索上位サイトのうち、この共起語が見出し内に出現したサイト数

      - **`siteCountTotal` (required)**

        `number` — 検索上位サイトのうち、この共起語が本文内で出現したサイト数

    - **`word` (required)**

      `string` — 検索上位ページから抽出した共起語

    - **`pageDetails`**

      `array` — URLごとの詳細情報（getDetails=true の場合のみ）

      **Items:**

      - **`count` (required)**

        `number` — 共起語の本文内出現回数

      - **`countInHeadline` (required)**

        `number` — 共起語の見出し内出現回数

      - **`countInTitle` (required)**

        `number` — 共起語のタイトル内出現回数

      - **`pageCount` (required)**

        `number` — 共起語が出現したページ数

      - **`pageCountInHeadline` (required)**

        `number` — 見出しに共起語が出現したページ数

      - **`rank` (required)**

        `number` — 検索結果における順位

      - **`title` (required)**

        `string` — ページタイトル

      - **`url` (required)**

        `string` — ページURL

  - **`query` (required)**

    `object` — リクエストで指定された検索クエリ情報

    - **`keyword` (required)**

      `string` — 共起語取得の元になった検索キーワード

  - **`summary` (required)**

    `object` — 件数サマリー（全体件数とレスポンスに含まれる件数）

    - **`returnedCount` (required)**

      `number` — このレスポンスに含まれている件数

    - **`totalCount` (required)**

      `number` — 取得対象全体の件数

* **`errors` (required)**

  `array` — エラーメッセージの配列。正常時は空配列。

  **Items:**

  `string`

* **`meta` (required)**

  `object` — リクエストに関するメタ情報（課金・消費リソースなど）

  - **`consumedCredit` (required)**

    `number` — このリクエストで消費されたクレジット数。

* **`result` (required)**

  `boolean` — API 呼び出しの成否。正常時は true、エラー時は false。

**Example:**

```json
{
  "result": true,
  "meta": {
    "consumedCredit": 3
  },
  "data": {
    "query": {
      "keyword": "ラッコ"
    },
    "summary": {
      "totalCount": 150,
      "returnedCount": 100
    },
    "items": [
      {
        "word": "水族館",
        "metrics": {
          "occurrencePageCount": 230,
          "occurrenceTitleCount": 8,
          "occurrenceHeadingCount": 21,
          "siteCountTotal": 13,
          "siteCountHeading": 7
        },
        "pageDetails": [
          {
            "rank": 1,
            "title": "ラッコ",
            "url": "https://ja.wikipedia.org/wiki/%E3%83%A9%E3%83%83%E3%82%B3",
            "count": 3,
            "countInHeadline": 0,
            "countInTitle": 0,
            "pageCount": 1,
            "pageCountInHeadline": 0
          }
        ]
      }
    ]
  },
  "errors": []
}
```

### SearchRankHistoryDto

- **Type:**`object`

* **`keywords` (required)**

  `array` — 順位チェックするキーワードの配列

  **Items:**

  `string`

* **`urls` (required)**

  `array` — 順位チェックするURL/ドメインの配列。最大50件まで指定可能。

  **Items:**

  `string`

* **`deduplicate`**

  `boolean`, default: `true` — キーワードの重複除去を行うかどうか。省略時は true。

* **`depth`**

  `number`, possible values: `30, 40, 50, 60, 70, 80, 90, 100`, default: `30` — 検索上位何位までデータ取得するかを指定する。30 / 40 / 50 / 60 / 70 / 80 / 90 / 100 のいずれかを指定。省略時は 30。

* **`device`**

  `string`, possible values: `"desktop", "mobile"`, default: `"desktop"` — SERP取得対象のデバイス。desktop / mobile のいずれか。省略時は desktop。

* **`isSearchVolumeAndSeoDifficultyEnabled`**

  `boolean`, default: `false` — 月間検索数/SEO難易度を取得するかどうか。省略時は false。

* **`language`**

  `string`, default: `"Japanese"` — SERP取得対象の言語名。指定可能な言語名は metadata の languages 一覧を参照。省略時は Japanese。

* **`location`**

  `string`, default: `"Japan"` — SERP取得対象の地域名。省略時は Japan。 - 指定可能な地域名は metadata の locations 一覧を参照（一覧は国レベルのみ） - 市区町村レベルの地域も指定可能。「市区町村名,上位地域名,国名」のようにカンマ区切りの正式名で指定する（例: Shibuya,Tokyo,Japan） - 途中の階層のみ（例: 都道府県のみ）の指定は未サポート

* **`matchType`**

  `string`, possible values: `"url", "forward_url", "domain", "sub_domain"`, default: `"sub_domain"` — マッチタイプ。url: 完全一致URL / forward\_url: 前方一致URL / domain: ドメイン完全一致 / sub\_domain: サブドメイン含むドメイン一致。省略時は sub\_domain。

* **`os`**

  `string`, possible values: `"windows", "macos", "android", "ios"` — SERP取得対象のOS。デスクトップは windows / macos、モバイルは android / ios を指定。省略時は desktop→windows / mobile→android。

**Example:**

```json
{
  "keywords": [
    "ラッコ",
    "カワウソ"
  ],
  "urls": [
    "https://rakkokeyword.com",
    "https://rakkokeyword.com/result/contentSearch?q=%E3%83%A9%E3%83%83%E3%82%B3"
  ],
  "matchType": "sub_domain",
  "depth": 30,
  "isSearchVolumeAndSeoDifficultyEnabled": false,
  "deduplicate": true,
  "location": "Japan",
  "language": "Japanese",
  "device": "desktop",
  "os": "windows"
}
```

### SearchRankHistoryResponseDto

- **Type:**`object`

* **`data` (required)**

  `object` — 履歴登録結果

  - **`requestId`**

    `string` — リクエストID

* **`errors` (required)**

  `array` — エラーメッセージの配列。正常時は空配列。

  **Items:**

  `string`

* **`meta` (required)**

  `object` — リクエストに関するメタ情報（課金・消費リソースなど）

  - **`consumedCredit` (required)**

    `number` — このリクエストで消費されたクレジット数。

* **`result` (required)**

  `boolean` — API 呼び出しの成否。正常時は true、エラー時は false。

**Example:**

```json
{
  "result": true,
  "meta": {
    "consumedCredit": 1.2
  },
  "data": {
    "requestId": "01HQZX5Y4JMQK8XNQ7WVZXZ5Y4"
  },
  "errors": []
}
```

### SearchRankHistoryOverallStatus

- **Type:**`string`

**Example:**

### SearchRankHistoriesResponseDto

- **Type:**`object`

* **`data` (required)**

  `object` — 検索順位チェック履歴一覧データ

  - **`items` (required)**

    `array` — 検索順位チェック履歴アイテムのリスト

    **Items:**

    - **`completedAt` (required)**

      `object` — 全処理完了日時（ISO 8601、UTC）。未完了時は null。

    - **`createdAt` (required)**

      `string`, format: `date-time` — リクエスト作成日時（ISO 8601、UTC）

    - **`depth` (required)**

      `object` — 検索結果の取得深度。30 / 40 / 50 / 60 / 70 / 80 / 90 / 100 のいずれか。取得深度が記録されていない古い履歴では null を返す。

    - **`isSearchVolumeAndSeoDifficultyEnabled` (required)**

      `boolean` — 月間検索数/SEO難易度の取得が有効かどうか

    - **`keywordCount` (required)**

      `number` — キーワードの件数

    - **`keywordSummary` (required)**

      `string` — キーワードのサマリ（カンマ区切り、先頭20件・255文字以内で切り詰め）

    - **`matchType` (required)**

      `string`, possible values: `"url", "forward_url", "domain", "sub_domain"` — マッチタイプ。url: 完全一致URL / forward\_url: 前方一致URL / domain: ドメイン完全一致 / sub\_domain: サブドメイン含むドメイン一致。

    - **`requestId` (required)**

      `string` — リクエストID

    - **`status` (required)**

      `string`, possible values: `"completed", "processing"` — 全体ステータス。statuses の両方が processed の場合に completed（月間検索数/SEO難易度取得 OFF の場合は serp のみで判定）。

    - **`statuses` (required)**

      `object` — 各処理のステータス情報

      - **`serp` (required)**

        `string`, possible values: `"unprocessed", "processing", "processed"` — SERP取得ステータス。unprocessed: 未処理 / processing: 処理中 / processed: 完了。

      - **`searchVolumeAndSeoDifficulty`**

        `string`, possible values: `"unprocessed", "processing", "processed", "failed", "integration_failed"` — 月間検索数/SEO難易度ステータス。月間検索数/SEO難易度取得 OFF のリクエストでは欠落する。unprocessed: 未処理 / processing: 処理中 / processed: 完了 / failed: 失敗 / integration\_failed: 統合失敗。

    - **`urlCount` (required)**

      `number` — URLの件数

    - **`urlSummary` (required)**

      `string` — URLのサマリ（カンマ区切り、先頭20件・255文字以内で切り詰め）

  - **`query` (required)**

    `object` — リクエストで指定されたクエリパラメータ

    - **`limit` (required)**

      `number` — リクエストで指定された取得件数

    - **`offset` (required)**

      `number` — リクエストで指定された取得開始位置

    - **`status` (required)**

      `object` — リクエストで指定されたステータスフィルタ

  - **`summary` (required)**

    `object` — 件数サマリ

    - **`returnedCount` (required)**

      `number` — このレスポンスに含まれている件数

    - **`totalCount` (required)**

      `number` — 取得対象全体の件数

* **`errors` (required)**

  `array` — エラーメッセージの配列。正常時は空配列。

  **Items:**

  `string`

* **`meta` (required)**

  `object` — リクエストに関するメタ情報（課金・消費リソースなど）

  - **`consumedCredit` (required)**

    `number` — このリクエストで消費されたクレジット数。

* **`result` (required)**

  `boolean` — API 呼び出しの成否。正常時は true、エラー時は false。

**Example:**

```json
{
  "result": true,
  "meta": {
    "consumedCredit": 0
  },
  "data": {
    "query": {
      "limit": 100,
      "offset": 0,
      "status": null
    },
    "summary": {
      "totalCount": 1,
      "returnedCount": 1
    },
    "items": [
      {
        "requestId": "01HQZX5Y4JMQK8XNQ7WVZXZ5Y4",
        "createdAt": "2026-05-31T01:00:00.000Z",
        "completedAt": null,
        "status": "processing",
        "statuses": {
          "serp": "processed",
          "searchVolumeAndSeoDifficulty": "processing"
        },
        "keywordSummary": "ラッコ,カワウソ",
        "urlSummary": "https://rakkokeyword.com,https://rakko.inc",
        "keywordCount": 2,
        "urlCount": 2,
        "matchType": "sub_domain",
        "depth": 30,
        "isSearchVolumeAndSeoDifficultyEnabled": true
      }
    ]
  },
  "errors": []
}
```

### SearchRankStatusResponseDto

- **Type:**`object`

* **`data` (required)**

  `object` — ステータス情報

  - **`isCompleted`**

    `boolean` — 全処理完了フラグ。statuses.serp が processed かつ statuses.searchVolumeAndSeoDifficulty が processed またはなし の場合に true。failed または integration\_failed の場合は false。

  - **`statuses`**

    `object` — 各処理のステータス情報

    - **`searchVolumeAndSeoDifficulty`**

      `string`, possible values: `"unprocessed", "processing", "processed", "failed", "integration_failed"` — 月間検索数/SEO難易度ステータス。unprocessed: 未処理 / processing: 処理中 / processed: 完了 / failed: 失敗 / integration\_failed: 統合失敗。

    - **`serp`**

      `string`, possible values: `"unprocessed", "processing", "processed"` — SERP取得ステータス。unprocessed: 未処理 / processing: 処理中 / processed: 完了。

* **`errors` (required)**

  `array` — エラーメッセージの配列。正常時は空配列。

  **Items:**

  `string`

* **`meta` (required)**

  `object` — リクエストに関するメタ情報（課金・消費リソースなど）

  - **`consumedCredit` (required)**

    `number` — このリクエストで消費されたクレジット数。

* **`result` (required)**

  `boolean` — API 呼び出しの成否。正常時は true、エラー時は false。

**Example:**

```json
{
  "result": true,
  "meta": {
    "consumedCredit": 0
  },
  "data": {
    "isCompleted": true,
    "statuses": {
      "serp": "processed",
      "searchVolumeAndSeoDifficulty": "processing"
    }
  },
  "errors": []
}
```

### SearchRankResultsDto

- **Type:**`object`

* **`filter`**

  `object` — 結果のフィルタリング条件。キーワード・SEO難易度・月間検索数で絞り込む。

  - **`keyword`**

    `object` — キーワードフィルタ（含む/含まないキーワード指定）

    - **`includes`**

      `array` — 含む単語のリスト（複数入力時はOR）

      **Items:**

      `string`

    - **`notIncludes`**

      `array` — 含まない単語のリスト（複数入力時はOR）

      **Items:**

      `string`

  - **`searchVolume`**

    `object` — 月間検索数フィルタ（範囲指定）

    - **`max`**

      `integer` — 最大値

    - **`min`**

      `integer` — 最小値

  - **`seoDifficulty`**

    `object` — SEO難易度フィルタ（0〜100の範囲指定）

    - **`max`**

      `integer` — 最大値

    - **`min`**

      `integer` — 最小値

* **`limit`**

  `integer`, default: `100` — 取得件数。1以上の整数を指定する。省略時は 100。

* **`orderBy`**

  `string`, possible values: `"asc", "desc"`, default: `"desc"` — ソート順。asc: 昇順 / desc: 降順。省略時は desc。

* **`sortBy`**

  `string`, possible values: `"keyword", "seoDifficulty", "searchVolume"`, default: `"searchVolume"` — ソート項目。keyword / seoDifficulty / searchVolume。省略時は searchVolume。

* **`withAggregation`**

  `boolean`, default: `false` — ターゲットごとの集計情報（推定流入数）を出力するかどうか。省略時は false。

**Example:**

```json
{
  "filter": {
    "keyword": {
      "includes": [
        "水族館"
      ],
      "notIncludes": [
        "グッズ"
      ]
    },
    "seoDifficulty": {
      "min": 1,
      "max": 100
    },
    "searchVolume": {
      "min": 100,
      "max": 10000
    }
  },
  "sortBy": "searchVolume",
  "orderBy": "desc",
  "limit": 100,
  "withAggregation": false
}
```

### SearchRankResultsResponseDto

- **Type:**`object`

* **`data` (required)**

  `object` — 検索順位チェック結果データ

  - **`items` (required)**

    `array` — 検索順位チェック結果アイテムのリスト

    **Items:**

    - **`entryNo` (required)**

      `number` — リクエスト内でのキーワードの登録順

    - **`keyword` (required)**

      `string` — 検索順位を確認したキーワード

    - **`metrics` (required)**

      `object` — 各種指標（SEO難易度・月間検索数・CPC・広告競合性）

      - **`competition` (required)**

        `object` — 広告競合性。0–100で表し、高いほど競合性が高い（0–33:低 / 34–66:中 / 67–100:高）。無効な場合は null。

      - **`cpc` (required)**

        `object` — 推定クリック単価（USD）。無効な場合は null。

      - **`searchVolume` (required)**

        `object` — 月間検索数（年平均）。無効な場合は null。

      - **`seoDifficulty` (required)**

        `object` — SEO難易度。1–100で表し、高いほど難易度が高い（1–33:低 / 34–66:中 / 67–100:高）。不明な場合は null。

    - **`rankings` (required)**

      `array` — ターゲットごとの検索順位情報

      **Items:**

      - **`estimatedTraffic` (required)**

        `number` — このキーワードでの推定検索流入数（月間）

      - **`position` (required)**

        `object` — 検索順位。圏外または未検出の場合は null。

      - **`rankedUrl` (required)**

        `object` — 実際にランクインしたURL。未検出の場合は null。

      - **`target` (required)**

        `string` — 順位チェック対象のURLパターンまたはドメイン

  - **`query` (required)**

    `object` — 検索クエリ情報

    - **`limit` (required)**

      `integer` — リクエストで指定された取得件数

    - **`orderBy` (required)**

      `string`, possible values: `"asc", "desc"` — リクエストで指定されたソート順。asc: 昇順 / desc: 降順。

    - **`requestId` (required)**

      `string` — 検索順位チェック結果を識別するリクエストID

    - **`sortBy` (required)**

      `string`, possible values: `"keyword", "seoDifficulty", "searchVolume"` — リクエストで指定されたソート項目。keyword / seoDifficulty / searchVolume。

    - **`withAggregation` (required)**

      `boolean` — ターゲットごとの集計情報（推定流入数）を出力するかどうか

    - **`filter`**

      `object` — リクエストで指定された絞り込み条件（キーワード・SEO難易度・月間検索数）。指定がない場合は省略される。

      - **`keyword`**

        `object` — キーワードフィルタ（含む/含まないキーワード指定）

        - **`includes`**

          `array` — 含む単語のリスト（複数入力時はOR）

          **Items:**

          `string`

        - **`notIncludes`**

          `array` — 含まない単語のリスト（複数入力時はOR）

          **Items:**

          `string`

      - **`searchVolume`**

        `object` — 月間検索数フィルタ（範囲指定）

        - **`max`**

          `integer` — 最大値

        - **`min`**

          `integer` — 最小値

      - **`seoDifficulty`**

        `object` — SEO難易度フィルタ（0〜100の範囲指定）

        - **`max`**

          `integer` — 最大値

        - **`min`**

          `integer` — 最小値

  - **`summary` (required)**

    `object` — 件数サマリー

    - **`returnedCount` (required)**

      `number` — このレスポンスに含まれている件数

    - **`targets` (required)**

      `array` — ターゲットごとの検索順位分布と推定流入数（フィルター条件にマッチした全件の集計）

      **Items:**

      - **`estimatedTraffic` (required)**

        `number` — 推定検索流入数の合計（withAggregation=false の場合は0）

      - **`rankingPositionDistribution` (required)**

        `object` — フィルター条件にマッチした全件の順位分布

        - **`1-3` (required)**

          `number` — 順位1〜3位のキーワード数

        - **`101+` (required)**

          `number` — 順位101位以降のキーワード数

        - **`11-20` (required)**

          `number` — 順位11〜20位のキーワード数

        - **`21-30` (required)**

          `number` — 順位21〜30位のキーワード数

        - **`31-50` (required)**

          `number` — 順位31〜50位のキーワード数

        - **`4-10` (required)**

          `number` — 順位4〜10位のキーワード数

        - **`51-100` (required)**

          `number` — 順位51〜100位のキーワード数

      - **`target` (required)**

        `string` — 順位チェック対象のURLパターンまたはドメイン

    - **`totalCount` (required)**

      `number` — 取得対象全体の件数

* **`errors` (required)**

  `array` — エラーメッセージの配列。正常時は空配列。

  **Items:**

  `string`

* **`meta` (required)**

  `object` — リクエストに関するメタ情報（課金・消費リソースなど）

  - **`consumedCredit` (required)**

    `number` — このリクエストで消費されたクレジット数。

* **`result` (required)**

  `boolean` — API 呼び出しの成否。正常時は true、エラー時は false。

**Example:**

```json
{
  "result": true,
  "meta": {
    "consumedCredit": 0
  },
  "data": {
    "query": {
      "requestId": "sr_20260309_001",
      "filter": {
        "keyword": {
          "includes": [
            "水族館"
          ],
          "notIncludes": [
            "グッズ"
          ]
        },
        "seoDifficulty": {
          "min": 1,
          "max": 100
        },
        "searchVolume": {
          "min": 100,
          "max": 10000
        }
      },
      "sortBy": "searchVolume",
      "orderBy": "desc",
      "limit": 100,
      "withAggregation": false
    },
    "summary": {
      "totalCount": 2,
      "returnedCount": 2,
      "targets": [
        {
          "target": "*.rakkoma.com/*",
          "estimatedTraffic": 7391,
          "rankingPositionDistribution": {
            "1-3": 40,
            "4-10": 15,
            "11-20": 5,
            "21-30": 4,
            "31-50": 5,
            "51-100": 3,
            "101+": 10
          }
        }
      ]
    },
    "items": [
      {
        "entryNo": 3,
        "keyword": "サイト売買 個人",
        "metrics": {
          "seoDifficulty": 23,
          "searchVolume": 70,
          "cpc": 3.47,
          "competition": 41
        },
        "rankings": [
          {
            "target": "*.rakkoma.com/*",
            "position": 3,
            "rankedUrl": "https://rakkoma.com/",
            "estimatedTraffic": 9
          }
        ]
      }
    ]
  },
  "errors": []
}
```

### SearchRankSerpCacheResponseDto

- **Type:**`object`

* **`data` (required)**

  `object` — 検索結果データ

  - **`items` (required)**

    `array` — 検索結果アイテムのリスト

    **Items:**

    - **`metrics` (required)**

      `object` — 付加情報の指標

      - **`estimatedTraffic` (required)**

        `object` — このページの推定検索流入数。不明な場合は null。

      - **`rankingKeywordCount` (required)**

        `object` — このページでランクインしているキーワード数。不明な場合は null。

      - **`trafficValue` (required)**

        `object` — このページの集客価値（USD）。不明な場合は null。

    - **`page` (required)**

      `object` — ページ情報

      - **`description` (required)**

        `string` — ページの説明文

      - **`title` (required)**

        `string` — ページタイトル

      - **`url` (required)**

        `string`, format: `uri` — ページURL

    - **`position` (required)**

      `number` — 検索結果の表示順位

    - **`topKeyword` (required)**

      `object` — トップキーワード情報

      - **`keyword` (required)**

        `object` — このページで最もSEO流入を獲得しているトップキーワード。不明な場合は null。

      - **`metrics` (required)**

        `object` — トップキーワードの指標

        - **`searchVolume` (required)**

          `object` — トップキーワードの月間検索数（年平均）。不明な場合は null。

        - **`seoDifficulty` (required)**

          `object` — SEO難易度。1–100で表し、高いほど難易度が高い（1–33:低 / 34–66:中 / 67–100:高）。不明な場合は null。

      - **`position` (required)**

        `object` — トップキーワードでの検索順位。不明な場合は null。

  - **`query` (required)**

    `object` — 検索クエリ情報

    - **`entryNo` (required)**

      `number` — リクエスト内でのキーワードの登録順

    - **`requestId` (required)**

      `string` — 検索順位チェック履歴を識別するID

  - **`summary` (required)**

    `object` — 件数サマリー

    - **`fetchedDate` (required)**

      `string`, format: `date` — 検索結果の取得日（YYYY-MM-DD）

    - **`keyword` (required)**

      `string` — 検索順位を確認したキーワード

    - **`returnedCount` (required)**

      `number` — このレスポンスに含まれている件数

* **`errors` (required)**

  `array` — エラーメッセージの配列。正常時は空配列。

  **Items:**

  `string`

* **`meta` (required)**

  `object` — リクエストに関するメタ情報（課金・消費リソースなど）

  - **`consumedCredit` (required)**

    `number` — このリクエストで消費されたクレジット数。

* **`result` (required)**

  `boolean` — API 呼び出しの成否。正常時は true、エラー時は false。

**Example:**

```json
{
  "result": true,
  "meta": {
    "consumedCredit": 0
  },
  "data": {
    "query": {
      "requestId": "01HQZX5Y4JMQK8XNQ7WVZXZ5Y4",
      "entryNo": 3
    },
    "summary": {
      "keyword": "サイト売買 個人",
      "returnedCount": 42,
      "fetchedDate": "2026-06-30"
    },
    "items": [
      {
        "position": 1,
        "page": {
          "url": "https://example.com/",
          "title": "サイト売買の個人間取引ガイド",
          "description": "個人でサイト売買を行う際の注意点..."
        },
        "metrics": {
          "estimatedTraffic": 120,
          "trafficValue": 45,
          "rankingKeywordCount": 8
        },
        "topKeyword": {
          "keyword": "サイト 売却 方法",
          "position": 3,
          "metrics": {
            "seoDifficulty": 42,
            "searchVolume": 500
          }
        }
      }
    ]
  },
  "errors": []
}
```

### SiteSearchDto

- **Type:**`object`

* **`filter`**

  `object`, default: `{}` — 絞り込み条件。コンテンツ・ドメイン・推定流入数・キーワード数・ページ数・価値・関連コンテンツ推定流入数・コンテンツ関連性で絞り込む。省略時は全サイトを流入が多い順に取得する。

  - **`contentRelevance`**

    `object` — コンテンツ関連性フィルタ（0〜100の範囲指定）。コンテンツフィルタ指定時のみ有効。

    - **`max`**

      `integer` — 最大値

    - **`min`**

      `integer` — 最小値

  - **`domain`**

    `object` — ドメインフィルタ（含む/含まないドメインとマッチタイプ）

    - **`includes`**

      `array` — 含むドメインのリスト

      **Items:**

      `string`

    - **`matchType`**

      `string`, possible values: `"partialMatch", "prefixMatch", "suffixMatch"`, default: `"partialMatch"` — ドメインのマッチタイプ。partialMatch: 部分一致 / prefixMatch: 前方一致 / suffixMatch: 後方一致。省略時は partialMatch。

    - **`notIncludes`**

      `array` — 含まないドメインのリスト

      **Items:**

      `string`

  - **`keyword`**

    `object` — コンテンツフィルタ（含む/含まないキーワード）。指定すると、まず関連サイトを流入が多い順に最大100件抽出した後に他フィルタが適用される。

    - **`includes` (required)**

      `array` — 含む単語のリスト（1件以上必須）

      **Items:**

      `string`

    - **`notIncludes`**

      `array` — 含まない単語のリスト

      **Items:**

      `string`

  - **`keywordCount`**

    `object` — キーワード数フィルタ（範囲指定）

    - **`max`**

      `integer` — 最大値

    - **`min`**

      `integer` — 最小値

  - **`pageCount`**

    `object` — ページ数フィルタ（範囲指定）

    - **`max`**

      `integer` — 最大値

    - **`min`**

      `integer` — 最小値

  - **`relatedContentEtv`**

    `object` — 関連コンテンツ推定流入数フィルタ（範囲指定）。コンテンツフィルタ指定時のみ有効。

    - **`max`**

      `integer` — 最大値

    - **`min`**

      `integer` — 最小値

  - **`totalEtv`**

    `object` — 推定流入数フィルタ（範囲指定）

    - **`max`**

      `integer` — 最大値

    - **`min`**

      `integer` — 最小値

  - **`totalTrafficValue`**

    `object` — 価値（USD）フィルタ（範囲指定）

    - **`max`**

      `integer` — 最大値

    - **`min`**

      `integer` — 最小値

* **`limit`**

  `integer`, default: `100` — 取得件数。1〜100 の整数を指定する。省略時は 100。

**Example:**

```json
{
  "filter": {},
  "limit": 100
}
```

### SiteSearchResponseDto

- **Type:**`object`

* **`data` (required)**

  `object` — サイト検索結果データ

  - **`items` (required)**

    `array` — サイト検索結果のリスト。流入が多い順（コンテンツフィルタ指定時は関連コンテンツ流入が多い順）。

    **Items:**

    - **`metrics` (required)**

      `object` — サイトの各種指標（推定流入数・価値・キーワード数・ページ数）

      - **`estimatedTraffic` (required)**

        `number` — サイト全体の推定検索流入数（月間）

      - **`pageCount` (required)**

        `number` — ランクインしているページ数

      - **`rankingKeywordCount` (required)**

        `number` — サイト全体でランクインしているキーワード数

      - **`trafficValue` (required)**

        `number` — サイト全体の集客価値（USD）

    - **`no` (required)**

      `number` — 結果内の連番（1始まり）

    - **`relatedContent` (required)**

      `object` — コンテンツフィルタ関連の指標。コンテンツフィルタ未指定時は null。

    - **`site` (required)**

      `object` — サイト情報（ドメイン・URL・タイトル・説明文）

      - **`description` (required)**

        `string` — トップページの説明文

      - **`domain` (required)**

        `string` — サイトのドメイン名

      - **`title` (required)**

        `string` — トップページのタイトル

      - **`url` (required)**

        `string` — サイトのトップページURL

  - **`query` (required)**

    `object` — リクエストで指定された検索条件

    - **`filter` (required)**

      `object` — リクエストで適用された絞り込み条件

      - **`contentRelevance`**

        `object` — コンテンツ関連性フィルタ（0〜100の範囲指定）。コンテンツフィルタ指定時のみ有効。

        - **`max`**

          `integer` — 最大値

        - **`min`**

          `integer` — 最小値

      - **`domain`**

        `object` — ドメインフィルタ（含む/含まないドメインとマッチタイプ）

        - **`includes`**

          `array` — 含むドメインのリスト

          **Items:**

          `string`

        - **`matchType`**

          `string`, possible values: `"partialMatch", "prefixMatch", "suffixMatch"`, default: `"partialMatch"` — ドメインのマッチタイプ。partialMatch: 部分一致 / prefixMatch: 前方一致 / suffixMatch: 後方一致。省略時は partialMatch。

        - **`notIncludes`**

          `array` — 含まないドメインのリスト

          **Items:**

          `string`

      - **`keyword`**

        `object` — コンテンツフィルタ（含む/含まないキーワード）。指定すると、まず関連サイトを流入が多い順に最大100件抽出した後に他フィルタが適用される。

        - **`includes` (required)**

          `array` — 含む単語のリスト（1件以上必須）

          **Items:**

          `string`

        - **`notIncludes`**

          `array` — 含まない単語のリスト

          **Items:**

          `string`

      - **`keywordCount`**

        `object` — キーワード数フィルタ（範囲指定）

        - **`max`**

          `integer` — 最大値

        - **`min`**

          `integer` — 最小値

      - **`pageCount`**

        `object` — ページ数フィルタ（範囲指定）

        - **`max`**

          `integer` — 最大値

        - **`min`**

          `integer` — 最小値

      - **`relatedContentEtv`**

        `object` — 関連コンテンツ推定流入数フィルタ（範囲指定）。コンテンツフィルタ指定時のみ有効。

        - **`max`**

          `integer` — 最大値

        - **`min`**

          `integer` — 最小値

      - **`totalEtv`**

        `object` — 推定流入数フィルタ（範囲指定）

        - **`max`**

          `integer` — 最大値

        - **`min`**

          `integer` — 最小値

      - **`totalTrafficValue`**

        `object` — 価値（USD）フィルタ（範囲指定）

        - **`max`**

          `integer` — 最大値

        - **`min`**

          `integer` — 最小値

  - **`summary` (required)**

    `object` — 件数サマリー（全体件数とレスポンスに含まれる件数）

    - **`returnedCount` (required)**

      `number` — このレスポンスに含まれている件数

    - **`totalCount` (required)**

      `number` — 取得対象全体の件数

* **`errors` (required)**

  `array` — エラーメッセージの配列。正常時は空配列。

  **Items:**

  `string`

* **`meta` (required)**

  `object` — リクエストに関するメタ情報（課金・消費リソースなど）

  - **`consumedCredit` (required)**

    `number` — このリクエストで消費されたクレジット数。

* **`result` (required)**

  `boolean` — API 呼び出しの成否。正常時は true、エラー時は false。

**Example:**

```json
{
  "result": true,
  "meta": {
    "consumedCredit": 1.5
  },
  "data": {
    "query": {
      "filter": {
        "keyword": {
          "includes": [
            "水族館"
          ],
          "notIncludes": [
            "グッズ"
          ]
        },
        "domain": {
          "includes": [
            "example.com"
          ],
          "notIncludes": [
            "example.net"
          ],
          "matchType": "partialMatch"
        },
        "totalEtv": {
          "min": 100,
          "max": 10000
        },
        "keywordCount": {
          "min": 100,
          "max": 10000
        },
        "pageCount": {
          "min": 100,
          "max": 10000
        },
        "totalTrafficValue": {
          "min": 100,
          "max": 10000
        },
        "relatedContentEtv": {
          "min": 100,
          "max": 10000
        },
        "contentRelevance": {
          "min": 1,
          "max": 100
        }
      }
    },
    "summary": {
      "totalCount": 150,
      "returnedCount": 100
    },
    "items": [
      {
        "no": 1,
        "site": {
          "domain": "rakkokeyword.com",
          "url": "https://rakkokeyword.com/",
          "title": "ラッコキーワード",
          "description": "多機能でサクサク使えるキーワードリサーチツール。"
        },
        "metrics": {
          "estimatedTraffic": 140000,
          "trafficValue": 22660,
          "rankingKeywordCount": 1800,
          "pageCount": 320
        },
        "relatedContent": {
          "estimatedTraffic": 12000,
          "relevanceScore": 42
        }
      }
    ]
  },
  "errors": []
}
```
