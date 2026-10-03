using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string,double[]> StochasticGravityOutputs(IReadOnlyList<Bar> bars,IBuiltInIndicator indicator)=>StochasticGravityOutputs(bars,Integer(indicator.CreateOptions(),"Length",8));
    internal static IReadOnlyDictionary<string,double[]> StochasticGravityOutputs(IReadOnlyList<Bar> bars,int length)
    {
        length=Math.Max(1,length);var center=new ReferenceFraction((long)length+1)/new ReferenceFraction(2);var zero=new ReferenceFraction(0);
        var gravity=bars.Select((b,i)=>{var sum=zero;var weighted=zero;for(var lag=0;lag<length&&lag<=i;lag++){var v=ReferenceFraction.FromDouble(bars[i-lag].Close);sum+=v;weighted+=new ReferenceFraction(lag+1)*v;}return sum.Sign==0?zero:RoundRocBankStage(RoundRocBankStage((zero-weighted)/sum)+center);}).ToArray();
        var ratios=gravity.Select((v,i)=>{var window=gravity.Skip(Math.Max(0,i-Math.Max(2,length)+1)).Take(Math.Min(i+1,Math.Max(2,length))).ToArray();var min=window.Min();var max=window.Max();return max.CompareTo(min)==0?zero:RoundRocBankStage((v-min)/(max-min));}).ToArray();
        var centered=ratios.Select((v,i)=>{var sum=new ReferenceFraction(4)*v;for(var lag=1;lag<=3&&lag<=i;lag++)sum+=new ReferenceFraction(4-lag)*ratios[i-lag];var smooth=RoundRocBankStage(sum/new ReferenceFraction(10));return RoundRocBankStage(new ReferenceFraction(2)*RoundRocBankStage(smooth-ReferenceFraction.FromDouble(.5)));}).ToArray();
        var trigger=centered.Select((v,i)=>Math.Max(0,Math.Min(1,(ReferenceFraction.FromDouble(.96)*RoundRocBankStage((i>0?centered[i-1]:zero)+ReferenceFraction.FromDouble(.02))).ToDouble()))).ToArray();return Outputs(("Escog",trigger));
    }
}
