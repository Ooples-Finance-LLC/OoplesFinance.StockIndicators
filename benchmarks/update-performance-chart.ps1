param(
    [string]$ReadmePath = "README.md",
    [string]$CsvPath,
    [int]$Count = 10000
)

$ErrorActionPreference = "Stop"

if (-not $CsvPath) {
    $CsvPath = Get-ChildItem -Path "BenchmarkDotNet.Artifacts/results" -Filter "*IndicatorBenchmarks*-report.csv" |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1 -ExpandProperty FullName
}

if (-not $CsvPath) {
    throw "No benchmark CSV found. Run the benchmarks or pass -CsvPath."
}

$rows = Import-Csv -Path $CsvPath

$indicatorMap = @(
    @{ Category = "SMA"; Display = "SMA" },
    @{ Category = "EMA"; Display = "EMA" },
    @{ Category = "RSI"; Display = "RSI" },
    @{ Category = "MACD"; Display = "MACD" },
    @{ Category = "Bollinger"; Display = "Bollinger Bands" },
    @{ Category = "ATR"; Display = "ATR" },
    @{ Category = "Chande"; Display = "Chande CMO" },
    @{ Category = "Ulcer"; Display = "Ulcer Index" }
)

$lengths = @(14, 50, 200)

function Convert-MeanToNumber {
    param([string]$Value)
    if ([string]::IsNullOrWhiteSpace($Value)) {
        return $null
    }

    $clean = $Value -replace "[^0-9\.]", ""
    if ([string]::IsNullOrWhiteSpace($clean)) {
        return $null
    }

    return [double]::Parse($clean, [System.Globalization.CultureInfo]::InvariantCulture)
}

function Format-Number {
    param([double]$Value)
    return $Value.ToString("0.0", [System.Globalization.CultureInfo]::InvariantCulture)
}

function Format-Speedup {
    param([double]$Value)
    return ($Value.ToString("0.0", [System.Globalization.CultureInfo]::InvariantCulture) + "x")
}

$tableBuilder = New-Object System.Text.StringBuilder

foreach ($length in $lengths) {
    [void]$tableBuilder.AppendLine("Length = $length")
    [void]$tableBuilder.AppendLine("| Indicator | Optimized (us) | Baseline (us) | Speedup |")
    [void]$tableBuilder.AppendLine("| --- | --- | --- | --- |")

    foreach ($indicator in $indicatorMap) {
        $filtered = $rows | Where-Object {
            $_.Categories -eq $indicator.Category -and
            $_.Count -eq $Count.ToString() -and
            $_.Length -eq $length.ToString()
        }

        $optimizedRow = $filtered | Where-Object { $_.Method -notmatch "_Original$" } | Select-Object -First 1
        $baselineRow = $filtered | Where-Object { $_.Method -match "_Original$" } | Select-Object -First 1

        if (-not $optimizedRow -or -not $baselineRow) {
            throw "Missing benchmark row for $($indicator.Category) length $length count $Count."
        }

        $optimizedMean = Convert-MeanToNumber $optimizedRow.Mean
        $baselineMean = Convert-MeanToNumber $baselineRow.Mean

        if ($null -eq $optimizedMean -or $null -eq $baselineMean) {
            throw "Missing mean values for $($indicator.Category) length $length count $Count."
        }

        $speedup = $baselineMean / $optimizedMean
        [void]$tableBuilder.AppendLine("| $($indicator.Display) | $(Format-Number $optimizedMean) | $(Format-Number $baselineMean) | $(Format-Speedup $speedup) |")
    }

    [void]$tableBuilder.AppendLine("")
}

$content = Get-Content -Path $ReadmePath -Raw
$start = "<!-- PERF_TABLES_START -->"
$end = "<!-- PERF_TABLES_END -->"

if ($content -notmatch [regex]::Escape($start) -or $content -notmatch [regex]::Escape($end)) {
    throw "PERF_TABLES markers not found in $ReadmePath."
}

$pattern = [regex]::Escape($start) + "[\\s\\S]*?" + [regex]::Escape($end)
$replacement = $start + "`r`n" + $tableBuilder.ToString().TrimEnd() + "`r`n" + $end
$updated = [regex]::Replace($content, $pattern, $replacement)

Set-Content -Path $ReadmePath -Value $updated -Encoding UTF8

Write-Host "Updated performance tables in $ReadmePath using $CsvPath."
