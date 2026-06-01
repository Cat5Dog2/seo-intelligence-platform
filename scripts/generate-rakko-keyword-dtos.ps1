param(
    [switch]$ValidateOnly
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$specPath = Join-Path $root "docs/rakko-keyword-api-docs.json"
$targetPath = Join-Path $root "src/SeoIntelligence.Infrastructure/RakkoKeyword/Generated/RakkoKeywordDtos.g.cs"

$requiredSchemas = @(
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
    "LocationsResponseDto",
    "LanguagesResponseDto"
)

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

$content = Get-Content -Encoding UTF8 -Raw $targetPath
$expectedVersionLine = "public const string OpenApiVersion = `"$version`";"
$expectedHashLine = "public const string SourceSha256 = `"$hash`";"

if ($ValidateOnly) {
    if (-not $content.Contains($expectedVersionLine)) {
        throw "Generated DTO metadata OpenApiVersion is out of date. Expected $version."
    }

    if (-not $content.Contains($expectedHashLine)) {
        throw "Generated DTO metadata SourceSha256 is out of date. Expected $hash."
    }

    Write-Output "Rakko Keyword generated DTO metadata is up to date."
    exit 0
}

$content = [regex]::Replace(
    $content,
    'public const string OpenApiVersion = "[^"]+";',
    $expectedVersionLine)
$content = [regex]::Replace(
    $content,
    'public const string SourceSha256 = "[^"]+";',
    $expectedHashLine)

[System.IO.File]::WriteAllText($targetPath, $content, [System.Text.UTF8Encoding]::new($false))
Write-Output "Updated Rakko Keyword generated DTO metadata from $specPath."
