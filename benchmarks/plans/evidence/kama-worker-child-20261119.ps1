param([Parameter(Mandatory=$true)][string]$Output, [Parameter(Mandatory=$true)][ValidateSet('QuanTAlib.Kama','Skender.GetKama','TaLib.Functions.Kama','Trady.Indicator.KaufmanAdaptiveMovingAverage')][string]$Pair)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$taskRoot=[IO.Path]::GetFullPath($Output)
$taskRuntime='C:\Users\cheat\source\repos\Ooples-Finance-LLC\si-kama-combined-grid-gpu-only-20261119\benchmarks\OoplesFinance.StockIndicators.CompetitorBenchmarks\bin\Release\net10.0'
$taskFlags=[Reflection.BindingFlags]'Public,NonPublic,Static,Instance'
$taskResolver=$null
function Write-AtomicJson([string]$Name,$Data) {
    $taskFinal=Join-Path $taskRoot $Name
    $taskPartial=$taskFinal+'.partial'
    $taskBytes=[Text.UTF8Encoding]::new($false).GetBytes(($Data|ConvertTo-Json -Depth 9))
    $taskStream=[IO.File]::Open($taskPartial,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
    try { $taskStream.Write($taskBytes,0,$taskBytes.Length); $taskStream.Flush($true) } finally { $taskStream.Dispose() }
    [IO.File]::Move($taskPartial,$taskFinal)
}
function File-Hash([string]$Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }
function Doubles-Hash([double[]]$Values) {
    $taskBytes=[byte[]]::new($Values.Length*8)
    [Buffer]::BlockCopy($Values,0,$taskBytes,0,$taskBytes.Length)
    $taskHasher=[Security.Cryptography.SHA256]::Create()
    try { [BitConverter]::ToString($taskHasher.ComputeHash($taskBytes)).Replace('-','').ToLowerInvariant() } finally { $taskHasher.Dispose() }
}
function Result-Hash($Result) {
    [string[]]$taskNames=@(if($Pair -eq 'Skender.GetKama') {'Kama';'ER'} else {'Kama'})
    if($Result.Outputs.Count -ne $taskNames.Count) { throw 'Returned column count changed' }
    $taskHashes=[ordered]@{};$taskPresenceHashes=[ordered]@{}
    foreach($taskName in $taskNames) {
        if(!$Result.Outputs.ContainsKey($taskName)) { throw 'Missing required returned column' }
        $taskColumn=$Result.Outputs[$taskName]
        if($taskColumn.Values.Length -ne 10000 -or $taskColumn.FirstValid -ne 0 -or $null -eq $taskColumn.Present -or $taskColumn.Present.Length -ne 10000) { throw 'Incomplete returned column/presence' }
        $taskFirst=if($Pair -eq 'QuanTAlib.Kama'){0}elseif($taskName -eq 'ER' -or $Pair -eq 'TaLib.Functions.Kama'){20}else{19}
        for($taskRow=0;$taskRow -lt 10000;$taskRow++) {
            if($taskColumn.Present[$taskRow] -ne ($taskRow -ge $taskFirst)) { throw 'Unexpected actual presence/alignment' }
            if($taskRow -lt $taskFirst -and ![double]::IsNaN($taskColumn.Values[$taskRow])) { throw 'Absent result is not NaN' }
        }
        $taskPresence=[byte[]]::new(10000);[Buffer]::BlockCopy([bool[]]$taskColumn.Present,0,$taskPresence,0,10000)
        $taskPresenceHashes[$taskName]=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($taskPresence)).ToLowerInvariant()
        $taskHashes[$taskName]=Doubles-Hash $taskColumn.Values
    }
    $script:taskPresenceHash=$taskPresenceHashes|ConvertTo-Json -Compress
    $taskHashes|ConvertTo-Json -Compress
}
try {
    if ([Environment]::Version.ToString() -ne '10.0.15' -or $env:AIDOTNET_DISABLE_GPU -ne '1') { throw 'Wrong CLR or CPU diagnostic bootstrap' }
    if (!(Test-Path -LiteralPath $taskRoot -PathType Container)) { throw 'Parent must create fresh bounded output first' }
    $taskPins=@{
        'OoplesFinance.StockIndicators.dll'='34f5f2f8f01e71a284011378915c2f5a4e4a515063d1cb8e8822bac2e1412d3c'
        'OoplesFinance.StockIndicators.CompetitorBenchmarks.dll'='25a4b8834ea5a0334ff4cdc61d10e87048b57e05c28c32c9dfe22f97d8cf9497'
    }
    foreach($taskName in $taskPins.Keys) { if ((File-Hash (Join-Path $taskRuntime $taskName)) -ne $taskPins[$taskName]) { throw 'Wrong source-bound assembly' } }
    # Resolve only siblings declared in the parent's authenticated 100-file closure.
    $taskDeclared=(Get-Content -LiteralPath (Join-Path $taskRoot 'invocation.json') -Raw|ConvertFrom-Json -AsHashtable).runtime_file_pins
    $taskContext=[Runtime.Loader.AssemblyLoadContext]::Default
    $taskResolver=[Func[Runtime.Loader.AssemblyLoadContext,Reflection.AssemblyName,Reflection.Assembly]] {
        param($context,$name)
        $taskSibling=$name.Name+'.dll'
        if ($taskDeclared.ContainsKey($taskSibling)) { return $context.LoadFromAssemblyPath((Join-Path $taskRuntime $taskSibling)) }
        return $null
    }
    $taskContext.add_Resolving($taskResolver)
    $taskCore=$taskContext.LoadFromAssemblyPath((Join-Path $taskRuntime 'OoplesFinance.StockIndicators.dll'))
    $taskBench=$taskContext.LoadFromAssemblyPath((Join-Path $taskRuntime 'OoplesFinance.StockIndicators.CompetitorBenchmarks.dll'))
    $taskTensor=$taskContext.LoadFromAssemblyPath((Join-Path $taskRuntime 'AiDotNet.Tensors.dll'))
    $taskEngine=$taskTensor.GetType('AiDotNet.Tensors.Engines.AiDotNetEngine',$true).GetProperty('Current',$taskFlags).GetValue($null)
    if ($taskEngine.GetType().FullName -ne 'AiDotNet.Tensors.Engines.CpuEngine') { throw 'Expected hosted-equivalent CPU package engine' }
    $taskWorkerProperty=$taskTensor.GetType('AiDotNet.Tensors.Helpers.CpuParallelSettings',$true).GetProperty('MaxDegreeOfParallelism',$taskFlags)
    $taskWorkers=[int]$taskWorkerProperty.GetValue($null)
    $taskBuilderType=$taskCore.GetType('OoplesFinance.StockIndicators.Builder.StockIndicatorBuilder',$true)
    $taskDefaultBuilder=[Activator]::CreateInstance($taskBuilderType)
    $taskBackend=$taskBuilderType.GetField('_executionBackend',$taskFlags).GetValue($taskDefaultBuilder).ToString()
    $taskHistory=$taskBuilderType.GetField('_historyMode',$taskFlags).GetValue($taskDefaultBuilder).ToString()
    if ($taskBackend -ne 'Auto' -or $taskHistory -ne 'Full') { throw 'Actual default CPU/Full path differs' }
    $taskFixtureType=$taskBench.GetType('OoplesFinance.StockIndicators.CompetitorBenchmarks.LibraryPairBenchmarks',$true)
    $taskFixture=[Activator]::CreateInstance($taskFixtureType)
    $taskFixtureType.GetProperty('PairId').SetValue($taskFixture,$Pair)
    $taskFixtureType.GetProperty('Bars').SetValue($taskFixture,10000)
    $taskPeriod=[int]$taskFixtureType.GetField('Period',$taskFlags).GetRawConstantValue()
    if ($taskPeriod -ne 20) { throw 'Wrong actual LP period' }
    [void]$taskFixtureType.GetMethod('Setup').Invoke($taskFixture,$null)
    if ([int]$taskWorkerProperty.GetValue($null) -ne $taskWorkers) { throw 'Actual Setup changed default worker policy' }
    $taskPair=$taskFixtureType.GetField('_pair',$taskFlags).GetValue($taskFixture)
    if ($taskPair.GetType().GetProperty('IsCandle',$taskFlags).GetValue($taskPair)) { throw 'Analytic pair unexpectedly selected candle fixture' }
    $taskData=$taskFixtureType.GetField('_data',$taskFlags).GetValue($taskFixture)
    $taskDataType=$taskBench.GetType('OoplesFinance.StockIndicators.CompetitorBenchmarks.CompetitorData',$true)
    $taskVerifier=$taskBench.GetType('OoplesFinance.StockIndicators.CompetitorBenchmarks.ComparisonVerifier',$true)
    $taskExpected=if($Pair -eq 'Trady.Indicator.KaufmanAdaptiveMovingAverage') {$taskDataType.GetMethod('Create',$taskFlags).Invoke($null,[object[]]@(10000,42))} else {$taskVerifier.GetMethod('Fixture',$taskFlags).Invoke($null,[object[]]@('walk',10000))}
    $taskFactory=if($Pair -eq 'Trady.Indicator.KaufmanAdaptiveMovingAverage') {'CompetitorData.Create(10000,42)'} else {'ComparisonVerifier.Fixture(walk,10000), Random731'}
    $taskDataHashes=@{}
    foreach($taskName in @('Opens','Highs','Lows','Closes','Volumes')) {
        $taskActualArray=[double[]]$taskDataType.GetProperty($taskName).GetValue($taskData)
        $taskExpectedArray=[double[]]$taskDataType.GetProperty($taskName).GetValue($taskExpected)
        $taskDataHashes[$taskName]=Doubles-Hash $taskActualArray
        if ($taskActualArray.Length -ne 10000 -or $taskDataHashes[$taskName] -ne (Doubles-Hash $taskExpectedArray)) { throw 'Actual LP Setup data differs from source-bound walk fixture' }
    }
    $taskIndicatorName='SeededAdaptiveAverage'
    if ($taskPair.GetType().GetProperty('Indicator',$taskFlags).GetValue($taskPair) -ne $taskIndicatorName) { throw 'Wrong actual analytic indicator' }
    $taskLibrary=$taskPair.GetType().GetProperty('Library',$taskFlags).GetValue($taskPair)
    $taskClosure=$taskLibrary.Target
    $taskClosureExpected=if($Pair -eq 'TaLib.Functions.Kama') {@{suppression=0}} elseif($Pair -eq 'Trady.Indicator.KaufmanAdaptiveMovingAverage') {@{fast=2;slow=30}} else {@{fast=2;slow=30;quan=($Pair -eq 'QuanTAlib.Kama')}}
    foreach($taskParameter in $taskClosureExpected.Keys) {
        $taskField=$taskClosure.GetType().GetField($taskParameter,$taskFlags)
        if($null -eq $taskField -or $taskField.GetValue($taskClosure) -ne $taskClosureExpected[$taskParameter]) { throw 'Actual registered factory closure parameter changed' }
    }
    # Independent exact source-bound reference uses actual registered factory parameters.
    $taskReference=$taskPair.GetType().GetProperty('Reference',$taskFlags).GetValue($taskPair)
    if($null -eq $taskReference) { throw 'No independent exact reference' }
    $taskReferenceResult=$taskReference.DynamicInvoke([object[]]@($taskData,$taskPeriod))
    $taskReferenceHash=Result-Hash $taskReferenceResult
    $taskReferencePresence=$taskPresenceHash
    $taskFactoryParameters=switch($Pair) {
        'QuanTAlib.Kama' {'period20;fast2;slow30;publishStartup=true;clampFast=true;resetFlat=false;fastFlat=false;outputDelay=0'}
        'Skender.GetKama' {'period20;fast2;slow30;publishStartup=false;clampFast=false;resetFlat=true;fastFlat=false;outputDelay=0'}
        'TaLib.Functions.Kama' {'period20;fast2;slow30;publishStartup=false;clampFast=false;resetFlat=false;fastFlat=true;outputDelay=1;suppression=0'}
        'Trady.Indicator.KaufmanAdaptiveMovingAverage' {'period20;fast2;slow30;publishStartup=false;clampFast=false;resetFlat=false;fastFlat=false;outputDelay=0'}
    }
    # Read compiled method identity only; never invoke a private kernel directly.
    $taskBatchType=$taskCore.GetType('OoplesFinance.StockIndicators.Indicators.ValuesBarExecution+SeededAdaptiveValueState',$true)
    $taskBatchMethod=$taskBatchType.GetMethod('FillBatch',$taskFlags)
    $taskRateMethods=@(foreach($taskType in $taskCore.GetTypes()|Where-Object{$_.FullName -like '*SeededAdaptiveValueState*'}) {
        foreach($taskMethod in $taskType.GetMethods($taskFlags)|Where-Object{$_.Name -like '<FillBatch>b__*'}) {
            $taskArguments=$taskMethod.GetParameters()
            if($taskArguments.Count -eq 1 -and $taskArguments[0].ParameterType -eq [int] -and $taskMethod.ReturnType -eq [void]) {$taskMethod}
        }
    })
    if($null -eq $taskBatchMethod -or $taskRateMethods.Count -ne 1) {throw 'Expected compiled batch/rate-worker methods missing'}
    $taskCompiled=@{}
    foreach($taskMethod in @($taskBatchMethod,$taskRateMethods[0])) {
        $taskIL=$taskMethod.GetMethodBody().GetILAsByteArray()
        $taskCompiled[$taskMethod.Name]=@{type=$taskMethod.DeclaringType.FullName;signature=$taskMethod.ToString();il_bytes=$taskIL.Length;il_sha256=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($taskIL)).ToLowerInvariant()}
    }
    $taskCall=$taskFixtureType.GetMethod('Ooples')
    $taskResult=$null
    for($taskIndex=0;$taskIndex -lt 1000;$taskIndex++) { $taskResult=$taskCall.Invoke($taskFixture,$null) }
    $taskOutputHash=Result-Hash $taskResult
    if($taskOutputHash -ne $taskReferenceHash -or $taskPresenceHash -ne $taskReferencePresence) { throw 'Complete outputs/presence differ from independently evaluated exact registered reference' }
    $taskExpectedPresenceHash=$taskPresenceHash
    $taskLoaded=@{}
    foreach($taskAssembly in @($taskCore,$taskBench,$taskTensor)) { $taskLoaded[$taskAssembly.GetName().Name]=@{path=$taskAssembly.Location;sha256=File-Hash $taskAssembly.Location} }
    if ($taskLoaded['OoplesFinance.StockIndicators'].sha256 -ne $taskPins['OoplesFinance.StockIndicators.dll'] -or $taskLoaded['OoplesFinance.StockIndicators.CompetitorBenchmarks'].sha256 -ne $taskPins['OoplesFinance.StockIndicators.CompetitorBenchmarks.dll']) { throw 'Loaded source differs from requested runtime' }
    Write-AtomicJson 'ready.json' @{
        status='ready';pid=$PID;source='7de04493efa8304cb2a21fea45c21d177ca0903f';pair=$Pair;bars=10000
        actual_clr=[Environment]::Version.ToString();cpu_engine=$taskEngine.GetType().FullName;loaded_modules=$taskLoaded
        default_backend=$taskBackend;default_history=$taskHistory;default_workers=$taskWorkers;logical_processors=[Environment]::ProcessorCount
        lp_period=$taskPeriod;actual_indicator_parameters=$taskFactoryParameters;independent_reference_output_hash=$taskReferenceHash;independent_reference_presence_hash=$taskReferencePresence;actual_factory=$taskFactory;compiled_batch_rate_methods=$taskCompiled;actual_data_bits=$taskDataHashes
        warmups=1000;full_returned_output_hash=$taskOutputHash;presence_sha256=$taskPresenceHash;utc=[DateTime]::UtcNow.ToString('O');qpc=[Diagnostics.Stopwatch]::GetTimestamp()
        source_compilation=$false;qualification=$false;performance_claim=$false
    }
    $taskWait=[Diagnostics.Stopwatch]::StartNew()
    while (!(Test-Path -LiteralPath (Join-Path $taskRoot 'go.json'))) {
        if ((Test-Path -LiteralPath (Join-Path $taskRoot 'abort.json')) -or $taskWait.Elapsed.TotalSeconds -gt 20) { throw 'No parent attach authorization within cap' }
        Start-Sleep -Milliseconds 20
    }
    $taskGo=Get-Content -LiteralPath (Join-Path $taskRoot 'go.json') -Raw|ConvertFrom-Json
    if ($taskGo.target_pid -ne $PID -or $taskGo.hot_seconds -ne 2) { throw 'Wrong parent attach authorization' }
    $taskBefore=[GC]::GetTotalAllocatedBytes($true)
    $taskGcBefore=@([GC]::CollectionCount(0),[GC]::CollectionCount(1),[GC]::CollectionCount(2))
    $taskStartUtc=[DateTime]::UtcNow.ToString('O');$taskStartQpc=[Diagnostics.Stopwatch]::GetTimestamp()
    [Console]::WriteLine('HOT START PID='+$PID+' UTC='+$taskStartUtc);[Console]::Out.Flush()
    $taskClock=[Diagnostics.Stopwatch]::StartNew();$taskCalls=0
    do { $taskResult=$taskCall.Invoke($taskFixture,$null);$taskCalls++ } while ($taskClock.Elapsed.TotalSeconds -lt 2)
    $taskEndQpc=[Diagnostics.Stopwatch]::GetTimestamp();$taskEndUtc=[DateTime]::UtcNow.ToString('O')
    $taskAllocated=[GC]::GetTotalAllocatedBytes($true)-$taskBefore
    $taskCollections=@(([GC]::CollectionCount(0)-$taskGcBefore[0]),([GC]::CollectionCount(1)-$taskGcBefore[1]),([GC]::CollectionCount(2)-$taskGcBefore[2]))
    [Console]::WriteLine('HOT END PID='+$PID+' UTC='+$taskEndUtc);[Console]::Out.Flush()
    if ([int]$taskWorkerProperty.GetValue($null) -ne $taskWorkers -or (Result-Hash $taskResult) -ne $taskOutputHash -or $taskPresenceHash -ne $taskExpectedPresenceHash) { throw 'Worker policy or complete returned output changed' }
    Write-AtomicJson 'replay.json' @{
        status='complete';pid=$PID;source='7de04493efa8304cb2a21fea45c21d177ca0903f';pair=$Pair;bars=10000
        hot_start_utc=$taskStartUtc;hot_end_utc=$taskEndUtc;start_qpc=$taskStartQpc;end_qpc=$taskEndQpc;qpc_frequency=[Diagnostics.Stopwatch]::Frequency
        full_returned_calls=$taskCalls;full_returned_output_hash=$taskOutputHash;presence_sha256=$taskPresenceHash;process_allocated_bytes=$taskAllocated;gc_collections=$taskCollections
        default_workers_before=$taskWorkers;default_workers_after=[int]$taskWorkerProperty.GetValue($null)
        qualification=$false;performance_claim=$false;limits='Reflection/PowerShell overhead is included. Counters and sampled stages are not latency savings or native timing.'
    }
    [GC]::KeepAlive($taskResult)
    $taskWait.Restart()
    while (!(Test-Path -LiteralPath (Join-Path $taskRoot 'release.json'))) {
        if ((Test-Path -LiteralPath (Join-Path $taskRoot 'abort.json')) -or $taskWait.Elapsed.TotalSeconds -gt 20) { throw 'Parent did not finish trace within finalization cap' }
        Start-Sleep -Milliseconds 20
    }
}
catch {
    if ((Test-Path -LiteralPath $taskRoot -PathType Container) -and !(Test-Path -LiteralPath (Join-Path $taskRoot 'child-failure.json'))) {
        Write-AtomicJson 'child-failure.json' @{status='incomplete';pid=$PID;error=$_.ToString();retry=$false;qualification=$false}
    }
    throw
}
finally {
    if ($null -ne $taskResolver) { $taskContext.remove_Resolving($taskResolver) }
}
