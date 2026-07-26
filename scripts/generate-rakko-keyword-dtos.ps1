param(
    [switch]$ValidateOnly
)

# 本スクリプトは生成DTOのメタデータ(OpenApiVersion/SourceSha256)と必須スキーマ名の存在を検証・更新する。
# DTOのプロパティ・required制約の形状検証は ContractTests の RakkoKeywordDtoShapeContractTests が担う。
# 更新手順は docs/adr/0006-openapi-dto-generation.md を参照。

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$specPath = Join-Path $root "docs/rakko-keyword-api-docs.json"
$targetPath = Join-Path $root "src/SeoIntelligence.Infrastructure/RakkoKeyword/Generated/RakkoKeywordDtos.g.cs"
$phase2TargetPath = Join-Path $root "src/SeoIntelligence.Infrastructure/RakkoKeyword/Generated/RakkoKeywordPhase2Dtos.g.cs"

$mvpSchemas = @(
    "SuggestKeywordsDto",
    "SuggestKeywordsResponseDto",
    "RelatedKeywordsDto",
    "RelatedKeywordsResponseDto",
    "OtherKeywordsDto",
    "OtherKeywordsResponseDto",
    "SearchQuestionDto",
    "SearchQuestionResponseDto",
    "RankingKeywordsDto",
    "RankingKeywordsResponseDto",
    "SearchVolumeHistoryDto",
    "SearchVolumeHistoryResponseDto",
    "SearchVolumeStatusResponseDto",
    "SearchVolumeResultsDto",
    "SearchVolumeResultsResponseDto",
    "MetadataLocationsResponseDto",
    "MetadataLanguagesResponseDto"
)

$phase2Schemas = @(
    "InfluxKeywordsKeywordDto",
    "InfluxKeywordsKeywordResponseDto",
    "InfluxPagesDto",
    "InfluxPagesResponseDto",
    "CompetitiveDto",
    "CompetitiveResponseDto",
    "ContentSearchDto",
    "ContentSearchResponseDto",
    "HeadlineDto",
    "HeadlineResponseDto",
    "CoOccurrenceDto",
    "CoOccurrenceResponseDto",
    "SearchRankHistoryDto",
    "SearchRankHistoryResponseDto",
    "SearchRankStatusResponseDto",
    "SearchRankResultsDto",
    "SearchRankResultsResponseDto"
)

$requiredSchemas = @($mvpSchemas + $phase2Schemas)

$spec = Get-Content -Encoding UTF8 -Raw $specPath | ConvertFrom-Json
$specText = [System.IO.File]::ReadAllText($specPath, [System.Text.Encoding]::UTF8)
$normalizedSpecText = $specText -replace "`r`n", "`n"
$normalizedSpecBytes = [System.Text.Encoding]::UTF8.GetBytes($normalizedSpecText)
$sha256 = [System.Security.Cryptography.SHA256]::Create()
$hash = (($sha256.ComputeHash($normalizedSpecBytes) | ForEach-Object { $_.ToString("x2") }) -join "")
$version = [string]$spec.info.version
$schemaNames = @($spec.components.schemas.PSObject.Properties.Name)

foreach ($schemaName in $requiredSchemas) {
    if ($schemaNames -notcontains $schemaName) {
        throw "Required schema '$schemaName' is missing from $specPath."
    }
}

if (-not (Test-Path $targetPath)) {
    throw "Generated DTO file is missing: $targetPath."
}

if (-not (Test-Path $phase2TargetPath)) {
    throw "Generated Phase 2 DTO file is missing: $phase2TargetPath."
}

$contentsByPath = [ordered]@{
    $targetPath = Get-Content -Encoding UTF8 -Raw $targetPath
    $phase2TargetPath = Get-Content -Encoding UTF8 -Raw $phase2TargetPath
}
$expectedVersionLine = "public const string OpenApiVersion = `"$version`";"
$expectedHashLine = "public const string SourceSha256 = `"$hash`";"

if ($ValidateOnly) {
    foreach ($path in $contentsByPath.Keys) {
        $content = $contentsByPath[$path]
        if (-not $content.Contains($expectedVersionLine)) {
            throw "Generated DTO metadata OpenApiVersion is out of date in $path. Expected $version."
        }

        if (-not $content.Contains($expectedHashLine)) {
            throw "Generated DTO metadata SourceSha256 is out of date in $path. Expected $hash."
        }
    }

    Write-Output "Rakko Keyword generated DTO metadata is up to date."
    exit 0
}

foreach ($path in $contentsByPath.Keys) {
    $content = [regex]::Replace(
        $contentsByPath[$path],
        'public const string OpenApiVersion = "[^"]+";',
        $expectedVersionLine)
    $content = [regex]::Replace(
        $content,
        'public const string SourceSha256 = "[^"]+";',
        $expectedHashLine)

    [System.IO.File]::WriteAllText($path, $content, [System.Text.UTF8Encoding]::new($false))
}

Write-Output "Updated Rakko Keyword generated DTO metadata from $specPath."
