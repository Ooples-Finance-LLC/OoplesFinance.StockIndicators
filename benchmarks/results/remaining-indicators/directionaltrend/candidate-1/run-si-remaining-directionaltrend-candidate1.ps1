param(
    [Parameter(Mandatory = $true)][string]$Stem,
    [Parameter(Mandatory = $true)][string]$Filter
)
$ErrorActionPreference = 'Stop'
$repo = 'C:/Users/cheat/source/repos/Ooples-Finance-LLC/si-adaptive-v1-rollout-work'
$artifactPath = 'Z:/StockIndicators-work/si-followon-performance-20261018/' + $Stem
$logPath = $artifactPath + '.log'
$errorPath = $artifactPath + '.stderr.log'
$samplePath = $artifactPath + '.processes.jsonl'
if ($Stem -notmatch '^si-[a-z0-9-]+$' -or $Filter -ne '*RemainingDirectionalTrendBenchmarks.Shared*') { throw 'Unexpected run name or benchmark filter.' }
foreach ($path in @($artifactPath, $logPath, $errorPath, $samplePath, ($artifactPath + '.exit'), ($artifactPath + '.pid'))) {
    if (Test-Path -LiteralPath $path) { throw 'Fresh artifact paths are required.' }
}
python Z:/StockIndicators-work/si-followon-performance-20261018/verify-si-remaining-snapshot.py 'Z:/StockIndicators-work/si-measured-gap-406c85e4-preserved'
if ($LASTEXITCODE -ne 0) { throw 'Preserved snapshot verification failed' }
. Z:/StockIndicators-work/si-followon-performance-20261018/si-quiet-process-monitor.ps1
$quietPath = $artifactPath + '.quiet.json'
$quietHistory = @(Get-Content -LiteralPath $quietPath -Raw | ConvertFrom-Json -DateKind String)
if ($quietHistory.Count -lt 5 -or ([DateTimeOffset]$quietHistory[-1].Time - [DateTimeOffset]$quietHistory[0].Time).TotalSeconds -lt 240) { throw 'Missing 240-second idle evidence.' }
if (([DateTimeOffset]::UtcNow - [DateTimeOffset]$quietHistory[-1].Time).TotalSeconds -gt 30) { throw 'Stale quiet evidence.' }
foreach ($quiet in $quietHistory) {
    if (@(Get-TimingActivity $quietHistory[0] $quiet).Count -gt 0) { throw 'Activity during quiet window.' }
}
$idleBaseline = $quietHistory[-1]
$sample = Get-TimingSample
$sample | ConvertTo-Json -Depth 4 -Compress | Add-Content -LiteralPath $samplePath
if (@(Get-TimingActivity $idleBaseline $sample).Count -gt 0) { Write-Output 'New activity at launch; return to quiet monitoring.'; exit 2 }
$dll = 'Z:/StockIndicators-work/si-measured-gap-406c85e4-preserved/benchmarks/OoplesFinance.StockIndicators.CompetitorBenchmarks/bin/Release/net10.0/OoplesFinance.StockIndicators.CompetitorBenchmarks.dll'
$runArguments = @($dll, '--filter', $Filter, '--warmupCount', '10', '--iterationCount', '10', '--exporters', 'json', 'csv', '--artifacts', $artifactPath)
$process = Start-Process -FilePath 'C:/Users/cheat/temp/dotnet-10.0.12/dotnet.exe' -ArgumentList $runArguments -WorkingDirectory $repo -WindowStyle Hidden -RedirectStandardOutput $logPath -RedirectStandardError $errorPath -PassThru
$process.Id | Set-Content -LiteralPath ($artifactPath + '.pid')
[pscustomobject]@{ Time = [DateTime]::UtcNow.ToString('o'); Phase = 'benchmark-running'; Stage = 'directionaltrend-candidate1'; BenchmarkProcessId = $process.Id; Artifacts = $artifactPath } | ConvertTo-Json | Set-Content -LiteralPath Z:/StockIndicators-work/si-followon-performance-20261018/status.json
Write-Output ('Benchmark PID ' + $process.Id + '; artifacts ' + $artifactPath)
$previousSample = $sample
$interrupted = $false
while (!$process.HasExited) {
    $sample = Get-TimingSample
    $sample | ConvertTo-Json -Depth 4 -Compress | Add-Content -LiteralPath $samplePath
    $competitors = @(Get-TimingActivity $idleBaseline $sample $process.Id) + @(Get-TimingActivity $previousSample $sample $process.Id)
    $previousSample = $sample
    if ($competitors.Count -gt 0) {
        $competitors | ConvertTo-Json -Depth 3
        $own = Get-CimInstance Win32_Process -Filter ('ProcessId = ' + $process.Id)
        if ($own -and $own.CommandLine.Contains($artifactPath) -and $own.CommandLine.Contains('OoplesFinance.StockIndicators.CompetitorBenchmarks.dll')) {
            Stop-Process -Id $process.Id
            $interrupted = $true
            Write-Output 'Stopped only our verified benchmark; the run is diagnostic, not timing qualification.'
        }
        elseif (!$process.HasExited) { throw 'Unable to verify benchmark identity; no process was stopped.' }
        else { $interrupted = $true }
        break
    }
    [void]$process.WaitForExit(10000)
    $process.Refresh()
}
$process.WaitForExit()
$sample = Get-TimingSample
$sample | ConvertTo-Json -Depth 4 -Compress | Add-Content -LiteralPath $samplePath
if (@(Get-TimingActivity $idleBaseline $sample $process.Id).Count -gt 0 -or @(Get-TimingActivity $previousSample $sample $process.Id).Count -gt 0) { $interrupted = $true }
$exitCode = $process.ExitCode
[pscustomobject]@{ ExitCode = $exitCode; Interrupted = $interrupted; Time = [DateTime]::UtcNow.ToString('o') } | ConvertTo-Json -Compress | Set-Content -LiteralPath ($artifactPath + '.exit')
Get-Content -LiteralPath $logPath -Tail 10
if ($interrupted) { exit 2 }
exit $exitCode


