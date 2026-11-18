$ErrorActionPreference='Stop'
function Hash([string]$path) { (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() }
function Bytes { (Get-ChildItem -LiteralPath $env:PROFILE_OUTPUT -Recurse -File | Measure-Object Length -Sum).Sum }
function Cap { if ((Bytes) -gt 104857600) { throw '100MiB complete-output cap exceeded; preserve evidence' } }
if ((dotnet --version).Trim() -ne '10.0.403') { throw 'Wrong SDK' }
$principal=[Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
if (!$principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Kernel CPU capture requires an already elevated isolated runner' }
$inputs=Join-Path $env:INPUT_ARTIFACT 'predicate-inputs'
$proof=Join-Path $inputs 'provenance.json'
$config=Join-Path $inputs 'common/OoplesFinance.StockIndicators.CompetitorBenchmarks.runtimeconfig.json'
$candidate=$env:CANDIDATE_ARTIFACT
$manifestPath=Join-Path $candidate 'manifest.json'
if ((Hash $proof) -ne '9013cb468cec2d505c0eb2fb6050517d2a1a5e28b441ade2cd55523be298bf78' -or (Hash $config) -ne 'c230a317a54dd960bcbeb5f347f52e18dc665a26f7efda2159fced9a5ac7e097') { throw 'Wrong frozen inputs/runtime configuration' }
if ((Hash $manifestPath) -ne 'f6a3bfc7781d4a9ccbfe9281f8f72e1d5c248ff4848ee919507b458ab1106055') { throw 'Wrong authenticated candidate manifest' }
$manifest=Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest.source -ne '093969bb3650c3e2aad384cd0ecb2a991084ab49' -or $manifest.run_id -ne '44201563051' -or $manifest.workflow_head -ne '2a1737f30e5e1164189d0209b39ea46bf38c1239' -or $manifest.checks.unit.passed -ne 46 -or $manifest.checks.parity.passed -ne 597) { throw 'Candidate source/build/actual focused results mismatch' }
$core='c4915d59aef682579cd0a3edfae98fa363f9694e1321687bdf2b48e32b4aa005'
$pdb='77579d1f431ca3b94e8a6ba490d0eed016bd33cd914f7f72a7f51648fad2a636'
if ($manifest.runtime_files.'src/bin/Release/net10.0/OoplesFinance.StockIndicators.dll' -ne $core -or $manifest.runtime_files.'src/bin/Release/net10.0/OoplesFinance.StockIndicators.pdb' -ne $pdb) { throw 'Wrong candidate core/PDB binding' }
foreach ($sha in @($core,$pdb)) { if ((Hash (Join-Path $candidate ('blobs/'+$sha))) -ne $sha) { throw 'Changed candidate blob' } }
$proofData=Get-Content -LiteralPath $proof -Raw | ConvertFrom-Json
if (@($proofData.common_manifest.PSObject.Properties).Count -ne 94) { throw 'Expected frozen 94-file closure' }
$common=[IO.Path]::GetFullPath((Join-Path $inputs 'common'))
if (Test-Path -LiteralPath $env:PROFILE_RUNTIME) { throw 'Fresh isolated runtime required' }
$links=@{}
foreach ($entry in $proofData.common_manifest.PSObject.Properties) {
    $source=[IO.Path]::GetFullPath((Join-Path $common $entry.Name));$target=[IO.Path]::GetFullPath((Join-Path $env:PROFILE_RUNTIME $entry.Name))
    if (!$source.StartsWith($common+'\',[StringComparison]::OrdinalIgnoreCase) -or !$target.StartsWith($env:PROFILE_RUNTIME+'\',[StringComparison]::OrdinalIgnoreCase) -or (Hash $source) -ne $entry.Value) { throw 'Escaped or changed frozen member' }
    $links[$target]=$source
}
# Select the new core before linking. No old hardlinked file is overwritten.
$links[(Join-Path $env:PROFILE_RUNTIME 'OoplesFinance.StockIndicators.dll')]=Join-Path $candidate ('blobs/'+$core)
foreach ($entry in $links.GetEnumerator()) {
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($entry.Key)) | Out-Null
    New-Item -ItemType HardLink -Path $entry.Key -Target $entry.Value | Out-Null
}
$symbols=Join-Path $env:PROFILE_OUTPUT 'symbols';[IO.Directory]::CreateDirectory($symbols) | Out-Null
Copy-Item -LiteralPath (Join-Path $candidate ('blobs/'+$pdb)) -Destination (Join-Path $symbols 'OoplesFinance.StockIndicators.pdb')
$env:_NT_SYMBOL_PATH=$symbols
Copy-Item -LiteralPath $manifestPath -Destination (Join-Path $env:PROFILE_OUTPUT 'candidate-manifest.json')
Copy-Item -LiteralPath $proof -Destination (Join-Path $env:PROFILE_OUTPUT 'input-provenance.json')
$tool=Join-Path $env:RUNNER_TEMP 'five-candle-block-PerfView-4.0.0.exe'
if (Test-Path -LiteralPath $tool) { throw 'Fresh collector required' }
Invoke-WebRequest -Uri 'https://github.com/microsoft/perfview/releases/download/v4.0.0/PerfView.exe' -OutFile $tool
if ((Hash $tool) -ne '270e2ba419b0b2d93c4b15b0851f257edf655c92fe0ad95add3d0b5f6d6aad48') { throw 'Wrong known-valid PerfView bytes' }
[ordered]@{sha256=(Hash $tool);administrator=$true;kernel='Process,Thread,ImageLoad,Profile';clr='GC,Jit,Loader,JittedMethodILToNativeMap';raw_only=$true;qualification=$false} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $env:PROFILE_OUTPUT 'tool.json') -Encoding utf8
$driverRoot=Join-Path $env:RUNNER_TEMP 'five-candle-block-profile-driver'
if (Test-Path -LiteralPath $driverRoot) { throw 'Fresh helper directory required' }
[IO.Directory]::CreateDirectory($driverRoot) | Out-Null
'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><UseSharedCompilation>false</UseSharedCompilation></PropertyGroup></Project>' | Set-Content -LiteralPath (Join-Path $driverRoot 'DiagnosticDriver.csproj') -Encoding utf8
$script=Join-Path $env:GITHUB_WORKSPACE 'scripts/profile-five-candle-pinned-runtime.cs'
Copy-Item -LiteralPath $script -Destination (Join-Path $driverRoot 'Program.cs')
dotnet build (Join-Path $driverRoot 'DiagnosticDriver.csproj') -c Release -o (Join-Path $driverRoot 'out') --disable-build-servers --nologo *> (Join-Path $env:PROFILE_OUTPUT 'driver-build.log')
if ($LASTEXITCODE -ne 0) { throw 'Helper-only build failed; no source/test build permitted' }
$driver=Join-Path $driverRoot 'out/DiagnosticDriver.dll';$dotnet=(Get-Command dotnet).Source
function IdleGate($name) {
    $samples=[Collections.Generic.List[double]]::new()
    for ($i=0;$i -lt 12;$i++) { $samples.Add((Get-Counter '\Processor(_Total)\% Processor Time' -SampleInterval 1 -MaxSamples 1).CounterSamples[0].CookedValue) }
    $busy=@(Get-CimInstance Win32_Process | Where-Object { $_.Name -match '^(dotnet|MSBuild|testhost|VBCSCompiler)(\.exe)?$' -and $_.CommandLine -match '(?i)(\s(build|test|vstest)\s|MSBuild\.(exe|dll)|testhost|VBCSCompiler)' } | Select-Object ProcessId,Name,CommandLine)
    $passed=$samples.Count -eq 12 -and @($samples | Where-Object { ![double]::IsFinite($_) -or $_ -lt 0 -or $_ -ge 15 }).Count -eq 0 -and $busy.Count -eq 0
    [ordered]@{cpu_percent=$samples.ToArray();build_test_processes=$busy;passed=$passed} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $env:PROFILE_OUTPUT ($name+'.json')) -Encoding utf8
    return $passed
}
$null=Get-Counter '\Processor(_Total)\% Processor Time' -SampleInterval 1 -MaxSamples 2
Start-Sleep -Seconds 5
if (!(IdleGate 'idle-before')) { throw 'Pre-capture idle gate failed; no trace' }
$pairs=@('ConcealingBabySwallow','LadderBottom','ThreeStarsInSouth','Breakaway','RisingFallingThreeMethods')
$records=[Collections.Generic.List[object]]::new();$valid=$false;$failure=$null;$post=$false;$sourceSeconds=0.0
try {
    foreach ($name in $pairs) {
        Cap
        if ($sourceSeconds+5 -gt 30) { throw 'Insufficient remaining 30 second source-window budget; no further case launched' }
        $folder=Join-Path $env:PROFILE_OUTPUT $name;[IO.Directory]::CreateDirectory($folder) | Out-Null
        $trace=Join-Path $folder 'cpu.etl';$log=Join-Path $folder 'cpu.log';$replay=Join-Path $folder 'replay'
        $arguments=@('/AcceptEula','/NoGui','/NoView','/KernelEvents:Process,Thread,ImageLoad,Profile','/ClrEvents:GC,Jit,Loader,JittedMethodILToNativeMap','/ClrEventLevel:Informational','/FocusProcess:dotnet.exe','/BufferSizeMB:16','/MaxCollectSec:25','/Merge:false','/Zip:false',('/DataFile:'+$trace),('/LogFile:'+$log),'run',$dotnet,'exec','--runtimeconfig',$config,$driver,$inputs,$env:PROFILE_RUNTIME,$candidate,'new',('TaLib.Candles.'+$name),$replay)
        $info=[Diagnostics.ProcessStartInfo]::new();$info.FileName=$tool;$info.UseShellExecute=$false;$info.CreateNoWindow=$true;$info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
        foreach ($argument in $arguments) { $info.ArgumentList.Add([string]$argument) }
        $process=[Diagnostics.Process]::new();$process.StartInfo=$info;$clock=[Diagnostics.Stopwatch]::StartNew();$started=$false;$limit=$null
        try {
            if (!$process.Start()) { throw 'Cannot launch kernel collector' };$started=$true
            $stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
            while (!$process.WaitForExit(250)) {
                if ($clock.Elapsed.TotalSeconds -gt 40) { $limit='40 second process cap';break }
                if ((Bytes) -gt 104857600) { $limit='100MiB total cap';break }
            }
            if ($limit) { $process.Kill($true);$process.WaitForExit() }
            $stdout.GetAwaiter().GetResult() | Set-Content -LiteralPath (Join-Path $folder 'capture.log') -Encoding utf8
            $stderr.GetAwaiter().GetResult() | Set-Content -LiteralPath (Join-Path $folder 'capture-error.log') -Encoding utf8
            [ordered]@{arguments=$arguments;exit_code=$process.ExitCode;elapsed_seconds=$clock.Elapsed.TotalSeconds;limit_failure=$limit;driver_sha256=(Hash $driver);script_sha256=(Hash $script);diagnostic_only=$true} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $folder 'capture.json') -Encoding utf8
            if ($limit -or $process.ExitCode -ne 0) { throw 'Capture failed; preserve raw evidence' }
            foreach ($file in @('cpu.etl','cpu.kernel.etl','cpu.clrRundown.etl')) { if (!(Test-Path -LiteralPath (Join-Path $folder $file) -PathType Leaf) -or (Get-Item -LiteralPath (Join-Path $folder $file)).Length -le 0) { throw 'Required raw ETL missing' } }
            $text=Get-Content -LiteralPath $log -Raw
            if ($text -notmatch ' SUCCESS:' -or $text -match '\[Error:' -or $text -match '\[Merging data files' -or (Test-Path -LiteralPath ($trace+'.zip'))) { throw 'Unclean or merged capture' }
            $receipt=Get-Content -LiteralPath (Join-Path $replay 'replay.json') -Raw | ConvertFrom-Json
            if ($receipt.status -ne 'complete' -or $receipt.pair -ne ('TaLib.Candles.'+$name) -or $receipt.warmups -ne 1000 -or $receipt.profile_seconds -ne 5 -or $receipt.actual_clr -ne '10.0.15' -or !$receipt.runtime_unchanged -or !$receipt.protected_inputs_unchanged -or $receipt.settings_after.dop -ne 2 -or $receipt.settings_after.effective_workers -ne 2) { throw 'Incomplete source/runtime/worker identity' }
            $ticks=[long]$receipt.profile_end_timestamp-[long]$receipt.profile_start_timestamp
            $frequency=[long]$receipt.timestamp_frequency
            if ($ticks -le 0 -or $frequency -le 0) { throw 'Invalid direct QPC source-window boundary' }
            $windowSeconds=[double]$ticks/[double]$frequency
            if (![double]::IsFinite($windowSeconds) -or $windowSeconds -lt 5) { throw 'Incomplete fixed source window' }
            $sourceSeconds+=$windowSeconds
            $records.Add($receipt);Cap
            if ($sourceSeconds -gt 30) { throw '30 second QPC source-window cap exceeded; no further case launched' }
        } finally { if ($started -and !$process.HasExited) { $process.Kill($true);$process.WaitForExit() };$process.Dispose() }
    }
    if ($records.Count -ne 5) { throw 'Incomplete fixed case scope' }
    if ($sourceSeconds -gt 30) { throw '30 second total QPC source-window cap exceeded' }
    $valid=$true
} catch { $failure=$_.Exception.ToString();throw }
finally {
    $post=IdleGate 'idle-after';if (!$post) { $valid=$false }
    if ((Bytes) -gt 104857600) { $valid=$false;$failure=([string]$failure)+' 100MiB total output cap' }
    $validation=[ordered]@{diagnostic_only=$true;qualification=$false;source='093969bb3650c3e2aad384cd0ecb2a991084ab49';complete=$valid;failure=$failure;post_idle_passed=$post;cases=$records.Count;source_windows_seconds=$sourceSeconds;window_clock='Direct QPC ticks/frequency; UTC retained only as a secondary marker';observer_boundary_note='QPC start precedes PROFILE START console output and the hot timer; retain this small observer prefix during analysis';output_bytes=(Bytes);production_builds=0;tests=0;driver_builds=1;native_or_latency_claim=$false}
    $validation | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $env:PROFILE_OUTPUT 'profile-validation.json') -Encoding utf8
    $outputManifest=Join-Path $env:PROFILE_OUTPUT 'output-manifest.json'
    Get-ChildItem -LiteralPath $env:PROFILE_OUTPUT -Recurse -File | Where-Object { $_.FullName -ne $outputManifest } | ForEach-Object { [ordered]@{path=$_.FullName.Substring($env:PROFILE_OUTPUT.Length);bytes=$_.Length;sha256=(Hash $_.FullName)} } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $outputManifest -Encoding utf8
    if ((Bytes) -gt 104857600) {
        $valid=$false;$validation.complete=$false;$validation.failure=([string]$validation.failure)+' 100MiB cap including final metadata';$validation.output_bytes=(Bytes)
        $validation | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $env:PROFILE_OUTPUT 'profile-validation.json') -Encoding utf8
        Get-ChildItem -LiteralPath $env:PROFILE_OUTPUT -Recurse -File | Where-Object { $_.FullName -ne $outputManifest } | ForEach-Object { [ordered]@{path=$_.FullName.Substring($env:PROFILE_OUTPUT.Length);bytes=$_.Length;sha256=(Hash $_.FullName)} } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $outputManifest -Encoding utf8
    }
    if (!$valid) { throw 'Diagnostic incomplete/invalid; no rerun or inference authorized' }
}
