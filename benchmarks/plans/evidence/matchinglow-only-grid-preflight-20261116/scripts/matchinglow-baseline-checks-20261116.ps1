param([Parameter(Mandatory=$true)][string]$Scratch,[Parameter(Mandatory=$true)][string]$Out)
$ErrorActionPreference='Stop'
$ops='C:\Users\cheat\source\stock-indicators-operations'
$baseline=Join-Path $ops 'active-performance-evidence\hosted-runs\43880487485\inputs'
$provenanceSha='92977e9786050146dbe97748c8a28d59e8eb47be5f4b5f85ae064ec4fcacef3a'
$sourceSha='5be1a81d02ad01393f3fa8ede764e914252d0e96'
$frozenSha='9a45372530e49718b01b6524f9a0db993514e1e8'
$productionSha='c63770e43fceda43a2286cb5272643ac3850adcc23962cc01eee2c7a9032c6fe'
$benchmarkSha='48229494a1ff1e4e59bddd5a018cbd590cae24523d52dd2f3effdd20bd802723'
function Hash([string]$Path){return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()}
function MapHash($Map){return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes(($Map|ConvertTo-Json -Depth 6 -Compress)))).ToLowerInvariant()}
function Full([string]$Path){return [IO.Path]::GetFullPath($Path)}
function Within([string]$Path,[string]$Root){return $Path.StartsWith($Root.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)}
function Manifest([string]$Root){
 $map=[ordered]@{}
 foreach($file in (Get-ChildItem -LiteralPath $Root -File -Recurse -Force|Sort-Object FullName)){
  $relative=[IO.Path]::GetRelativePath($Root,$file.FullName).Replace('\','/')
  $map[$relative]=Hash $file.FullName
 }
 return $map
}
function SameMap($Expected,$Actual){
 if($Expected.Count -ne $Actual.Count){throw 'Manifest count changed'}
 foreach($key in $Expected.Keys){if(-not $Actual.Contains($key) -or $Expected[$key] -ne $Actual[$key]){throw "Manifest mismatch: $key"}}
}
function ProtectedSnapshot{
 $map=[ordered]@{}
 foreach($section in @('common','production')){
  $sectionMap=Manifest (Join-Path $baseline $section)
  foreach($key in $sectionMap.Keys){$map[$section+'/'+$key]=$sectionMap[$key]}
 }
 return $map
}
$Scratch=Full $Scratch;$Out=Full $Out
if(-not (Within $Scratch (Join-Path $ops 'matchinglow-baseline-checks-20261116')) -or [IO.Path]::GetPathRoot($Scratch) -ne 'C:\'){throw 'Scratch must be a fresh child of the dedicated C runtime root'}
if(-not (Within $Out 'Z:\StockIndicators-Archives\active-operations')){throw 'Logs/receipt must be a fresh Z active-operations directory'}
if((Test-Path -LiteralPath $Scratch) -or (Test-Path -LiteralPath $Out)){throw 'Never reuse/overwrite a scratch runtime or output folder'}
$hostProof=[ordered]@{runtime_version=[Environment]::Version.ToString();framework_description=[Runtime.InteropServices.RuntimeInformation]::FrameworkDescription;corelib_product=[Diagnostics.FileVersionInfo]::GetVersionInfo([object].Assembly.Location).ProductVersion}
if($hostProof.runtime_version -ne '10.0.15' -or $hostProof.framework_description -ne '.NET 10.0.15'){throw 'Baseline checks require the authenticated .NET10.0.15 host'}
$provenancePath=Join-Path $baseline 'provenance.json'
if((Hash $provenancePath) -ne $provenanceSha){throw 'Original438804 provenance changed'}
$provenance=Get-Content -LiteralPath $provenancePath -Raw|ConvertFrom-Json -AsHashtable
if($provenance.sources.old -ne $sourceSha -or $provenance.sources.successor -ne $sourceSha -or $provenance.sources.variants -ne $frozenSha){throw 'Wrong canonical/frozen source identities'}
if($provenance.common_manifest.Count -ne 94 -or $provenance.production_sha256.Count -ne 3){throw 'Unexpected protected runtime composition'}
if($provenance.production_sha256.old -ne $productionSha -or $provenance.common_manifest['OoplesFinance.StockIndicators.CompetitorBenchmarks.dll'] -ne $benchmarkSha){throw 'Wrong production/harness hashes'}
$expectedProtected=[ordered]@{}
foreach($key in $provenance.common_manifest.Keys){$expectedProtected['common/'+$key]=$provenance.common_manifest[$key]}
foreach($key in $provenance.production_sha256.Keys){$expectedProtected['production/'+$key+'.dll']=$provenance.production_sha256[$key]}
$protectedBefore=ProtectedSnapshot
SameMap $expectedProtected $protectedBefore
$expectedRuntime=[ordered]@{}
foreach($key in $provenance.common_manifest.Keys){$expectedRuntime[$key]=$provenance.common_manifest[$key]}
# Select canonical bytes BEFORE creating any link. Never link variant core then overwrite it.
$expectedRuntime['OoplesFinance.StockIndicators.dll']=$productionSha
[IO.Directory]::CreateDirectory($Scratch)|Out-Null
[IO.Directory]::CreateDirectory($Out)|Out-Null
$logPath=Join-Path $Out 'baseline-checks.log'
$rows=[Collections.Generic.List[object]]::new()
$resolve=$null;$settings=$null
try{
 foreach($relative in $expectedRuntime.Keys){
  $destination=Full (Join-Path $Scratch $relative)
  if(-not (Within $destination $Scratch)){throw 'Unsafe runtime member path'}
  $original=if($relative -eq 'OoplesFinance.StockIndicators.dll'){Join-Path $baseline 'production\old.dll'}else{Join-Path (Join-Path $baseline 'common') $relative}
  if((Hash $original) -ne $expectedRuntime[$relative]){throw 'Protected source hash mismatch'}
  if(Test-Path -LiteralPath $destination){throw 'Never overwrite a hardlink target'}
  [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination))|Out-Null
  New-Item -ItemType HardLink -Path $destination -Target $original -ErrorAction Stop|Out-Null
  if((Hash $destination) -ne $expectedRuntime[$relative]){throw 'Composed runtime hash mismatch'}
 }
 SameMap $expectedRuntime (Manifest $Scratch)
 $production=Join-Path $Scratch 'OoplesFinance.StockIndicators.dll'
 $benchmark=Join-Path $Scratch 'OoplesFinance.StockIndicators.CompetitorBenchmarks.dll'
 $resolve=[Func[Runtime.Loader.AssemblyLoadContext,Reflection.AssemblyName,Reflection.Assembly]]{
  param($ctx,$name)
  $candidate=Join-Path $Scratch ($name.Name+'.dll')
  if(Test-Path -LiteralPath $candidate){return $ctx.LoadFromAssemblyPath($candidate)}
  return $null
 }
 [Runtime.Loader.AssemblyLoadContext]::Default.add_Resolving($resolve)
 $prod=[Runtime.Loader.AssemblyLoadContext]::Default.LoadFromAssemblyPath($production)
 $bench=[Runtime.Loader.AssemblyLoadContext]::Default.LoadFromAssemblyPath($benchmark)
 if((Full $prod.Location) -ne $production -or (Full $bench.Location) -ne $benchmark){throw 'Unexpected preloaded assembly identity'}
 $flags=[Reflection.BindingFlags]'Public,NonPublic,Static,Instance'
 $ns='OoplesFinance.StockIndicators.CompetitorBenchmarks.'
 $pairs=$bench.GetType($ns+'ComparisonPairs',$true).GetMethod('Get',$flags)
 $verifier=$bench.GetType($ns+'ComparisonVerifier',$true)
 $fixture=$verifier.GetMethod('BenchmarkFixture',$flags)
 $check=$verifier.GetMethod('Check',$flags)
 # TA-Lib compatibility and unstable EMA settings only; not a GPU/backend switch.
 $settings=[Activator]::CreateInstance($bench.GetType($ns+'TripleRateComparison+Settings',$true),$flags,$null,[object[]]@($false,[int]0),$null)
 foreach($id in @('TaLib.Candles.MatchingLow','TaLib.Candles.SeparatingLines','TaLib.Candles.StickSandwich')){
  $pair=$pairs.Invoke($null,[object[]]@([string]$id))
  foreach($count in @(1000,10000)){
   $data=$fixture.Invoke($null,[object[]]@($pair,[int]$count))
   $values=$check.Invoke($null,[object[]]@($pair,$data,[int]20,$true))
   if([int]$values -le 0){throw 'No compared values'}
   $rows.Add([pscustomobject]@{pair=[string]$id;bars=$count;period=20;compared_values=[int]$values;verify_isolation=$true;passed=$true})
   $line="$id $count independent frozen/canonical parity passed; compared_values=$values"
   [IO.File]::AppendAllText($logPath,$line+"`n");Write-Output $line
  }
 }
 if($rows.Count -ne 6){throw 'Incomplete six-call baseline coverage'}
 SameMap $expectedRuntime (Manifest $Scratch)
}catch{
 [IO.File]::AppendAllText($logPath,'FAILED: '+$_.Exception.ToString()+"`n")
 throw
}finally{
 if($null -ne $settings){$settings.Dispose()}
 if($null -ne $resolve){[Runtime.Loader.AssemblyLoadContext]::Default.remove_Resolving($resolve)}
 # Rehash protected inputs even when reflection or a Check call fails.
 SameMap $protectedBefore (ProtectedSnapshot)
 if((Hash $provenancePath) -ne $provenanceSha){throw 'Protected provenance changed'}
}
$receipt=[ordered]@{kind='matchinglow-baseline-six-checks';status='passed';source=$sourceSha;frozen_source=$frozenSha;baseline_input_run='43880487485';input_provenance_sha256=$provenanceSha;production_sha256=$productionSha;frozen_benchmark_sha256=$benchmarkSha;common_manifest_count=94;composed_runtime_manifest_count=94;check_count=6;rows=@($rows.ToArray());protected_inputs_unchanged=$true;protected_manifest=$protectedBefore;protected_manifest_sha256=(MapHash $protectedBefore);composed_runtime_manifest=$expectedRuntime;composed_runtime_manifest_sha256=(MapHash $expectedRuntime);manifest_digest_format='SHA256 of UTF8 PowerShell compressed JSON preserving recorded map order';runtime_materialization='fresh-C-hardlinks-canonical-core-selected-before-linking';runtime_path=$Scratch;host=$hostProof;script_sha256=(Hash $PSCommandPath);log_sha256=(Hash $logPath);fresh_gpu_execution=$false;new_compilation=$false;performance_claim=$false;qualification=$false}
$receiptPath=Join-Path $Out 'baseline-six-checks.json';$temporary=$receiptPath+'.tmp'
if(Test-Path -LiteralPath $temporary){throw 'Unexpected receipt temporary'}
[IO.File]::WriteAllText($temporary,($receipt|ConvertTo-Json -Depth 12).Replace("`r`n","`n")+"`n")
[IO.File]::Move($temporary,$receiptPath,$false)
Write-Output "Saved six-check receipt: $receiptPath"
