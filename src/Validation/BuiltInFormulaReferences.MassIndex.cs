using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string,double[]> MassIndexOutputs(IReadOnlyList<Bar> bars,IBuiltInIndicator indicator)
    {
        var o=indicator.CreateOptions();var first=Integer(o,"EmaLength",21);return MassIndexOutputs(bars,first,first,Integer(o,"SumLength",Integer(o,"Length",25)),9,AverageKind(o,3));
    }
    internal static IReadOnlyDictionary<string,double[]> MassIndexOutputs(IReadOnlyList<Bar> bars,int first,int second,int length,int signal,int kind,double[][]? external=null)
    {
        length=Math.Max(1,length);
        var ranges=bars.Select(b=>RoundRocBankStage(ReferenceFraction.FromDouble(b.High)-ReferenceFraction.FromDouble(b.Low))).ToArray();
        var a=external is null?SmoothRocBankStage(ranges,Math.Max(1,first),kind):external[0].Select(ReferenceFraction.FromDouble).ToArray();
        var secondValues=external is null?SmoothRocBankStage(a,Math.Max(1,second),kind):external[1].Select(ReferenceFraction.FromDouble).ToArray();
        var ratio=a.Select((v,i)=>secondValues[i].Sign==0?new ReferenceFraction(0):RoundRocBankStage(v/secondValues[i])).ToArray();
        var line=ratio.Select((_,i)=>{var sum=new ReferenceFraction(0);for(var j=Math.Max(0,i-length+1);j<=i;j++)sum+=ratio[j];return RoundRocBankStage(sum);}).ToArray();
        var sig=external is null?SmoothRocBankStage(line,Math.Max(1,signal),kind):external[2].Select(ReferenceFraction.FromDouble).ToArray();
        return Outputs(("Mi",line.Select(v=>v.ToDouble()).ToArray()),("Signal",sig.Select(v=>v.ToDouble()).ToArray()));
    }
}
