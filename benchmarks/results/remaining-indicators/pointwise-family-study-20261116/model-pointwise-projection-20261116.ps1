param([string]$Runtime='C:\Users\cheat\source\stock-indicators-operations\active-performance-evidence\hosted-runs\43779540029\artifact\roi-inputs\common')
$ErrorActionPreference='Stop'
Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
public static class PointwiseProjectionModel {
 public sealed class Projection {public double[] Values;public bool[] Present;}
 public static Projection Direct(ReadOnlySpan<double> values, ReadOnlySpan<double> flags) {
  if(values.Length!=flags.Length)throw new ArgumentException();
  var output=new Projection {Values=new double[values.Length],Present=new bool[values.Length]};
  for(int i=0;i<values.Length;i++) {bool defined=flags[i]>0;output.Present[i]=defined;output.Values[i]=defined?values[i]:double.NaN;}
  return output;
 }
 public static string Run(string runtime) {
  AssemblyLoadContext.Default.Resolving+=(c,n)=>{var p=Path.Combine(runtime,n.Name+".dll");return File.Exists(p)?c.LoadFromAssemblyPath(p):null;};
  var bench=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(runtime,"OoplesFinance.StockIndicators.CompetitorBenchmarks.dll"));
  var nullable=bench.GetType("OoplesFinance.StockIndicators.CompetitorBenchmarks.VolumePriceComparison",true).GetMethod("Nullable",BindingFlags.NonPublic|BindingFlags.Static);
  if(nullable==null)throw new Exception("Pinned original publisher unavailable");
  var random=new Random(20261116); int compared=0,owned=0; const int n=100037;
  double[] edges={0.0,-0.0,double.Epsilon,-double.Epsilon,double.MaxValue,-double.MaxValue,double.PositiveInfinity,double.NegativeInfinity,double.NaN,BitConverter.Int64BitsToDouble(unchecked((long)0xfff8000000001234UL))};
  double[] flags={0,-0.0,1,-1,double.NaN,double.PositiveInfinity,double.NegativeInfinity,double.Epsilon,-double.Epsilon};
  foreach(int count in new[]{0,1,3,7,8,255,256,257,1000,10000,n}){
   var values=new double[count];var present=new double[count];
   for(int i=0;i<count;i++){values[i]=i<edges.Length?edges[i]:BitConverter.Int64BitsToDouble(random.NextInt64()|(random.Next(2)==0?long.MinValue:0));present[i]=flags[i%flags.Length];}
   var legacy=values.Select((v,i)=>present[i]>0?(double?)v:null).ToArray();
   var baseline=nullable.Invoke(null,new object[]{legacy});
   var originalValues=(double[])baseline.GetType().GetProperty("Values").GetValue(baseline);
   var originalFlags=(bool[])baseline.GetType().GetProperty("Present").GetValue(baseline);
   var result=Direct(values,present);
   for(int i=0;i<count;i++){if(result.Present[i]!=originalFlags[i]||BitConverter.DoubleToInt64Bits(result.Values[i])!=BitConverter.DoubleToInt64Bits(originalValues[i]))throw new Exception("Bit/presence mismatch");compared++;}
   Array.Fill(values,42);Array.Fill(present,42);
   for(int i=0;i<count;i++){if(result.Present[i]!=originalFlags[i]||BitConverter.DoubleToInt64Bits(result.Values[i])!=BitConverter.DoubleToInt64Bits(originalValues[i]))throw new Exception("Output aliases input");owned++;}
  }
  return JsonSerializer.Serialize(new{diagnostic_only=true,qualification=false,production_unchanged=true,actual_frozen_publisher=true,values_compared=compared,independent_ownership_checks=owned,includes_nan_payloads_signed_zero_and_nonfinite_values=true,latency_measured=false,avoided_arrays="Value copy, flag copy, nullable intermediate; output values/presence remain owned.",limitation="Prototype covers projection only. Integration must retain builder construction, Full history, input validation, cancellation and all errors. Both benchmark labels must use the same repaired harness; historical timings cannot be compared across harness versions."});
 }
}
'@
$json=[PointwiseProjectionModel]::Run($Runtime)
$json | Set-Content -LiteralPath 'C:\Users\cheat\source\stock-indicators-operations\active-performance-evidence\pointwise-direct-projection-model-20261116.json'
$json
