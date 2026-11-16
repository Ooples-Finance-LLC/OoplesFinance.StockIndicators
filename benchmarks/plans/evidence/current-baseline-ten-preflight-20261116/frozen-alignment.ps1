param([Parameter(Mandatory=$true)][string]$Root)
$ErrorActionPreference='Stop'
$auth=Get-Content -LiteralPath (Join-Path $Root 'authentication.json') -Raw|ConvertFrom-Json
if($auth.status -ne 'authenticated'){throw 'Unauthenticated runtime'}
$runtime=Join-Path $Root 'frozen-runtime'
$production=Join-Path $runtime 'OoplesFinance.StockIndicators.dll'
if((Get-FileHash -LiteralPath $production -Algorithm SHA256).Hash.ToLowerInvariant() -ne $auth.production_sha256){throw 'Wrong production'}
$resolve=[Func[Runtime.Loader.AssemblyLoadContext,Reflection.AssemblyName,Reflection.Assembly]]{
 param($ctx,$name)
 $candidate=Join-Path $runtime ($name.Name+'.dll')
 if(Test-Path -LiteralPath $candidate){return $ctx.LoadFromAssemblyPath($candidate)}
 return $null
}
[Runtime.Loader.AssemblyLoadContext]::Default.add_Resolving($resolve)
# Load canonical production first. The composed frozen runtime also contains
# that same canonical DLL; the original variant production is never loaded.
$prod=[Runtime.Loader.AssemblyLoadContext]::Default.LoadFromAssemblyPath($production)
$bench=[Runtime.Loader.AssemblyLoadContext]::Default.LoadFromAssemblyPath((Join-Path $runtime 'OoplesFinance.StockIndicators.CompetitorBenchmarks.dll'))
$flags=[Reflection.BindingFlags]'Public,NonPublic,Static,Instance'
$ns='OoplesFinance.StockIndicators.CompetitorBenchmarks.'
$pairs=$bench.GetType($ns+'ComparisonPairs',$true).GetMethod('Get',$flags)
$verifier=$bench.GetType($ns+'ComparisonVerifier',$true)
$fixture=$verifier.GetMethod('BenchmarkFixture',$flags)
$check=$verifier.GetMethod('Check',$flags)
# This preserves default TA-Lib compatibility and zero unstable EMA periods;
# this setting is not a GPU/backend switch.
$settingsType=$bench.GetType($ns+'TripleRateComparison+Settings',$true)
$settings=[Activator]::CreateInstance($settingsType,$flags,$null,[object[]]@($false,[int]0),$null)
$rows=[Collections.Generic.List[object]]::new()
try{
 foreach($id in $auth.pairs){
  $pair=$pairs.Invoke($null,[object[]]@([string]$id))
  foreach($count in @(1000,10000)){
   $data=$fixture.Invoke($null,[object[]]@($pair,[int]$count))
   # Check itself verifies independent references for every declared column,
   # native conventions, presence/alignment, retained outputs and poisoning.
   $values=$check.Invoke($null,[object[]]@($pair,$data,[int]20,$true))
   if([int]$values -le 0){throw 'No compared values'}
   $rows.Add([pscustomobject]@{pair=[string]$id;bars=$count;period=20;compared_values=[int]$values;verify_isolation=$true;passed=$true})
   Write-Output "$id $count independent frozen/canonical parity passed"
  }
 }
}finally{$settings.Dispose();[Runtime.Loader.AssemblyLoadContext]::Default.remove_Resolving($resolve)}
if($rows.Count -ne 20){throw 'Incomplete selected fixture coverage'}
$receipt=[ordered]@{status='passed';source=$auth.source;frozen_source=$auth.frozen_source;production_sha256=$auth.production_sha256;frozen_benchmark_sha256=$auth.frozen_benchmark_sha256;rows=@($rows.ToArray());fresh_gpu_execution=$false;new_compilation=$false;performance_claim=$false}
$json=($receipt|ConvertTo-Json -Depth 8).Replace("`r`n","`n")+"`n"
[IO.File]::WriteAllText((Join-Path $Root 'frozen-canonical-alignment.json'),$json)
