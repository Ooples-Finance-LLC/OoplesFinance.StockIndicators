using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string,double[]> SimplePriceZoneOutputs(IReadOnlyList<Bar> bars,int length)
    {
        var changes=bars.Select((b,i)=>i==0?new ReferenceFraction(0):RoundRocBankStage(ReferenceFraction.FromDouble(b.Close)-ReferenceFraction.FromDouble(bars[i-1].Close))).ToArray();var result=new double[bars.Count];
        for(var i=0;i<bars.Count;i++)
        {
            var net=new ReferenceFraction(0);var travel=new ReferenceFraction(0);
            for(var j=Math.Max(0,i-Math.Max(1,length)+1);j<=i;j++){net+=changes[j];travel+=changes[j].Abs();}
            result[i]=travel.Sign==0?0:(new ReferenceFraction(100)*net/travel).ToDouble();
        }
        return Outputs(("Spz",result));
    }
}
