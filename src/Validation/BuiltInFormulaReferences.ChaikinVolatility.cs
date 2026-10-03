using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string,double[]> ChaikinVolatilityOutputs(IReadOnlyList<Bar> bars,IBuiltInIndicator indicator)
    {var o=indicator.CreateOptions();return ChaikinVolatilityOutputs(bars,Integer(o,"Length",10),Integer(o,"Length2",12),AverageKind(o,3));}
    internal static IReadOnlyDictionary<string,double[]> ChaikinVolatilityOutputs(IReadOnlyList<Bar> bars,int smooth,int lag,int kind,double[]? customer=null)
    {
        lag=Math.Max(1,lag);var zero=new ReferenceFraction(0);
        var ranges=bars.Select(b=>RoundRocBankStage(ReferenceFraction.FromDouble(b.High)-ReferenceFraction.FromDouble(b.Low))).ToArray();
        var average=customer is null?SmoothRocBankStage(ranges,Math.Max(1,smooth),kind):customer.Select(ReferenceFraction.FromDouble).ToArray();
        return Outputs(("Cv",average.Select((v,i)=>i<lag||average[i-lag].Sign==0?0:(new ReferenceFraction(100)*(v-average[i-lag])/average[i-lag]).ToDouble()).ToArray()));
    }
}
