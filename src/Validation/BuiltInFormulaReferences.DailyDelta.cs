using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string,double[]> DailyDeltaOutputs(IReadOnlyList<Bar> bars,IBuiltInIndicator indicator)
    {var o=indicator.CreateOptions();return DailyDeltaOutputs(bars,Integer(o,"Length",21),AverageKind(o,1));}
    internal static IReadOnlyDictionary<string,double[]> DailyDeltaOutputs(IReadOnlyList<Bar> bars,int length,int kind,double[]? customerHigh=null,double[]? customerLow=null)
    {
        var highs=bars.Select(b=>ReferenceFraction.FromDouble(b.High)).ToArray();var lows=bars.Select(b=>ReferenceFraction.FromDouble(b.Low)).ToArray();
        var ah=customerHigh is null?SmoothRocBankStage(highs,Math.Max(1,length),kind):customerHigh.Select(ReferenceFraction.FromDouble).ToArray();
        var al=customerLow is null?SmoothRocBankStage(lows,Math.Max(1,length),kind):customerLow.Select(ReferenceFraction.FromDouble).ToArray();
        var widths=ah.Select((v,i)=>RoundRocBankStage(v-al[i])).ToArray();
        return Outputs(("UpperBand",highs.Select((v,i)=>(v+widths[i]).ToDouble()).ToArray()),("LowerBand",lows.Select((v,i)=>(v-widths[i]).ToDouble()).ToArray()));
    }
}
