param([Parameter(Mandatory)][string]$Runtime,[Parameter(Mandatory)][ValidateSet('batch','builder')][string]$Profile,[Parameter(Mandatory)][string]$Output,[Parameter(Mandatory)][string]$ProductionSha)
$ErrorActionPreference='Stop'
if ((Get-FileHash -LiteralPath "$Runtime/OoplesFinance.StockIndicators.dll").Hash -ne $ProductionSha) { throw 'Wrong production image' }
$assembly=[Reflection.Assembly]::LoadFrom("$Runtime/OoplesFinance.StockIndicators.CompetitorBenchmarks.dll")
if ($Profile -eq 'batch') {
    $benchmark=[Activator]::CreateInstance($assembly.GetType('OoplesFinance.StockIndicators.CompetitorBenchmarks.LibraryPairBenchmarks',$true))
    $benchmark.PairId='TaLib.Functions.Log10'; $benchmark.Bars=10000; $method='Ooples'; $warmups=1000
} else {
    $benchmark=[Activator]::CreateInstance($assembly.GetType('OoplesFinance.StockIndicators.CompetitorBenchmarks.SharedPointwiseBenchmarks',$true))
    $benchmark.Operation='Log10'; $benchmark.Count=10000; $method='BuilderFull'; $warmups=10
}
$benchmark.Setup()
$invoke=$benchmark.GetType().GetMethod($method).CreateDelegate([Func[object]],$benchmark)
for ($i=0;$i -lt $warmups;$i++) { $null=$invoke.Invoke() }
$loaded=@([AppDomain]::CurrentDomain.GetAssemblies() | Where-Object {$_.GetName().Name -eq 'OoplesFinance.StockIndicators'})
if ($loaded.Count -ne 1 -or $loaded[0].Location -ne [IO.Path]::GetFullPath("$Runtime/OoplesFinance.StockIndicators.dll")) { throw 'Unexpected loaded image' }
@{pid=$PID;profile=$Profile;method=$method;bars=10000;warmups=$warmups;maximum_degree_of_parallelism=[AiDotNet.Tensors.Helpers.CpuParallelSettings]::MaxDegreeOfParallelism;runtime=[Runtime.InteropServices.RuntimeInformation]::FrameworkDescription;production_sha256=(Get-FileHash $loaded[0].Location).Hash.ToLowerInvariant();benchmark_sha256=(Get-FileHash "$Runtime/OoplesFinance.StockIndicators.CompetitorBenchmarks.dll").Hash.ToLowerInvariant()} | ConvertTo-Json | Set-Content -LiteralPath "$Output/ready.json" -Encoding utf8
$deadline=[DateTime]::UtcNow.AddSeconds(60)
while (!(Test-Path -LiteralPath "$Output/go")) { if ([DateTime]::UtcNow -gt $deadline) { throw 'Trace attachment timed out' }; Start-Sleep -Milliseconds 100 }
$chunks=@()
for ($chunk=0;$chunk -lt 10;$chunk++) {
    $before=[GC]::GetTotalAllocatedBytes($true); $cpu=[Diagnostics.Process]::GetCurrentProcess().TotalProcessorTime; $pause=[GC]::GetTotalPauseDuration(); $watch=[Diagnostics.Stopwatch]::StartNew()
    for ($i=0;$i -lt 1000;$i++) { $last=$invoke.Invoke() }
    $watch.Stop()
    $chunks+=@{chunk=$chunk;invocations=1000;wall_seconds=$watch.Elapsed.TotalSeconds;process_cpu_seconds=([Diagnostics.Process]::GetCurrentProcess().TotalProcessorTime-$cpu).TotalSeconds;gc_pause_seconds=([GC]::GetTotalPauseDuration()-$pause).TotalSeconds;host_inclusive_allocated_bytes=([GC]::GetTotalAllocatedBytes($true)-$before)}
}
[GC]::KeepAlive($last)
if ($Profile -eq 'builder') { $benchmark.Cleanup() }
@{completed=$true;kind='Unmeasured instrumented attribution replay; counters include PowerShell/delegate overhead and cannot qualify performance';profile=$Profile;chunks=$chunks;invocations=10000;production_sha256=(Get-FileHash "$Runtime/OoplesFinance.StockIndicators.dll").Hash.ToLowerInvariant()} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath "$Output/completed.json" -Encoding utf8
