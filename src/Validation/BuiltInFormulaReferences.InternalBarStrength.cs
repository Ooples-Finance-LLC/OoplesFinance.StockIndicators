using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string,double[]> InternalBarStrengthOutputs(IReadOnlyList<Bar> bars,IBuiltInIndicator indicator)
        =>InternalBarStrengthOutputs(bars,Integer(indicator.CreateOptions(),"Length",14));
    internal static IReadOnlyDictionary<string,double[]> InternalBarStrengthOutputs(IReadOnlyList<Bar> bars,int length,int smooth=3)
    {
        length=Math.Max(1,length);smooth=Math.Max(1,smooth);
        var positions=bars.Select(b=>b.High==b.Low?new ReferenceFraction(0):RoundRocBankStage(new ReferenceFraction(100)*(ReferenceFraction.FromDouble(b.Close)-ReferenceFraction.FromDouble(b.Low))/(ReferenceFraction.FromDouble(b.High)-ReferenceFraction.FromDouble(b.Low)))).ToArray();
        var line=new double[bars.Count];var signal=new double[bars.Count];var prior=new ReferenceFraction(0);
        for(var i=0;i<bars.Count;i++)
        {
            var start=Math.Max(0,i-length+1);var sum=new ReferenceFraction(0);for(var j=start;j<=i;j++)sum+=positions[j];
            var value=RoundRocBankStage(sum/new ReferenceFraction(i-start+1));
            prior=RoundRocBankStage((prior*new ReferenceFraction(smooth-1L)+value*new ReferenceFraction(2))/new ReferenceFraction(smooth+1L));
            line[i]=value.ToDouble();signal[i]=prior.ToDouble();
        }
        return Outputs(("Ibs",line),("Signal",signal));
    }
}
