using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string,double[]> PriceCycleOutputs(IReadOnlyList<Bar> bars,IBuiltInIndicator indicator)
    {var o=indicator.CreateOptions();return PriceCycleOutputs(bars,Integer(o,"Length",22),AverageKind(o,1));}
    internal static IReadOnlyDictionary<string,double[]> PriceCycleOutputs(IReadOnlyList<Bar> bars,int length,int kind,double[]? customerAtr=null,double[]? customerDistance=null)
    {
        length=Math.Max(1,length);var differences=bars.Select(b=>RoundRocBankStage(ReferenceFraction.FromDouble(b.Close)-ReferenceFraction.FromDouble(b.Low))).ToArray();
        var atr=customerAtr is null?SmoothRocBankStage(PrettyGoodRanges(bars),length,kind):customerAtr.Select(ReferenceFraction.FromDouble).ToArray();
        var distance=customerDistance is null?SmoothRocBankStage(differences,length,kind):customerDistance.Select(ReferenceFraction.FromDouble).ToArray();
        return Outputs(("Pco",distance.Select((v,i)=>atr[i].Sign==0?0:(new ReferenceFraction(100)*v/atr[i]).ToDouble()).ToArray()));
    }
}
