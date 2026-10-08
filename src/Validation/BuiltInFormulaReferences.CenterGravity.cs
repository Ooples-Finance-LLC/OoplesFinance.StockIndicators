using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string,double[]> CenterGravityOutputs(IReadOnlyList<Bar> bars,IBuiltInIndicator indicator)=>CenterGravityOutputs(bars,Integer(indicator.CreateOptions(),"Length",10));
    internal static IReadOnlyDictionary<string,double[]> CenterGravityOutputs(IReadOnlyList<Bar> bars,int length)
    {
        length=Math.Max(1,length);var center=new ReferenceFraction((long)length+1)/new ReferenceFraction(2);
        return Outputs(("Ecog",bars.Select((b,i)=>{var sum=new ReferenceFraction(0);var weighted=new ReferenceFraction(0);for(var lag=0;lag<length&&lag<=i;lag++){var value=ReferenceFraction.FromDouble(bars[i-lag].Close);sum+=value;weighted+=new ReferenceFraction(lag+1)*value;}return sum.Sign==0?0:(RoundRocBankStage((new ReferenceFraction(0)-weighted)/sum)+center).ToDouble();}).ToArray()));
    }
}
