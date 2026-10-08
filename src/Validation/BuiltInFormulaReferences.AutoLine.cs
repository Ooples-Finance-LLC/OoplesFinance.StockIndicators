using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string,double[]> AutoLineOutputs(IReadOnlyList<Bar> bars,int length)
    {
        var deviation=PopulationDeviation(Closes(bars),Math.Max(1,length));var result=new double[bars.Count];
        for(var i=0;i<bars.Count;i++)
        {
            var previous=i==0?bars[i].Close:result[i-1];var upper=previous+deviation[i];var lower=previous-deviation[i];
            result[i]=bars[i].Close>upper||bars[i].Close<lower?bars[i].Close:previous;
        }
        return Outputs(("Al",result));
    }
}
