param(
    [Parameter(Mandatory=$true)][string]$InputRoot,
    [Parameter(Mandatory=$true)][string]$OutputRoot,
    [Parameter(Mandatory=$true)][string]$RuntimeRoot
)
$ErrorActionPreference='Stop'
# This script executes existing CI fixture methods only. No Add-Type, build,
# dynamic compilation, dependency installation, or policy assignment is used.
if ([Environment]::Version.ToString() -ne '10.0.15') { throw 'STOP: the PowerShell host must use .NET 10.0.15.' }
$inheritedFlags=@{}
foreach ($entry in [Environment]::GetEnvironmentVariables().GetEnumerator()) {
    if ($entry.Key -match '^(DOTNET_|COMPlus_).*(Tiered|TC_|Jit|ReadyToRun|GC|Roll_Forward|RollForward)') { $inheritedFlags[[string]$entry.Key]=[string]$entry.Value }
}
if ($inheritedFlags.Count -ne 0) { throw ('STOP: inherited runtime overrides require review: '+($inheritedFlags.Keys -join ',')) }
$customPattern='^(OoplesFinance|AiDotNet|TALib|Trady|QuanTAlib|Skender|BenchmarkDotNet)'
foreach ($assembly in [AppDomain]::CurrentDomain.GetAssemblies()) {
    if ($assembly.GetName().Name -match $customPattern) { throw ('STOP: custom assembly already loaded: '+$assembly.FullName) }
}
$inputs=[IO.Path]::GetFullPath($InputRoot)
$runtime=[IO.Path]::GetFullPath($RuntimeRoot)
$outPath=[IO.Path]::GetFullPath($OutputRoot)
$proofPath=Join-Path $inputs 'provenance.json'
$source='6051a95220eefa36e6f837a9a54e036164b76ebe'
$producer='2a1737f30e5e1164189d0209b39ea46bf38c1239'
$productionSha='4d571648599e567bb6df0cda1f823d79e95712241078c2ab1721cf7409c10173'
$benchmarkSha='48229494a1ff1e4e59bddd5a018cbd590cae24523d52dd2f3effdd20bd802723'
function Hash([string]$Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }
if ((Hash $proofPath) -ne '9013cb468cec2d505c0eb2fb6050517d2a1a5e28b441ade2cd55523be298bf78') { throw 'STOP: wrong qualified input provenance bytes.' }
$proof=Get-Content -LiteralPath $proofPath -Raw | ConvertFrom-Json
if ($proof.sources.old -ne $source -or $proof.run_id -ne '44010931924' -or $proof.production_sha256.old -ne $productionSha -or $proof.common_manifest.'OoplesFinance.StockIndicators.CompetitorBenchmarks.dll' -ne $benchmarkSha) { throw 'STOP: wrong frozen-harness/old-production provenance.' }
if (@($proof.common_manifest.PSObject.Properties).Count -ne 94) { throw 'STOP: expected exact 94-file common runtime.' }
if (!(Test-Path -LiteralPath $inputs -PathType Container) -or (Test-Path -LiteralPath $outPath) -or (Test-Path -LiteralPath $runtime)) { throw 'STOP: input must exist; output/runtime roots must be fresh.' }
foreach ($pair in @(@($inputs,$runtime),@($inputs,$outPath),@($runtime,$outPath))) {
    if ($pair[0].Equals($pair[1],[StringComparison]::OrdinalIgnoreCase) -or $pair[0].StartsWith($pair[1]+'\',[StringComparison]::OrdinalIgnoreCase) -or $pair[1].StartsWith($pair[0]+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'STOP: input/runtime/output roots must be disjoint.' }
}
$protectedInputs=@{};$runtimeFiles=@{};$linkSources=@{}
$common=[IO.Path]::GetFullPath((Join-Path $inputs 'common'))
foreach ($entry in $proof.common_manifest.PSObject.Properties) {
    $relative=$entry.Name.Replace('/','\')
    $sourcePath=[IO.Path]::GetFullPath((Join-Path $common $relative))
    $target=[IO.Path]::GetFullPath((Join-Path $runtime $relative))
    if (!$sourcePath.StartsWith($common+'\',[StringComparison]::OrdinalIgnoreCase) -or !$target.StartsWith($runtime+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'STOP: runtime member escaped pinned roots.' }
    if ((Hash $sourcePath) -ne $entry.Value) { throw ('STOP: protected common bytes changed: '+$sourcePath) }
    $protectedInputs[$sourcePath]=[string]$entry.Value
    $runtimeFiles[$target]=[string]$entry.Value;$linkSources[$target]=$sourcePath
}
$productionSource=Join-Path $inputs 'production\old.dll'
if ((Hash $productionSource) -ne $productionSha) { throw 'STOP: old production bytes changed.' }
$protectedInputs[$productionSource]=$productionSha
$production=Join-Path $runtime 'OoplesFinance.StockIndicators.dll'
$benchmark=Join-Path $runtime 'OoplesFinance.StockIndicators.CompetitorBenchmarks.dll'
$runtimeFiles[$production]=$productionSha;$linkSources[$production]=$productionSource
# Every input is authenticated before creating the view. This allocates fresh
# directory entries only, not copies of the 159 MB immutable runtime payload.
[IO.Directory]::CreateDirectory($runtime) | Out-Null
foreach ($entry in $linkSources.GetEnumerator()) {
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($entry.Key)) | Out-Null
    $null=New-Item -ItemType HardLink -Path $entry.Key -Target $entry.Value -ErrorAction Stop
    if ((Hash $entry.Key) -ne $runtimeFiles[$entry.Key]) { throw 'STOP: hardlink view hash mismatch.' }
}
if ((Hash $production) -ne $productionSha -or (Hash $benchmark) -ne $benchmarkSha) { throw 'STOP: composed core/frozen benchmark mismatch.' }
[IO.Directory]::CreateDirectory($outPath) | Out-Null
$receipt=[ordered]@{
    diagnostic_only=$true; qualification=$false; performance_claim=$false
    source=$source; ci_run='43984038209'; timing_reference_run='44010931924'; compiled_source='a1f0028c601e1c6e7be5eabfe0d5da5cfe1f0758'; producer=$producer
    production_sha256=$productionSha; frozen_benchmark_sha256=$benchmarkSha
    input_provenance_sha256=(Hash $proofPath); frozen_harness_source='9a45372530e49718b01b6524f9a0db993514e1e8'
    host_clr=[Environment]::Version.ToString()
    host_framework=[Runtime.InteropServices.RuntimeInformation]::FrameworkDescription
    powershell=$PSVersionTable.PSVersion.ToString(); process_id=$PID
    environment_processor_count=[Environment]::ProcessorCount
    env_aidotnet_cpu_threads=[Environment]::GetEnvironmentVariable('AIDOTNET_CPU_THREADS')
    workflow_commit=$env:GITHUB_SHA; workflow_run=$env:GITHUB_RUN_ID
    replay_script_sha256=(Hash $PSCommandPath)
    process_start_utc=[Diagnostics.Process]::GetCurrentProcess().StartTime.ToUniversalTime().ToString('O')
    inherited_runtime_overrides=$inheritedFlags
    attribution='Managed sampled thread wall time only; not kernel CPU/full native CPU/qualified latency.'
    isolation='Installed PowerShell hosted by dotnet exec under verified frozen benchmark runtimeconfig; noncollectible AssemblyLoadContext with ADR-selected authenticated managed CI assets. No additional runtime settings, callback/native resolver, or worker-policy assignment.'
    fixture_execution_context='Unchanged frozen LibraryPairBenchmarks.Setup/Ooples with DarkCloudCover10000; observed Windows runner CLR10.0.15/default worker policy. No Program/BenchmarkDotNet job setup or manual backend/worker assignment.'
    pair='TaLib.Candles.DarkCloudCover'; bars=10000; fixture='LibraryPairBenchmarks'; arm='Ooples'
    warmups=100; profile_seconds=10; runtime_file_count=$runtimeFiles.Count
    runtime_files=$runtimeFiles; stage='authenticated'; status='running'
    hardlink_view_files=94; hardlink_new_payload_bytes=0; protected_input_files=$protectedInputs.Count; protected_input_hashes=$protectedInputs
    symbol_fidelity='Frozen common PDB retained exactly as hosted composition; no source/PDB match claimed.'
    limits='Diagnostic-only managed thread-time replay of exact frozen482 adapter and old4d production. Not CPU/native CPU shares, BenchmarkDotNet timing, native comparison, qualification or latency transfer. Runtime hardlink logical bytes are separate from the100MiB trace/output cap. Worker limits/effective WorkerCount do not by themselves prove concurrent execution; captured stacks supply that evidence.'
}
function SaveReceipt {
    $target=Join-Path $outPath 'replay.json';$temporary=$target+'.new'
    $bytes=[Text.Encoding]::UTF8.GetBytes(($receipt | ConvertTo-Json -Depth 12)+"`n")
    $stream=[IO.File]::Open($temporary,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
    try { $stream.Write($bytes,0,$bytes.Length);$stream.Flush($true) } finally { $stream.Dispose() }
    [IO.File]::Move($temporary,$target,$true)
}
SaveReceipt
try {
    try {
        $receipt.cim_processors=@(Get-CimInstance Win32_Processor | ForEach-Object { @{name=$_.Name;cores=$_.NumberOfCores;enabled_cores=$_.NumberOfEnabledCore;logical_processors=$_.NumberOfLogicalProcessors} })
        $receipt.cim_computer_logical_processors=(Get-CimInstance Win32_ComputerSystem).NumberOfLogicalProcessors
    } catch { $receipt.cim_query_error=$_.Exception.Message }
    $receipt.stage='assembly-load';SaveReceipt
    $frameworkRoot=Join-Path (Split-Path -Parent (Get-Command dotnet).Source) 'shared\Microsoft.NETCore.App\10.0.15'
    if (!(Test-Path -LiteralPath $frameworkRoot)) { throw 'STOP: existing .NET 10.0.15 framework inventory missing.' }
    $frameworkIdentities=@{}
    foreach ($file in Get-ChildItem -LiteralPath $frameworkRoot -File -Filter '*.dll') {
        try { $identity=[Reflection.AssemblyName]::GetAssemblyName($file.FullName) }
        catch [BadImageFormatException] { continue }
        $frameworkIdentities[$identity.FullName]=$file.FullName
    }
    $context=[Runtime.Loader.AssemblyLoadContext]::new('AuthenticatedDarkCloud6051Frozen482',$false)
    $resolver=[Runtime.Loader.AssemblyDependencyResolver]::new($benchmark)
    $selected=@{};$shared=@{};$nativeAssets=@()
    foreach ($path in $runtimeFiles.Keys) {
        if (![IO.Path]::GetExtension($path).Equals('.dll',[StringComparison]::OrdinalIgnoreCase)) { continue }
        try { $identity=[Reflection.AssemblyName]::GetAssemblyName($path) }
        catch [BadImageFormatException] { $nativeAssets+=$path;continue }
        if ($frameworkIdentities.ContainsKey($identity.FullName)) {
            $shared[$identity.FullName]=$frameworkIdentities[$identity.FullName];continue
        }
        $resolved=$resolver.ResolveAssemblyToPath($identity)
        if ([string]::IsNullOrEmpty($resolved)) { throw ('STOP: no authenticated component asset selected for '+$identity.FullName) }
        $resolved=[IO.Path]::GetFullPath($resolved)
        if (!$runtimeFiles.ContainsKey($resolved) -or (Hash $resolved) -ne $runtimeFiles[$resolved]) { throw ('STOP: component resolver selected unauthenticated asset: '+$resolved) }
        $actualIdentity=[Reflection.AssemblyName]::GetAssemblyName($resolved)
        if ($selected.ContainsKey($actualIdentity.FullName) -and $selected[$actualIdentity.FullName] -ne $resolved) { throw ('STOP: ambiguous selected identity: '+$actualIdentity.FullName) }
        $selected[$actualIdentity.FullName]=$resolved
    }
    $loaded=@{}
    foreach ($identity in $selected.Keys) {
        $assembly=$context.LoadFromAssemblyPath($selected[$identity])
        if ($assembly.FullName -ne $identity -or [Runtime.Loader.AssemblyLoadContext]::GetLoadContext($assembly) -ne $context -or (Hash $assembly.Location) -ne $runtimeFiles[$selected[$identity]]) { throw ('STOP: isolated assembly identity/location/hash mismatch: '+$identity) }
        $loaded[$identity]=$assembly
    }
    $receipt.selected_managed_assets=$selected;$receipt.shared_framework_identities=$shared
    $receipt.native_assets_not_explicitly_resolved=$nativeAssets
    $receipt.assembly_load_context=$context.Name
    $prodAssembly=$loaded[([Reflection.AssemblyName]::GetAssemblyName($production)).FullName]
    $benchAssembly=$loaded[([Reflection.AssemblyName]::GetAssemblyName($benchmark)).FullName]
    $tensor=$loaded[([Reflection.AssemblyName]::GetAssemblyName((Join-Path $runtime 'AiDotNet.Tensors.dll'))).FullName]
    if ($null -eq $prodAssembly -or $null -eq $benchAssembly -or $null -eq $tensor) { throw 'STOP: required canonical assemblies were not isolated.' }
    $flags=[Reflection.BindingFlags]'Public,NonPublic,Static,Instance'
    $degree=$tensor.GetType('AiDotNet.Tensors.Helpers.CpuParallelSettings',$true).GetProperty('MaxDegreeOfParallelism',$flags)
    $receipt.worker_policy_before_setup=$degree.GetValue($null)
    $workerMethod=$prodAssembly.GetType('OoplesFinance.StockIndicators.Indicators.ValuesBarExecution',$true).GetMethod('WorkerCount',$flags)
    function ReadEffectiveWorkers {
        if ($null -eq $workerMethod) { return @{available=$false} }
        return @{available=$true;maximum_argument=8;value=$workerMethod.Invoke($null,[object[]]@([int]8))}
    }
    $receipt.effective_workers_before_setup=ReadEffectiveWorkers
    function CheckLoadedClosure {
        $closure=@()
        foreach ($assembly in $context.Assemblies) {
            if ($assembly.IsDynamic) { continue }
            if (!$selected.ContainsKey($assembly.FullName)) { throw ('STOP: unselected isolated dependency: '+$assembly.FullName) }
            $expected=$selected[$assembly.FullName]
            if ([IO.Path]::GetFullPath($assembly.Location) -ne $expected -or (Hash $assembly.Location) -ne $runtimeFiles[$expected]) { throw ('STOP: isolated dependency changed: '+$assembly.FullName) }
            $closure+=@{name=$assembly.FullName;location=$assembly.Location;sha256=(Hash $assembly.Location)}
        }
        $defaultBcl=@()
        foreach ($assembly in [Runtime.Loader.AssemblyLoadContext]::Default.Assemblies) {
            if (!$assembly.IsDynamic -and $frameworkIdentities.ContainsKey($assembly.FullName)) {
                $defaultBcl+=@{name=$assembly.FullName;location=$assembly.Location;sha256=(Hash $assembly.Location)}
            }
        }
        $receipt.actual_default_framework_assemblies=$defaultBcl
        return $closure
    }
    $type=$benchAssembly.GetType('OoplesFinance.StockIndicators.CompetitorBenchmarks.LibraryPairBenchmarks',$true)
    $fixture=[Activator]::CreateInstance($type)
    $type.GetProperty('PairId').SetValue($fixture,'TaLib.Candles.DarkCloudCover')
    $type.GetProperty('Bars').SetValue($fixture,10000)
    $setup=$type.GetMethod('Setup');$invoke=$type.GetMethod('Ooples')
    if ($null -eq $setup -or $null -eq $invoke) { throw 'STOP: existing fixture replay methods unavailable.' }
    $receipt.stage='fixture-setup';SaveReceipt
    $null=$setup.Invoke($fixture,$null)
    $receipt.worker_policy_after_setup=$degree.GetValue($null)
    $receipt.effective_workers_after_setup=ReadEffectiveWorkers
    $receipt.loaded_closure_after_setup=@(CheckLoadedClosure)
    $receipt.stage='warmup';SaveReceipt
    for ($i=0;$i -lt 100;$i++) { $last=$invoke.Invoke($fixture,$null) }
    $receipt.stage='profile-ready';SaveReceipt
    Write-Output "PROFILE START DarkCloudCover PID=$PID source=$source fixture=LibraryPairBenchmarks arm=Ooples bars=10000"
    $receipt.profile_start_utc=[DateTime]::UtcNow.ToString('O')
    $receipt.profile_start_timestamp=[Diagnostics.Stopwatch]::GetTimestamp()
    $receipt.timestamp_frequency=[Diagnostics.Stopwatch]::Frequency
    $timer=[Diagnostics.Stopwatch]::StartNew();$iterations=0
    do { $last=$invoke.Invoke($fixture,$null);$iterations++ } while ($timer.Elapsed.TotalSeconds -lt 10)
    $receipt.profile_end_utc=[DateTime]::UtcNow.ToString('O')
    $receipt.profile_end_timestamp=[Diagnostics.Stopwatch]::GetTimestamp()
    $receipt.loaded_closure_after_profile=@(CheckLoadedClosure)
    $receipt.iterations=$iterations
    $receipt.worker_policy_after_profile=$degree.GetValue($null)
    $receipt.effective_workers_after_profile=ReadEffectiveWorkers
    Write-Output "PROFILE END DarkCloudCover PID=$PID iterations=$iterations"
    [GC]::KeepAlive($last)
    foreach ($entry in $runtimeFiles.GetEnumerator()) { if ((Hash $entry.Key) -ne $entry.Value) { throw 'STOP: protected runtime changed during replay.' } }
    foreach ($entry in $protectedInputs.GetEnumerator()) { if ((Hash $entry.Key) -ne $entry.Value) { throw 'STOP: protected source inputs changed during replay.' } }
    $receipt.protected_inputs_unchanged=$true
    $receipt.runtime_unchanged=$true;$receipt.stage='complete';$receipt.status='complete'
    SaveReceipt
} catch {
    $receipt.status='failed';$receipt.error=$_.Exception.ToString();SaveReceipt
    throw
}
