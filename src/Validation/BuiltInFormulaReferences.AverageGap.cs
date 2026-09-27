using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string,double[]> AverageGapOutputs(IReadOnlyList<Bar> bars,IBuiltInIndicator indicator)
    {var o=indicator.CreateOptions();var ehlers=indicator.BatchName==IndicatorName.EhlersMovingAverageDifferenceIndicator;return AverageGapOutputs(bars,Integer(o,"FastLength",ehlers?8:7),Integer(o,"SlowLength",ehlers?23:65),AverageKind(o,ehlers?2:1),ehlers?"Emad":"Ravi");}
    internal static IReadOnlyDictionary<string,double[]> AverageGapOutputs(IReadOnlyList<Bar> bars,int fast,int slow,int kind,string key,double[][]? external=null)
    {
        var input=bars.Select(b=>ReferenceFraction.FromDouble(b.Close)).ToArray();
        var f=external is null?SmoothRocBankStage(input,Math.Max(1,fast),kind):external[0].Select(ReferenceFraction.FromDouble).ToArray();
        var s=external is null?SmoothRocBankStage(input,Math.Max(1,slow),kind):external[1].Select(ReferenceFraction.FromDouble).ToArray();
        return Outputs((key,f.Select((v,i)=>s[i].Sign==0?0:(new ReferenceFraction(100)*(v-s[i])/s[i]).ToDouble()).ToArray()));
    }
}
