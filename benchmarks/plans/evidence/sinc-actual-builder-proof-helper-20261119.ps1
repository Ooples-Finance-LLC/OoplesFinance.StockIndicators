param([string]$Runtime,[string]$ExpectedCore,[string]$ExpectedHarness,[string]$Out)
$ErrorActionPreference = 'Stop'
if (Test-Path -LiteralPath $Out) { throw 'Fresh proof output only' }
if ([Environment]::Version.ToString() -ne '10.0.15' -or $env:AIDOTNET_DISABLE_GPU -ne '1') { throw 'Wrong CPU proof bootstrap' }
$core = Join-Path $Runtime 'OoplesFinance.StockIndicators.dll'
$harness = Join-Path $Runtime 'OoplesFinance.StockIndicators.CompetitorBenchmarks.dll'
if ((Get-FileHash -LiteralPath $core).Hash.ToLowerInvariant() -ne $ExpectedCore -or (Get-FileHash -LiteralPath $harness).Hash.ToLowerInvariant() -ne $ExpectedHarness) { throw 'Binary pin mismatch' }
[System.Runtime.Loader.AssemblyLoadContext]::Default.add_Resolving([Func[System.Runtime.Loader.AssemblyLoadContext,System.Reflection.AssemblyName,System.Reflection.Assembly]]{
    param($context,$name)
    $path = Join-Path $Runtime ($name.Name + '.dll')
    if (Test-Path -LiteralPath $path) { return $context.LoadFromAssemblyPath($path) }
    return $null
})
$assembly = [Reflection.Assembly]::LoadFrom($core)
$benchmarks = [Reflection.Assembly]::LoadFrom($harness)
$tensor = [Reflection.Assembly]::LoadFrom((Join-Path $Runtime 'AiDotNet.Tensors.dll'))
$flags = [Reflection.BindingFlags]'Public,NonPublic,Instance,Static'
$engine = $tensor.GetType('AiDotNet.Tensors.Engines.AiDotNetEngine',$true).GetProperty('Current',$flags).GetValue($null)
if ($engine.GetType().FullName -ne 'AiDotNet.Tensors.Engines.CpuEngine') { throw 'Actual engine must be CPU' }
$workers = $tensor.GetType('AiDotNet.Tensors.Helpers.CpuParallelSettings',$true).GetProperty('MaxDegreeOfParallelism',$flags)
$priorDop = [int]$workers.GetValue($null)
function BitsHash([double[]]$values) {
    $bytes = [byte[]]::new($values.Length * 8)
    [Buffer]::BlockCopy($values,0,$bytes,0,$bytes.Length)
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
}
try {
    $factory = $benchmarks.GetType('OoplesFinance.StockIndicators.CompetitorBenchmarks.FixedKernelBenchmarkInput',$true)
    $arguments = [object[]]::new(4)
    $arguments[0] = 10000; $arguments[3] = 0
    $values = [double[]]$factory.GetMethod('Create',$flags).Invoke($null,$arguments)
    $bars = $arguments[1]
    if ($values.Length -ne 10000 -or $bars.Length -ne 10000 -or [int]$workers.GetValue($null) -ne 8) { throw 'Wrong actual fixture' }
    $inputHash = BitsHash $values
    if ($inputHash -ne '814ad816b7db39c23240d4999f9b4dc6f793f184af08ddb56f2a415af40c757f') { throw 'Fixture bits changed' }
    $sincType = $assembly.GetType('OoplesFinance.StockIndicators.Indicators.WindowedSinc',$true)
    $windowType = $assembly.GetType('OoplesFinance.StockIndicators.Indicators.SincWindow',$true)
    $indicator = [Activator]::CreateInstance($sincType,[object[]]@(20,21,[Enum]::Parse($windowType,'Hann')))
    $original = $sincType.GetMethod('CreateState',$flags).Invoke($indicator,$null)
    $update = $original.GetType().GetMethod('Update',$flags)
    $expected = [double[]]::new(10000)
    $one = [object[]]::new(1)
    for ($row = 0; $row -lt 10000; $row++) {
        $one[0] = $bars.GetValue($row)
        $expected[$row] = [double]$update.Invoke($original,$one)
    }
    $expectedHash = BitsHash $expected

    # Source-bound eligibility of each actual worker region, separate from the
    # two actual builder calls below. This does not claim an observed call stack.
    $execution = $assembly.GetType('OoplesFinance.StockIndicators.Indicators.ValuesBarExecution',$true)
    $targetType = $execution.GetNestedType('BoundedOutput',[Reflection.BindingFlags]::NonPublic)
    $kernelType = $execution.GetNestedType('StateWindowKernel',[Reflection.BindingFlags]::NonPublic)
    $actualChunks = [int]$execution.GetMethod('WorkerCount',$flags).Invoke($null,[object[]]@(8))
    if ($actualChunks -lt 1 -or $actualChunks -gt [Math]::Min(8,[Environment]::ProcessorCount)) { throw 'Wrong actual worker clamp' }
    $regions = [Collections.Generic.List[object]]::new()
    $columns = [Array]::CreateInstance([double[]],1)
    $columns.SetValue([double[]]::new(10000),0)
    for ($chunk = 0; $chunk -lt $actualChunks; $chunk++) {
        $start = [int][Math]::Floor(10000 * $chunk / [double]$actualChunks)
        $end = [int][Math]::Floor(10000 * ($chunk + 1) / [double]$actualChunks)
        $origin = [Math]::Max(0,$start - 20); $count = $end - $origin
        $targetArgs = [object[]]::new(3)
        $targetArgs[0] = $columns; $targetArgs[1] = $origin; $targetArgs[2] = $start
        $target = [Activator]::CreateInstance($targetType,$flags,$null,$targetArgs,$null)
        $kernelArgs = [object[]]::new(4)
        $kernelArgs[0] = 21; $kernelArgs[1] = $count; $kernelArgs[2] = $indicator; $kernelArgs[3] = $target
        $kernel = $kernelType.GetMethod('Create',$flags).Invoke($null,$kernelArgs)
        $fields = @($kernelType.GetFields($flags) | Where-Object { $_.FieldType.Name -eq 'IIndicatorState' })
        if ($fields.Count -ne 1) { throw 'Unexpected kernel state binding' }
        $state = $fields[0].GetValue($kernel)
        if ($state.GetType().Name -ne 'SincGridBatchState') { throw 'Funded region did not select wrapper' }
        for ($row = $origin; $row -lt $origin + 21; $row++) {
            $one[0] = $bars.GetValue($row)
            $null = $state.GetType().GetMethod('Update',$flags).Invoke($state,$one)
        }
        $batch = $state.GetType().GetField('_batch',$flags).GetValue($state)
        if ($null -eq $batch -or !$batch.GetType().GetProperty('HasCertifiedPositiveMass',$flags).GetValue($batch)) { throw 'Actual coefficient certificate rejected' }
        $batchTarget = $batch.GetType().GetField('_target',$flags).GetValue($batch)
        $batchOrigin = [int]$batchTarget.GetType().GetProperty('Origin',$flags).GetValue($batchTarget)
        $writeStart = [int]$batchTarget.GetType().GetProperty('WriteStart',$flags).GetValue($batchTarget)
        $batchCount = [int]$batch.GetType().GetField('_count',$flags).GetValue($batch)
        $seeded = [int]$batch.GetType().GetField('_received',$flags).GetValue($batch)
        if ($batchOrigin -ne $origin + 1 -or $writeStart -ne [Math]::Max($start,$origin + 21) -or $batchCount -ne $count - 1 -or $seeded -ne 20) { throw 'Wrong actual handoff' }
        $regions.Add(@{start=$start;end=$end;origin=$origin;count=$count;batch_origin=$batchOrigin;batch_count=$batchCount;write_start=$writeStart;seeded=$seeded;certified=$true})
    }
    $benchmarkType = $benchmarks.GetType('OoplesFinance.StockIndicators.CompetitorBenchmarks.SharedWindowedSincBenchmarks',$true)
    $benchmark = [Activator]::CreateInstance($benchmarkType)
    $benchmarkType.GetProperty('Count',$flags).SetValue($benchmark,10000)
    $benchmarkType.GetProperty('Operation',$flags).SetValue($benchmark,'QuanTAlib.Afirma')
    $benchmarkType.GetField('_bars',$flags).SetValue($benchmark,$bars)
    $benchmarkType.GetField('_input',$flags).SetValue($benchmark,$arguments[2])
    $benchmarkType.GetField('_priorDop',$flags).SetValue($benchmark,$priorDop)
    $calls = [Collections.Generic.List[object]]::new()
    foreach ($method in @('BuilderFull','BuilderLatestOnly')) {
        $run = $benchmarkType.GetMethod($method,$flags).Invoke($benchmark,$null)
        try {
            if ($run.BarCount -ne 10000 -or !$run.Latest.Bar.Equals($bars.GetValue(9999))) { throw 'Wrong count or latest ownership' }
            $runType = $run.GetType()
            Write-Output ('Actual builder returned ' + $method + ' as ' + $runType.FullName)
            if ($method -eq 'BuilderFull' -and $runType.Name -ne 'IndicatorRun') { throw 'Wrong Full run type' }
            if ($method -eq 'BuilderLatestOnly' -and $runType.Name -notin @('LatestOnlyIndicatorRun','ScalarLatestOnlyIndicatorRun')) { throw 'Wrong LatestOnly run type' }
            # IIndicatorRun exposes its column as ReadOnlySpan<double>, which
            # reflection cannot box. Read the declared owning array/dictionary
            # by type, including primary-constructor capture fields.
            $storage = @($runType.GetFields([Reflection.BindingFlags]'Public,NonPublic,Instance,DeclaredOnly'))
            if ($runType.Name -eq 'ScalarLatestOnlyIndicatorRun') {
                $arrays = @($storage | Where-Object { $_.FieldType -eq [double[]] })
                if ($arrays.Count -ne 1) { throw 'Unexpected scalar column storage' }
                $actual = [double[]]$arrays[0].GetValue($run)
            } else {
                $dictionaries = @($storage | Where-Object {
                    $_.FieldType.IsGenericType -and $_.FieldType.GetGenericTypeDefinition().FullName -eq 'System.Collections.Generic.Dictionary`2' -and $_.FieldType.GetGenericArguments()[1] -eq [double[]]
                })
                if ($dictionaries.Count -ne 1) { throw 'Unexpected dictionary column storage' }
                $series = $dictionaries[0].GetValue($run)
                $enumerator = $series.GetEnumerator()
                if (!$enumerator.MoveNext()) { throw 'Missing output column' }
                $actual = [double[]]$enumerator.Current.Value
                if ($enumerator.MoveNext()) { throw 'Unexpected extra output column' }
            }
            if ($actual.Length -ne 10000 -or (BitsHash $actual) -ne $expectedHash) { throw 'Actual builder output bits differ' }
            if ($method -eq 'BuilderFull' -and $run.GetType().GetProperty('HasLegacyRuntime',$flags).GetValue($run)) { throw 'Builder used ordinary runtime' }
            $calls.Add(@{method=$method;bar_count=$run.BarCount;run_type=$run.GetType().FullName;output_sha256=(BitsHash $actual)})
        } finally { $run.Dispose() }
    }
    if ((Get-FileHash -LiteralPath $core).Hash.ToLowerInvariant() -ne $ExpectedCore -or (Get-FileHash -LiteralPath $harness).Hash.ToLowerInvariant() -ne $ExpectedHarness) { throw 'Binary pin changed' }
    $receipt = @{status='actual-builder-correctness-proof-only';core_sha256=$ExpectedCore;harness_sha256=$ExpectedHarness;fixture_hash=$inputHash;fixture_dop=8;actual_worker_chunks=$actualChunks;period=20;taps=21;window='Hann';reference='unchanged original CompactState';reference_output_sha256=$expectedHash;region_eligibility=$regions;calls=$calls;setup_calls=0;oopless_calls=0;factory_calls=1;builder_calls=2;local_builds=0;profiles=0;timings=0}
} finally { $workers.SetValue($null,$priorDop) }
if ([int]$workers.GetValue($null) -ne $priorDop) { throw 'DOP restoration failed' }
[IO.File]::WriteAllText($Out,($receipt | ConvertTo-Json -Depth 12))
