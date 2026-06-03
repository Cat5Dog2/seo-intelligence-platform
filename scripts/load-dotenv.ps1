function Import-DotEnvFile {
    param(
        [string]$Path = ".env",
        [switch]$Override
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    foreach ($line in Get-Content -LiteralPath $Path -Encoding UTF8) {
        $trimmed = $line.Trim()
        if ([string]::IsNullOrWhiteSpace($trimmed) -or $trimmed.StartsWith("#")) {
            continue
        }

        if ($trimmed.StartsWith("export ")) {
            $trimmed = $trimmed.Substring(7).TrimStart()
        }

        $separator = $trimmed.IndexOf("=")
        if ($separator -lt 1) {
            continue
        }

        $name = $trimmed.Substring(0, $separator).Trim()
        if ($name -notmatch "^[A-Za-z_][A-Za-z0-9_.:-]*$") {
            throw "Invalid environment variable name in ${Path}: $name"
        }

        $value = $trimmed.Substring($separator + 1).Trim()
        if ($value.Length -ge 2) {
            $quote = $value[0]
            if (($quote -eq '"' -or $quote -eq "'") -and $value[$value.Length - 1] -eq $quote) {
                $value = $value.Substring(1, $value.Length - 2)
            }
        }

        if (-not $Override -and $null -ne [Environment]::GetEnvironmentVariable($name, "Process")) {
            continue
        }

        [Environment]::SetEnvironmentVariable($name, $value, "Process")
    }

    Write-Host "Loaded local environment variables from $Path."
}
