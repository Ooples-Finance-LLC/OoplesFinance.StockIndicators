$ErrorActionPreference='Stop'
$ops=$PSScriptRoot
$q=Get-Content -LiteralPath (Join-Path $ops 'context-price-prefilter-20261114-draft.json') -Raw | ConvertFrom-Json
if($q.gpu_passed -ne 100 -or $q.unit_passed -ne 16 -or $q.comparator_passed -ne 147){throw 'Fresh corrected-source correctness is required'}
$runtime=Join-Path $q.repo 'benchmarks\OoplesFinance.StockIndicators.CompetitorBenchmarks\bin\Release\net10.0'
$manifest=Get-Content -LiteralPath (Join-Path (Join-Path $ops $q.correctness_output_name) 'ci-manifest.json') -Raw | ConvertFrom-Json
$expected=$manifest.runtime_files.'src/bin/Release/net10.0/OoplesFinance.StockIndicators.dll'
if((Get-FileHash -LiteralPath (Join-Path $runtime 'OoplesFinance.StockIndicators.dll')).Hash.ToLowerInvariant() -ne $expected){throw 'Corrected production hash mismatch'}
Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Collections.Generic;
using System.Text.Json;
public static class ContextBatchModeProbe {
 const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
 public static string Run(string runtime,string hash,string source){
  AssemblyLoadContext.Default.Resolving+=(context,name)=>{var p=Path.Combine(runtime,name.Name+".dll");return File.Exists(p)?context.LoadFromAssemblyPath(p):null;};
  var a=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(runtime,"OoplesFinance.StockIndicators.dll"));
  const string ns="OoplesFinance.StockIndicators.Indicators.";
  var kernel=a.GetType(ns+"ValuesBarExecution+ContextReversalKernel",true);
  var target=a.GetType(ns+"ValuesBarExecution+BoundedOutput",true);
  var targetCtor=target.GetConstructors(F).Single(ctor=>ctor.GetParameters().Length==3);
  var stateType=a.GetType(ns+"ContextReversalState",true);
  var field=kernel.GetFields(F).Single(candidate=>candidate.FieldType==stateType);
  var mode=stateType.GetProperty("PreferPriceContext",F);
  var rows=new List<object>();
  foreach(string name in new[]{"Hammer","HangingMan","InvertedHammer","ShootingStar"}){
   var ownerType=a.GetType(ns+name+"Candle",true);
   object owner=Activator.CreateInstance(ownerType,name=="Hammer"||name=="HangingMan"?new object[]{10,5}:new object[]{10});
   var streaming=ownerType.GetMethod("CreateState",F).Invoke(owner,null);
   if((bool)mode.GetValue(streaming))throw new Exception("Streaming mode changed");
   foreach(int bars in new[]{1000,8191,8192,10000}){
    object output=targetCtor.Invoke(new object[]{new double[][]{new double[bars]},0,0});
    // Deliberately short worker slice: selection must use the full output length.
    object boxed=kernel.GetMethod("Create",F).Invoke(null,new object[]{12,17,owner,output});
    object state=field.GetValue(boxed);
    bool expected=bars>=8192&&(name=="InvertedHammer"||name=="ShootingStar");
    bool actual=(bool)mode.GetValue(state);
    if(actual!=expected)throw new Exception("Wrong batch selection "+name+" "+bars);
    stateType.GetMethod("Reset",F).Invoke(state,null);
    if((bool)mode.GetValue(state)!=expected)throw new Exception("Reset changed configuration");
    rows.Add(new{owner=name,bars,workerSlice=17,pricePrefilter=actual,configurationSurvivesReset=true});
   }
  }
  return JsonSerializer.Serialize(new{source,productionSha256=hash,rows,streamingUnchanged=true,performanceEvidence=false});
 }
}
'@
[IO.File]::WriteAllText((Join-Path $ops 'context-batch-mode-verified-20261114.json'),[ContextBatchModeProbe]::Run($runtime,$expected,$q.source))
