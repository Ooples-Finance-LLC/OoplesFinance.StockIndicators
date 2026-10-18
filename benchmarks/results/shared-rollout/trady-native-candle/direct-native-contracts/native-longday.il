.class public auto ansi beforefieldinit Trady.Analysis.Candlestick.LongDay`2 extends [Trady.Analysis.dll]Trady.Analysis.Infrastructure.AnalyzableBase`4<TInput,System.ValueTuple`2<System.Decimal,System.Decimal>,System.Boolean,TOutput>
{
  .custom instance void class [netstandard]System.Runtime.CompilerServices.TupleElementNamesAttribute::.ctor(string[]) = ( 01 00 02 00 00 00 04 4F 70 65 6E 05 43 6C 6F 73 65 00 00 ) // .......Open.Close..
  .field private initonly int32 '<PeriodCount>k__BackingField'
  .custom instance void class [netstandard]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) // ....
  .field private initonly valuetype [netstandard]System.Decimal '<Threshold>k__BackingField'
  .custom instance void class [netstandard]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) // ....
  .method hidebysig virtual instance default bool ComputeByIndexImpl(class [netstandard]System.Collections.Generic.IReadOnlyList`1<System.ValueTuple`2<System.Decimal,System.Decimal>> mappedInputs, int32 index) cil managed
  {
    .param [1]
    .custom instance void class [netstandard]System.Runtime.CompilerServices.TupleElementNamesAttribute::.ctor(string[]) = ( 01 00 02 00 00 00 04 4F 70 65 6E 05 43 6C 6F 73 65 00 00 ) // .......Open.Close..
    // Method begins at Relative Virtual Address (RVA) 0xCA08
    // Code size 109 (0x6D)
    .maxstack 5
    .locals init(valuetype [netstandard]System.Decimal V_0, class [netstandard]System.Nullable`1<System.Decimal> V_1, class [netstandard]System.Nullable`1<System.Int32> V_2)
    IL_0000: ldarg.1
    IL_0001: ldsfld class [netstandard]System.Func`2<System.ValueTuple`2<System.Decimal,System.Decimal>,System.Decimal> [Trady.Analysis.dll]Trady.Analysis.Candlestick.LongDay`2/<>c<TInput,TOutput>::'<>9__7_0'
    IL_0006: dup
    IL_0007: brtrue.s     IL_0020
    IL_0009: pop
    IL_000a: ldsfld class [Trady.Analysis.dll]Trady.Analysis.Candlestick.LongDay`2/<>c<TInput,TOutput> [Trady.Analysis.dll]Trady.Analysis.Candlestick.LongDay`2/<>c<TInput,TOutput>::'<>9'
    IL_000f: ldftn instance valuetype [netstandard]System.Decimal class [Trady.Analysis.dll]Trady.Analysis.Candlestick.LongDay`2/<>c<TInput,TOutput>::<ComputeByIndexImpl>b__7_0([netstandard]System.ValueTuple`2<System.Decimal,System.Decimal>)
    IL_0015: newobj instance void class [netstandard]System.Func`2<System.ValueTuple`2<System.Decimal,System.Decimal>,System.Decimal>::.ctor([netstandard]System.Object, [netstandard]System.IntPtr)
    IL_001a: dup
    IL_001b: stsfld class [netstandard]System.Func`2<System.ValueTuple`2<System.Decimal,System.Decimal>,System.Decimal> [Trady.Analysis.dll]Trady.Analysis.Candlestick.LongDay`2/<>c<TInput,TOutput>::'<>9__7_0'
    IL_0020: call System.Collections.Generic.IEnumerable`1<!!1> class [netstandard]System.Linq.Enumerable::Select<valuetype [netstandard]System.ValueTuple`2<valuetype [netstandard]System.Decimalvaluetype [netstandard]System.Decimal>valuetype [netstandard]System.Decimal>(System.Collections.Generic.IEnumerable`1<!!0>, System.Func`2<!!0,!!1>)
    IL_0025: dup
    IL_0026: ldarg.2
    IL_0027: call !!0 class [netstandard]System.Linq.Enumerable::ElementAt<valuetype [netstandard]System.Decimal>(System.Collections.Generic.IEnumerable`1<!!0>, System.Int32)
    IL_002c: stloc.0
    IL_002d: ldarg.0
    IL_002e: call instance int32 class [Trady.Analysis.dll]Trady.Analysis.Candlestick.LongDay`2<TInput,TOutput>::get_PeriodCount()
    IL_0033: ldarg.0
    IL_0034: call instance valuetype [netstandard]System.Decimal class [Trady.Analysis.dll]Trady.Analysis.Candlestick.LongDay`2<TInput,TOutput>::get_Threshold()
    IL_0039: ldloca.s class V_2
    IL_003b: initobj class [netstandard]System.Nullable`1<System.Int32>
    IL_0041: ldloc.2
    IL_0042: ldloca.s class V_2
    IL_0044: initobj class [netstandard]System.Nullable`1<System.Int32>
    IL_004a: ldloc.2
    IL_004b: call [netstandard]System.Collections.Generic.IReadOnlyList`1<System.Nullable`1<System.Decimal>> class Trady.Analysis.Extension.TuplesExtension::Percentile([netstandard]System.Collections.Generic.IEnumerable`1<System.Decimal>, int32, valuetype [netstandard]System.Decimal, [netstandard]System.Nullable`1<System.Int32>, [netstandard]System.Nullable`1<System.Int32>)
    IL_0050: ldarg.2
    IL_0051: callvirt instance var class [netstandard]System.Collections.Generic.IReadOnlyList`1<System.Nullable`1<System.Decimal>>::get_Item(int32)
    IL_0056: stloc.1
    IL_0057: ldloc.0
    IL_0058: ldloca.s class V_1
    IL_005a: call instance var class [netstandard]System.Nullable`1<System.Decimal>::GetValueOrDefault()
    IL_005f: call bool class valuetype [netstandard]System.Decimal::op_GreaterThanOrEqual(valuetype [netstandard]System.Decimal, valuetype [netstandard]System.Decimal)
    IL_0064: ldloca.s class V_1
    IL_0066: call instance bool class [netstandard]System.Nullable`1<System.Decimal>::get_HasValue()
    IL_006b: and
    IL_006c: ret
  } // End of method System.Boolean Trady.Analysis.Candlestick.LongDay`2::ComputeByIndexImpl(System.Collections.Generic.IReadOnlyList`1<System.ValueTuple`2<System.Decimal,System.Decimal>>,System.Int32)

  .class nested private auto ansi sealed beforefieldinit <>c extends [netstandard]System.Object
  {
    .custom instance void class [netstandard]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) // ....
    .field public static initonly class [Trady.Analysis.dll]Trady.Analysis.Candlestick.LongDay`2/<>c<TInput,TOutput> '<>9'
    .field public static class [netstandard]System.Func`2<System.ValueTuple`2<System.Decimal,System.Decimal>,System.Decimal> '<>9__7_0'
    .custom instance void class [netstandard]System.Runtime.CompilerServices.TupleElementNamesAttribute::.ctor(string[]) = ( 01 00 02 00 00 00 04 4F 70 65 6E 05 43 6C 6F 73 65 00 00 ) // .......Open.Close..
  } // End of class Trady.Analysis.Candlestick.LongDay`2/<>c
}
