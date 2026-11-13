param(
 [Parameter(Mandatory=$true)][string]$Runtime,
 [Parameter(Mandatory=$true)][string]$ProductionSha256,
 [Parameter(Mandatory=$true)][string]$Output,
 [ValidateSet('Ceil','Floor')][string]$Operation='Floor',
 [ValidateSet('Ooples','Competitor','BuilderFull','BuilderLatestOnly')][string]$Arm='Ooples',
 [int]$Count=10000
)
$ErrorActionPreference='Stop'
if((Get-FileHash (Join-Path $Runtime 'OoplesFinance.StockIndicators.dll')).Hash.ToLowerInvariant() -ne $ProductionSha256.ToLowerInvariant()){throw 'Production hash mismatch'}
Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Linq.Expressions;
using System.Runtime.Loader;
using System.Diagnostics;
using System.Text.Json;
using System.Collections.Generic;
public static class TensorRoundingDiagnostic {
 public static string Run(string runtime,string operation,string arm,int count){
  AssemblyLoadContext.Default.Resolving+=(c,n)=>{var p=Path.Combine(runtime,n.Name+".dll");return File.Exists(p)?c.LoadFromAssemblyPath(p):null;};
  var a=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(runtime,"OoplesFinance.StockIndicators.CompetitorBenchmarks.dll"));
  const BindingFlags flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
  const string ns="OoplesFinance.StockIndicators.CompetitorBenchmarks.";
  Func<object> invoke;object benchmark=null;
  if(arm.StartsWith("Builder")){
   var type=a.GetType(ns+"SharedPointwiseBenchmarks",true);benchmark=Activator.CreateInstance(type);
   type.GetProperty("Operation").SetValue(benchmark,operation);type.GetProperty("Count").SetValue(benchmark,count);
   type.GetMethod("Setup").Invoke(benchmark,null);
   invoke=Expression.Lambda<Func<object>>(Expression.Convert(Expression.Call(Expression.Constant(benchmark),type.GetMethod(arm)),typeof(object))).Compile();
  }else{
   var pair=a.GetType(ns+"ComparisonPairs",true).GetMethod("Get",flags).Invoke(null,new object[]{"TaLib.Functions."+operation});
   var fixture=a.GetType(ns+"ComparisonVerifier",true).GetMethod("BenchmarkFixture",flags).Invoke(null,new object[]{pair,count});
   Expression call=arm=="Ooples"?Expression.Call(Expression.Constant(pair),pair.GetType().GetMethod("Ooples",flags),Expression.Constant(fixture),Expression.Constant(20)):
    Expression.Invoke(Expression.Constant(pair.GetType().GetProperty("Competitor").GetValue(pair)),Expression.Constant(fixture),Expression.Constant(20));
   invoke=Expression.Lambda<Func<object>>(Expression.Convert(call,typeof(object))).Compile();
  }
  object result=null;
  void RunOnce(){result=invoke();if(result is IDisposable d)d.Dispose();}
  try{
   for(int i=0;i<1000;i++)RunOnce();
   var rounds=new List<object>();
   for(int round=0;round<5;round++){
    long before=GC.GetAllocatedBytesForCurrentThread();var timer=Stopwatch.StartNew();
    for(int i=0;i<1000;i++)RunOnce();timer.Stop();
    rounds.Add(new{round,iterations=1000,elapsedMs=timer.Elapsed.TotalMilliseconds,nsPerCall=timer.Elapsed.TotalNanoseconds/1000,allocatedBytes=GC.GetAllocatedBytesForCurrentThread()-before});
   }
   GC.KeepAlive(result);
   return JsonSerializer.Serialize(new{diagnosticOnly=true,operation,arm,count,warmups=1000,productionModule=AssemblyLoadContext.Default.Assemblies.Single(x=>x.GetName().Name=="OoplesFinance.StockIndicators").ManifestModule.ModuleVersionId,rounds},new JsonSerializerOptions{WriteIndented=true});
  }finally{if(benchmark!=null)benchmark.GetType().GetMethod("Cleanup").Invoke(benchmark,null);}
 }
}
'@
$result=[TensorRoundingDiagnostic]::Run($Runtime,$Operation,$Arm,$Count)
$result | Set-Content -LiteralPath $Output
$result
