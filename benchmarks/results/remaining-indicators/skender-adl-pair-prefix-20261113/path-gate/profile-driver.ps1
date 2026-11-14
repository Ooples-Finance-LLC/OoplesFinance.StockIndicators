param([string]$Pair='QuanTAlib.Qema',[int]$Seconds=20,[int]$Workers=0,
 [string]$Runtime='C:\Users\cheat\source\repos\Ooples-Finance-LLC\si-qema-difference-combination-20261113\benchmarks\OoplesFinance.StockIndicators.CompetitorBenchmarks\bin\Release\net10.0',
 [string]$Expected='9091f27b5a78f1dd431dc186257073b9b84b283f39bc06ceab760cf36ddb71b2')
$ErrorActionPreference='Stop'
if((Get-FileHash (Join-Path $runtime 'OoplesFinance.StockIndicators.dll')).Hash.ToLowerInvariant() -ne $expected){throw 'Production hash mismatch'}
Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Reflection;
using System.Linq.Expressions;
using System.Runtime.Loader;
using System.Diagnostics;
public static class NativeGapProfile {
 public static void Run(string runtime,string id,int seconds,int workers) {
  AssemblyLoadContext.Default.Resolving+=(c,n)=>{var p=Path.Combine(runtime,n.Name+".dll");return File.Exists(p)?c.LoadFromAssemblyPath(p):null;};
  var a=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(runtime,"OoplesFinance.StockIndicators.CompetitorBenchmarks.dll"));
  if(workers>0){var tensors=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(runtime,"AiDotNet.Tensors.dll"));tensors.GetType("AiDotNet.Tensors.Helpers.CpuParallelSettings",true).GetProperty("MaxDegreeOfParallelism").SetValue(null,workers);}
  const BindingFlags flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
  const string ns="OoplesFinance.StockIndicators.CompetitorBenchmarks.";
  var pair=a.GetType(ns+"ComparisonPairs",true).GetMethod("Get",flags).Invoke(null,new object[]{id});
  var fixture=a.GetType(ns+"ComparisonVerifier",true).GetMethod("BenchmarkFixture",flags).Invoke(null,new object[]{pair,10000});
  var call=Expression.Call(Expression.Constant(pair),pair.GetType().GetMethod("Ooples",flags),Expression.Constant(fixture),Expression.Constant(20));
  var run=Expression.Lambda<Func<object>>(Expression.Convert(call,typeof(object))).Compile();
  object result=null;
  for(int i=0;i<100;i++)result=run();
  Console.WriteLine("PROFILE START "+id+" PID="+Environment.ProcessId+" workers="+workers);
  var timer=Stopwatch.StartNew();long before=GC.GetAllocatedBytesForCurrentThread();int count=0;
  while(timer.Elapsed.TotalSeconds<seconds){result=run();count++;}
  Console.WriteLine("PROFILE END iterations="+count+" seconds="+timer.Elapsed.TotalSeconds+" allocated="+(GC.GetAllocatedBytesForCurrentThread()-before));
  GC.KeepAlive(result);
 }
}
'@
[NativeGapProfile]::Run($runtime,$Pair,$Seconds,$Workers)
