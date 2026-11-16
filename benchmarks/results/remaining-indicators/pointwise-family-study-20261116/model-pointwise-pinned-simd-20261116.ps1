param([string]$Runtime='C:\Users\cheat\source\stock-indicators-operations\active-performance-evidence\hosted-runs\43779540029\artifact\roi-inputs\common')
$ErrorActionPreference='Stop'
Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Collections.Generic;
using System.Text.Json;
public static class PinnedPointwiseSimdModel {
 public delegate void Unary(ReadOnlySpan<double> x, Span<double> dst);
 public delegate void Binary(ReadOnlySpan<double> x, ReadOnlySpan<double> y, Span<double> dst);
 public static string Run(string runtime) {
  var asm=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(runtime,"AiDotNet.Tensors.dll"));
  var core=asm.GetType("AiDotNet.Tensors.Helpers.TensorPrimitivesCore",true);
  var random=new Random(20261116); const int n=100037;
  var x=new double[n];var y=new double[n];var dst=new double[n];
  double[] edges={0.0,-0.0,double.Epsilon,-double.Epsilon,double.MaxValue,-double.MaxValue,1,-1,Math.BitIncrement(1),Math.BitDecrement(1),Math.ScaleB(1,-1022),-Math.ScaleB(1,-1022),Math.PI,-Math.PI};
  for(int i=0;i<n;i++) { x[i]=i<edges.Length?edges[i]:BitConverter.Int64BitsToDouble(random.NextInt64() | (random.Next(2)==0?long.MinValue:0)); if(!double.IsFinite(x[i]))x[i]=1; y[i]=i<edges.Length?edges[edges.Length-1-i]:BitConverter.Int64BitsToDouble(random.NextInt64() | (random.Next(2)==0?long.MinValue:0));if(!double.IsFinite(y[i]))y[i]=1; }
  var rows=new List<object>();
  foreach(string op in new[]{"Add","Subtract","Multiply","Divide","Floor","Ceiling","Sqrt","Sin","Cos","Tan","Acos","Atan","Log","Log10","Exp","Sinh","Cosh","Tanh"}) {
   bool binary=op=="Add"||op=="Subtract"||op=="Multiply"||op=="Divide";
   var type=asm.GetType("AiDotNet.Tensors.Operators."+op+"OperatorDouble",false);
   if(type==null){rows.Add(new {op,unavailable=true});continue;}
   var method=core.GetMethods().Single(m=>m.Name==(binary?"InvokeSpanSpanIntoSpan":"InvokeSpanIntoSpan")&&m.GetParameters()[0].ParameterType==typeof(ReadOnlySpan<double>)).MakeGenericMethod(type);
   var unary=binary?null:(Unary)method.CreateDelegate(typeof(Unary));
   var pair=binary?(Binary)method.CreateDelegate(typeof(Binary)):null;
   int checkedValues=0,mismatches=0;var examples=new List<object>();
   foreach(int offset in new[]{0,1,3,7}) foreach(int length in new[]{0,1,3,4,7,8,15,16,17,255,256,257,1000,10000,n-7}) {
    if(binary)pair(x.AsSpan(offset,length),y.AsSpan(offset,length),dst.AsSpan(offset,length));else unary(x.AsSpan(offset,length),dst.AsSpan(offset,length));
    for(int j=offset;j<offset+length;j++){
     double a=x[j],b=y[j];double expected=op switch {"Add"=>a+b,"Subtract"=>a-b,"Multiply"=>a*b,"Divide"=>a/b,"Floor"=>Math.Floor(a),"Ceiling"=>Math.Ceiling(a),"Sqrt"=>Math.Sqrt(a),"Sin"=>Math.Sin(a),"Cos"=>Math.Cos(a),"Tan"=>Math.Tan(a),"Acos"=>Math.Acos(a),"Atan"=>Math.Atan(a),"Log"=>Math.Log(a),"Log10"=>Math.Log10(a),"Exp"=>Math.Exp(a),"Sinh"=>Math.Sinh(a),"Cosh"=>Math.Cosh(a),"Tanh"=>Math.Tanh(a),_=>throw new Exception()};
     // Only values that the owned API publishes as defined and finite can use a vector fast path.
     if(!double.IsFinite(expected))continue;
     checkedValues++;if(BitConverter.DoubleToInt64Bits(expected)!=BitConverter.DoubleToInt64Bits(dst[j])){mismatches++;if(examples.Count<3)examples.Add(new {input=a,other=b,expected,actual=dst[j]});}
    }
   }
   rows.Add(new {op,checkedValues,mismatches,examples});
  }
  return JsonSerializer.Serialize(new {diagnostic_only=true,qualification=false,source_unchanged=true,generated_inputs_per_operand=n,negative_inputs=x.Count(v=>v<0),comparison_counts_include_repeated_offsets_and_lengths=true,vector128=System.Runtime.Intrinsics.Vector128.IsHardwareAccelerated,vector256=System.Runtime.Intrinsics.Vector256.IsHardwareAccelerated,vector512=System.Runtime.Intrinsics.Vector512.IsHardwareAccelerated,rows});
 }
}
'@
$json=[PinnedPointwiseSimdModel]::Run($Runtime)
$json | Set-Content -LiteralPath 'C:\Users\cheat\source\stock-indicators-operations\active-performance-evidence\pointwise-pinned-simd-model-20261116.json'
$json
