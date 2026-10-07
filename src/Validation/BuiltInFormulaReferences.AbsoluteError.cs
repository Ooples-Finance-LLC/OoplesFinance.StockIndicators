using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string,double[]> AbsoluteErrorOutputs(IReadOnlyList<Bar> bars,int length)
    {
        var errors=new ReferenceFraction[bars.Count];var result=new double[bars.Count];var prediction=bars.Count==0?new ReferenceFraction(0):ReferenceFraction.FromDouble(bars[0].Close);
        for(var i=0;i<bars.Count;i++)
        {
            var price=ReferenceFraction.FromDouble(bars[i].Close);errors[i]=RoundRocBankStage(price-prediction);
            var first=Math.Max(0,i-Math.Max(1,length)+1);var net=new ReferenceFraction(0);var travel=new ReferenceFraction(0);
            for(var j=first;j<=i;j++){net+=errors[j];travel+=errors[j].Abs();}
            result[i]=travel.Sign==0?0:(net/travel).ToDouble();
            var mean=RoundRocBankStage(travel/new ReferenceFraction(i-first+1));
            var correction=RoundRocBankStage(mean*ReferenceFraction.FromDouble(result[i]));prediction=RoundRocBankStage(price+correction);
        }
        return Outputs(("Aaen",result));
    }
}
