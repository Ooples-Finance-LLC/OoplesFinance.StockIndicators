using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string,double[]> AutoDriftOutputs(IReadOnlyList<Bar> bars,int length)
    {
        length=Math.Max(1,length);var deviation=PopulationDeviation(Closes(bars),length);var held=new ReferenceFraction[bars.Count];var output=new double[bars.Count];
        var gain=ReferenceFraction.FromDouble(1d/(2L*length));
        for(var i=0;i<bars.Count;i++)
        {
            var price=ReferenceFraction.FromDouble(bars[i].Close);var seed=ReferenceFraction.FromDouble(Math.Round(bars[i].Close));var previous=i==0?seed:held[i-1];
            var prior=(long)i>length?held[(int)((long)i-length-1)]:seed;var width=ReferenceFraction.FromDouble(deviation[i]);
            var upper=RoundRocBankStage(previous+width);var lower=RoundRocBankStage(previous-width);
            var drift=RoundRocBankStage(RoundRocBankStage(previous-prior)*gain);
            held[i]=price.CompareTo(upper)>0||price.CompareTo(lower)<0?price:RoundRocBankStage(previous+drift);output[i]=held[i].ToDouble();
        }
        return Outputs(("Alwd",output));
    }
}
