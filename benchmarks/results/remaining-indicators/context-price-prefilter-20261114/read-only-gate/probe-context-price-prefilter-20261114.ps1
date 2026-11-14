$ErrorActionPreference='Stop'
$runtime='C:\Users\cheat\source\repos\Ooples-Finance-LLC\si-star-reversal-certified-difference-20261114\benchmarks\OoplesFinance.StockIndicators.CompetitorBenchmarks\bin\Release\net10.0'
$expected='186c441ad23620cfaae3a94e9d10df7f0b46cacc66cdb8fb03860536b00ce7e5'
if((Get-FileHash (Join-Path $runtime 'OoplesFinance.StockIndicators.dll')).Hash.ToLowerInvariant() -ne $expected){throw 'hash mismatch'}
Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Collections.Generic;
using System.Text.Json;
public static class ContextPricePrefilterProbe {
 const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
 public static string Run(string runtime,string hash){
  AssemblyLoadContext.Default.Resolving+=(context,name)=>{var p=Path.Combine(runtime,name.Name+".dll");return File.Exists(p)?context.LoadFromAssemblyPath(p):null;};
  var a=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(runtime,"OoplesFinance.StockIndicators.dll"));
  var b=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(runtime,"OoplesFinance.StockIndicators.CompetitorBenchmarks.dll"));
  const string ns="OoplesFinance.StockIndicators.CompetitorBenchmarks.";
  var results=new List<object>();
  foreach(string name in new[]{"InvertedHammer","ShootingStar"}){
   string id="TaLib.Candles."+name;
   var pair=b.GetType(ns+"ComparisonPairs",true).GetMethod("Get",F).Invoke(null,new object[]{id});
   var fixture=b.GetType(ns+"ComparisonVerifier",true).GetMethod("BenchmarkFixture",F).Invoke(null,new object[]{pair,10000});
   var bars=(Array)fixture.GetType().GetProperty("IndicatorBars",F).GetValue(fixture);
   var barType=bars.GetType().GetElementType();var open=barType.GetProperty("Open");var close=barType.GetProperty("Close");
   var owner=Activator.CreateInstance(a.GetType("OoplesFinance.StockIndicators.Indicators."+name+"Candle",true),new object[]{10});
   var state=owner.GetType().GetMethod("CreateState",F).Invoke(owner,null);var update=state.GetType().GetMethod("Update",F);
   var kind=Enum.Parse(a.GetType("OoplesFinance.StockIndicators.Indicators.ContextReversalKind",true),name);
   var reference=(IReadOnlyList<double>)a.GetType("OoplesFinance.StockIndicators.Indicators.ContextReversalReference",true).GetMethod("Evaluate",F).Invoke(null,new object[]{bars,10,0,11,kind});
   int eligible=0,rejected=0,signals=0;double po=0,pc=0;
   for(int i=0;i<bars.Length;i++){
    object bar=bars.GetValue(i);double o=(double)open.GetValue(bar),c=(double)close.GetValue(bar);
    double actual=(double)update.Invoke(state,new object[]{bar});
    if(BitConverter.DoubleToInt64Bits(actual)!=BitConverter.DoubleToInt64Bits(reference[i]))throw new Exception("Original/reference mismatch "+id+" "+i);
    if(actual!=0)signals++;
    if(i>=11){eligible++;bool reject=name=="InvertedHammer"?Math.Max(o,c)>=Math.Min(po,pc):Math.Min(o,c)<=Math.Max(po,pc);if(reject){rejected++;if(actual!=0)throw new Exception("Rejected signal");}}
    po=o;pc=c;
   }
   results.Add(new{pair=id,bars=bars.Length,eligible,earlyRejected=rejected,signals,allOriginalReferenceOutputsBitExact=true,noSignalsRejected=true});
  }
  return JsonSerializer.Serialize(new{productionSha256=hash,productionChanged=false,performanceEvidence=false,results});
 }
}
'@
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'context-price-prefilter-authenticated-model-20261114.json'),[ContextPricePrefilterProbe]::Run($runtime,$expected))
