using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string,double[]> NormalizedVigorOutputs(IReadOnlyList<Bar> bars,IBuiltInIndicator indicator)
    {var o=indicator.CreateOptions();return NormalizedVigorOutputs(bars,Integer(o,"Length",10),AverageKind(o,0));}
    internal static IReadOnlyDictionary<string,double[]> NormalizedVigorOutputs(IReadOnlyList<Bar> bars,int length,int kind,double[][]? external=null)
    {
        length=Math.Max(1,length);
        ReferenceFraction[] Smooth(ReferenceFraction[] values)
        {
            if(kind!=0) return SmoothRocBankStage(values,length,kind);
            // Direct triangular dot product is independent of the engine's two-box convolution.
            var weights=Enumerable.Range(0,length).Select(j=>Math.Min(j+1,length-j)).ToArray();var divisor=weights.Aggregate(0L,(a,b)=>a+b);
            return values.Select((v,i)=> {var sum=new ReferenceFraction(0);for(var lag=0;lag<length && lag<=i;lag++)sum+=new ReferenceFraction(weights[lag])*values[i-lag];return RoundRocBankStage(sum/new ReferenceFraction(divisor));}).ToArray();
        }
        var body=bars.Select(b=>RoundRocBankStage(ReferenceFraction.FromDouble(b.Close)-ReferenceFraction.FromDouble(b.Open))).ToArray();
        var range=bars.Select(b=>RoundRocBankStage(ReferenceFraction.FromDouble(b.High)-ReferenceFraction.FromDouble(b.Low))).ToArray();
        var b=external is null?Smooth(body):external[0].Select(ReferenceFraction.FromDouble).ToArray();
        var r=external is null?Smooth(range):external[1].Select(ReferenceFraction.FromDouble).ToArray();
        var values=b.Select((v,i)=> {var numerator=new ReferenceFraction(0);var denominator=new ReferenceFraction(0);for(var j=Math.Max(0,i-length+1);j<=i;j++){numerator+=b[j];denominator+=r[j];}return denominator.Sign==0?new ReferenceFraction(0):RoundRocBankStage(new ReferenceFraction(100)*numerator/denominator);}).ToArray();
        var signal=external is null?Smooth(values):external[2].Select(ReferenceFraction.FromDouble).ToArray();return Outputs(("Nrvi",values.Select(v=>v.ToDouble()).ToArray()),("Signal",signal.Select(v=>v.ToDouble()).ToArray()));
    }
}
