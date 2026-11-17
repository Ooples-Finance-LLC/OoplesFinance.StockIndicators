
param([string]$Runtime,[string]$ExpectedProduction,[string]$Operation,[int]$Workers,[string]$Receipt)
$ErrorActionPreference='Stop'
$hostProof=[ordered]@{runtime_version=[Environment]::Version.ToString();framework_description=[Runtime.InteropServices.RuntimeInformation]::FrameworkDescription;corelib_product=[Diagnostics.FileVersionInfo]::GetVersionInfo([object].Assembly.Location).ProductVersion;host_path=[Diagnostics.Process]::GetCurrentProcess().MainModule.FileName;pwsh_version=$PSVersionTable.PSVersion.ToString()}
if($hostProof.runtime_version -ne '10.0.15' -or $hostProof.framework_description -ne '.NET 10.0.15') {
 [IO.File]::WriteAllText($Receipt,([ordered]@{status='inconclusive-runtime-mismatch';host=$hostProof;qualification=$false;replay_executed=$false}|ConvertTo-Json -Depth 6));exit 0
}
if((Get-FileHash -LiteralPath (Join-Path $Runtime 'OoplesFinance.StockIndicators.dll') -Algorithm SHA256).Hash.ToLowerInvariant() -ne $ExpectedProduction){throw 'Wrong production DLL'}
$resolve=[Func[Runtime.Loader.AssemblyLoadContext,Reflection.AssemblyName,Reflection.Assembly]]{
 param($ctx,$name)
 $candidate=Join-Path $Runtime ($name.Name+'.dll')
 if(Test-Path -LiteralPath $candidate){return $ctx.LoadFromAssemblyPath($candidate)}
 return $null
}
[Runtime.Loader.AssemblyLoadContext]::Default.add_Resolving($resolve)
$flags=[Reflection.BindingFlags]'Public,NonPublic,Static,Instance'
$ns='OoplesFinance.StockIndicators.CompetitorBenchmarks.'
$fixture=$null;$policy=$null;$degree=$null;$prior=$null
try{
 $prod=[Runtime.Loader.AssemblyLoadContext]::Default.LoadFromAssemblyPath((Join-Path $Runtime 'OoplesFinance.StockIndicators.dll'))
 $bench=[Runtime.Loader.AssemblyLoadContext]::Default.LoadFromAssemblyPath((Join-Path $Runtime 'OoplesFinance.StockIndicators.CompetitorBenchmarks.dll'))
 $tensor=[Runtime.Loader.AssemblyLoadContext]::Default.LoadFromAssemblyPath((Join-Path $Runtime 'AiDotNet.Tensors.dll'))
 $degree=$tensor.GetType('AiDotNet.Tensors.Helpers.CpuParallelSettings',$true).GetProperty('MaxDegreeOfParallelism',$flags)
 $prior=$degree.GetValue($null)
 # TA-Lib compatibility/unstable EMA settings, not a GPU/backend switch.
 $policy=[Activator]::CreateInstance($bench.GetType($ns+'TripleRateComparison+Settings',$true),$flags,$null,[object[]]@($false,[int]0),$null)
 $typeName=if($Operation -eq 'SeparatingLines'){'SharedMatchedLinesBenchmarks'}else{'SharedSequenceCandleBenchmarks'}
 $type=$bench.GetType($ns+$typeName,$true);$fixture=[Activator]::CreateInstance($type)
 $type.GetProperty('Operation').SetValue($fixture,'TaLib.Candles.'+$Operation)
 $type.GetProperty('Count').SetValue($fixture,[int]8193)
 $null=$type.GetMethod('Setup').Invoke($fixture,$null)
 # Setup itself forces8; requested replay configuration is applied AFTER setup.
 $degree.SetValue($null,[int]$Workers)
 if([int]$degree.GetValue($null) -ne $Workers){throw 'Worker API configuration did not stick'}
 $rows=[Collections.Generic.List[object]]::new()
 foreach($name in @('BuilderFull','BuilderLatestOnly')){
  $method=$type.GetMethod($name)
  if($null -eq $method){throw 'Expected actual fixture builder method absent'}
  if([int]$degree.GetValue($null) -ne $Workers){throw 'Worker configuration changed'}
  for($i=0;$i -lt 1000;$i++){$run=$method.Invoke($fixture,$null);$run.Dispose()}
  # Diagnostic promotion opportunity only; never report elapsed time as a benchmark.
  $clock=[Diagnostics.Stopwatch]::StartNew();$calls=0
  do{$run=$method.Invoke($fixture,$null);$run.Dispose();$calls++}while($clock.ElapsedMilliseconds -lt 5000)
  if([int]$degree.GetValue($null) -ne $Workers){throw 'Worker configuration changed during replay'}
  $rows.Add([pscustomobject]@{method=$name;warmup_calls=1000;promotion_calls=$calls;configured_worker_limit=$Workers})
 }
 $result=[ordered]@{status='replayed';host=$hostProof;fixture=$type.FullName;operation=$Operation;count=8193;configured_worker_limit=$Workers;rows=@($rows.ToArray());production_sha256=$ExpectedProduction;qualification=$false;performance_claim=$false;new_compilation=$false;worker_thread_execution_proven=$false}
 [IO.File]::WriteAllText($Receipt,($result|ConvertTo-Json -Depth 8))
}finally{
 if($null -ne $fixture){$null=$fixture.GetType().GetMethod('Cleanup').Invoke($fixture,$null)}
 if($null -ne $policy){$policy.Dispose()}
 if($null -ne $degree){$degree.SetValue($null,$prior)}
 [Runtime.Loader.AssemblyLoadContext]::Default.remove_Resolving($resolve)
}
