using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string,double[]> CenterLinearityOutputs(IReadOnlyList<Bar> bars,IBuiltInIndicator indicator)=>CenterLinearityOutputs(bars,Integer(indicator.CreateOptions(),"Length",14));
    internal static IReadOnlyDictionary<string,double[]> CenterLinearityOutputs(IReadOnlyList<Bar> bars,int length)
    {
        length=Math.Max(1,length);var zero=new ReferenceFraction(0);var prices=bars.Select(b=>ReferenceFraction.FromDouble(b.Close)).ToArray();
        var terms=prices.Select((v,i)=>RoundRocBankStage(new ReferenceFraction(i+1L)*RoundRocBankStage((i>=length?prices[i-length]:zero)-(i>0?prices[i-1]:zero)))).ToArray();
        return Outputs(("Col",terms.Select((v,i)=>{var sum=zero;for(var j=Math.Max(0,i-length+1);j<=i;j++)sum+=terms[j];return sum.ToDouble();}).ToArray()));
    }
}
