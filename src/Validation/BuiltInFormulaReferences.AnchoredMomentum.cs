using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string,double[]> AnchoredMomentumOutputs(IReadOnlyList<Bar> bars,IBuiltInIndicator indicator)
    {var o=indicator.CreateOptions();return AnchoredMomentumOutputs(bars,Integer(o,"Length",10),Integer(o,"SmoothLength",7),Integer(o,"SignalLength",8),AverageKind(o,3));}
    internal static IReadOnlyDictionary<string,double[]> AnchoredMomentumOutputs(IReadOnlyList<Bar> bars,int momentum,int smoothing,int signal,int kind,double[]? external=null)
    {
        var length=momentum<1?2:momentum>=265?530:momentum*2+1;signal=Math.Max(1,signal);
        var input=bars.Select(b=>ReferenceFraction.FromDouble(b.Close)).ToArray();
        ReferenceFraction[] Partial(ReferenceFraction[] values,int period)=>values.Select((v,i)=>{var sum=new ReferenceFraction(0);var start=Math.Max(0,i-period+1);for(var j=start;j<=i;j++)sum+=values[j];return RoundRocBankStage(sum/new ReferenceFraction(i-start+1));}).ToArray();
        var anchors=Partial(input,length);var smooth=external is null?SmoothRocBankStage(input,Math.Max(1,smoothing),kind):external.Select(ReferenceFraction.FromDouble).ToArray();
        var values=smooth.Select((v,i)=>anchors[i].Sign==0?new ReferenceFraction(0):RoundRocBankStage(new ReferenceFraction(100)*(v-anchors[i])/anchors[i])).ToArray();return Outputs(("Amom",values.Select(v=>v.ToDouble()).ToArray()),("Signal",Partial(values,signal).Select(v=>v.ToDouble()).ToArray()));
    }
}
