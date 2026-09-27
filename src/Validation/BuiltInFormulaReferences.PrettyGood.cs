using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string,double[]> PrettyGoodOutputs(IReadOnlyList<Bar> bars,IBuiltInIndicator indicator)
    {var o=indicator.CreateOptions();return PrettyGoodOutputs(bars,Integer(o,"Length",14),AverageKind(o,1));}
    internal static ReferenceFraction[] PrettyGoodRanges(IReadOnlyList<Bar> bars)=>bars.Select((b,i)=>
    {
        var high=ReferenceFraction.FromDouble(b.High);var low=ReferenceFraction.FromDouble(b.Low);var prior=ReferenceFraction.FromDouble(i>0?bars[i-1].Close:b.Close);
        var gapHigh=high-prior;var gapLow=low-prior;var zero=new ReferenceFraction(0);if(gapHigh.Sign<0)gapHigh=zero-gapHigh;if(gapLow.Sign<0)gapLow=zero-gapLow;
        return RoundRocBankStage(new[] {high-low,gapHigh,gapLow}.Max());
    }).ToArray();
    internal static IReadOnlyDictionary<string,double[]> PrettyGoodOutputs(IReadOnlyList<Bar> bars,int length,int kind,double[]? customerAverage=null,double[]? customerAtr=null)
    {
        var prices=bars.Select(b=>ReferenceFraction.FromDouble(b.Close)).ToArray();length=Math.Max(1,length);
        var average=customerAverage is null?SmoothRocBankStage(prices,length,kind):customerAverage.Select(ReferenceFraction.FromDouble).ToArray();
        var atr=customerAtr is null?SmoothRocBankStage(PrettyGoodRanges(bars),length,kind):customerAtr.Select(ReferenceFraction.FromDouble).ToArray();
        return Outputs(("Pgo",prices.Select((v,i)=>atr[i].Sign==0?0:((v-average[i])/atr[i]).ToDouble()).ToArray()));
    }
}
