param([Parameter(Mandatory=$true)][string]$Output,[Parameter(Mandatory=$true)][string]$Runtime,[Parameter(Mandatory=$true)][string]$Expected)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$taskRoot=[IO.Path]::GetFullPath($Output)
$taskRuntime=[IO.Path]::GetFullPath($Runtime)
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
        'OoplesFinance.StockIndicators.CompetitorBenchmarks.dll'='933e0694594e08f663d27e31d0e174f267ad5471c9206a91e4f21be39e6aafe0'
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
    $taskExpectedContexts=Get-Content -LiteralPath $Expected -Raw|ConvertFrom-Json -AsHashtable
    $taskRecords=@()
    foreach($Pair in @('QuanTAlib.Kama','Skender.GetKama','TaLib.Functions.Kama','Trady.Indicator.KaufmanAdaptiveMovingAverage')) {
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
    $taskCall=$taskFixtureType.GetMethod('Ooples')
    $taskResult=$taskCall.Invoke($taskFixture,$null)
    $taskOutputHash=Result-Hash $taskResult
    if($taskOutputHash -ne $taskReferenceHash -or $taskPresenceHash -ne $taskReferencePresence) { throw 'Common-harness returned outputs/presence differ from independent reference' }
    $taskBinding=$taskExpectedContexts[$Pair].ready
    if ($taskFactoryParameters -ne $taskBinding.actual_indicator_parameters -or $taskFactory -ne $taskBinding.actual_factory) { throw 'Factory binding differs from retained worker proof' }
    foreach($taskName in @('Opens','Highs','Lows','Closes','Volumes')) {
        if($taskDataHashes[$taskName] -ne $taskBinding.actual_data_bits[$taskName]) { throw 'Common-harness fixture differs from retained model/proof' }
    }
    $taskOutputMap=$taskOutputHash|ConvertFrom-Json -AsHashtable
    $taskPresenceMap=$taskPresenceHash|ConvertFrom-Json -AsHashtable
    $taskBoundOutputs=$taskBinding.full_returned_output_hash|ConvertFrom-Json -AsHashtable
    $taskBoundPresence=$taskBinding.presence_sha256|ConvertFrom-Json -AsHashtable
    foreach($taskName in $taskOutputMap.Keys) {
        if($taskOutputMap[$taskName] -ne $taskBoundOutputs[$taskName] -or $taskPresenceMap[$taskName] -ne $taskBoundPresence[$taskName]) { throw 'Common-harness output differs from retained model/proof' }
    }
    if([int]$taskWorkerProperty.GetValue($null) -ne $taskWorkers) { throw 'Adapter altered default worker policy' }
    $taskRecords+=@{pair=$Pair;bars=10000;period=$taskPeriod;factory=$taskFactory;parameters=$taskFactoryParameters;
        actual_data_bits=$taskDataHashes;full_returned_output_hash=$taskOutputMap;presence_sha256=$taskPresenceMap}
    }
    Write-AtomicJson 'bridge.json' @{
        status='common-harness-four-adapter-preflight-passed';source='7de04493efa8304cb2a21fea45c21d177ca0903f'
        actual_clr=[Environment]::Version.ToString();core_sha256=File-Hash $taskCore.Location;harness_sha256=File-Hash $taskBench.Location
        cpu_engine=$taskEngine.GetType().FullName;default_backend=$taskBackend;default_history=$taskHistory;default_workers=$taskWorkers
        setup_calls=4;explicit_ooples_calls=4;builds=0;profiles=0;warmups=0;timings=0;qualification=$false;performance_claim=$false;contexts=$taskRecords
    }
}
finally { if ($null -ne $taskResolver) { $taskContext.remove_Resolving($taskResolver) } }
