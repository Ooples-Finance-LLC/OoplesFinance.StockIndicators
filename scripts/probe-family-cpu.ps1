param([Parameter(Mandatory=$true)][string]$Production,[Parameter(Mandatory=$true)][string]$Expected,[string]$Pair='TaLib.Functions.AdOsc',[ValidateSet('Pair','Native','Full','Latest')][string]$Role='Pair',[int]$Bars=10000,[int]$Workers=8,[int]$Warmups=1000,[int]$ProfileSeconds=0,[string]$Out='',[Parameter(Mandatory=$true)][string]$Runtime)
$ErrorActionPreference='Stop'

if ((Get-FileHash -LiteralPath $Production -Algorithm SHA256).Hash.ToLowerInvariant() -ne $Expected) { throw 'Production hash mismatch' }
Add-Type -TypeDefinition @"
using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Linq.Expressions;
using System.Diagnostics;
using System.Text.Json;
using System.Collections.Generic;
public static class ExactWorkerProbe {
 public static string Run(string runtime,string production,string sha,string pair,string role,int bars,int workers,int warmups,int seconds) {
  var dir=Path.GetDirectoryName(production);
  AssemblyLoadContext.Default.Resolving+=(c,n)=>{string p=Path.Combine(dir,n.Name+".dll");if(!File.Exists(p))p=Path.Combine(runtime,n.Name+".dll");return File.Exists(p)?c.LoadFromAssemblyPath(p):null;};
  var prod=AssemblyLoadContext.Default.LoadFromAssemblyPath(production);
  var bench=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(runtime,"OoplesFinance.StockIndicators.CompetitorBenchmarks.dll"));
  var tensor=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(runtime,"AiDotNet.Tensors.dll"));
  const BindingFlags flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
  const string ns="OoplesFinance.StockIndicators.CompetitorBenchmarks.";
  var degree=tensor.GetType("AiDotNet.Tensors.Helpers.CpuParallelSettings",true).GetProperty("MaxDegreeOfParallelism",flags);var previous=degree.GetValue(null);degree.SetValue(null,workers);
  var policy=(IDisposable)Activator.CreateInstance(bench.GetType(ns+"TripleRateComparison+Settings",true),flags,null,new object[]{false,0},null);
  object fixture=null;MethodInfo cleanup=null;
  try {
   bool library=role=="Pair"||role=="Native";var type=bench.GetType(ns+(library?"LibraryPairBenchmarks":"SharedSmoothedAccumulationBenchmarks"),true);
   fixture=Activator.CreateInstance(type);type.GetProperty(library?"PairId":"Operation").SetValue(fixture,pair);type.GetProperty(library?"Bars":"Count").SetValue(fixture,bars);type.GetMethod("Setup").Invoke(fixture,null);cleanup=type.GetMethod("Cleanup");
   var method=type.GetMethod(role=="Pair"?"Ooples":role=="Native"?"Competitor":role=="Full"?"BuilderFull":"BuilderLatestOnly");
   var call=Expression.Lambda<Func<object>>(Expression.Convert(Expression.Call(Expression.Constant(fixture),method),typeof(object))).Compile();
   object last=null;Action once=()=>{last=call();if(last is IDisposable d)d.Dispose();};
   for(int i=0;i<warmups;i++)once();
   var samples=new List<double>();int iterations=0;
   var profileStartUtc=DateTime.UtcNow;
   Console.WriteLine("PROFILE START ExactWorkerProbe PID="+Environment.ProcessId+" pair="+pair+" role="+role+" bars="+bars);
   if(seconds>0) {var timer=Stopwatch.StartNew();while(timer.Elapsed.TotalSeconds<seconds){once();iterations++;}}
   else for(int i=0;i<10;i++){var timer=Stopwatch.StartNew();for(int j=0;j<10;j++)once();samples.Add(timer.Elapsed.TotalMilliseconds/10);iterations+=10;}
   var profileEndUtc=DateTime.UtcNow;
   Console.WriteLine("PROFILE END iterations="+iterations);GC.KeepAlive(last);
   return JsonSerializer.Serialize(new{diagnostic_only=true,qualification=false,performance_claim=false,process_id=Environment.ProcessId,profile_start_utc=profileStartUtc,profile_end_utc=profileEndUtc,production_sha256=sha,pair,role,bars,workers,warmups,profile_seconds=seconds,samples_ms=samples,iterations});
  } finally {if(fixture!=null&&cleanup!=null)cleanup.Invoke(fixture,null);policy.Dispose();degree.SetValue(null,previous);}
 }
}
"@
$result=[ExactWorkerProbe]::Run($runtime,$Production,$Expected,$Pair,$Role,$Bars,$Workers,$Warmups,$ProfileSeconds)
if($Out){[System.IO.File]::WriteAllText($Out,$result)}
$result
