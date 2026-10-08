using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> FractalBandOutputs(IReadOnlyList<Bar> bars)
    {
        var upper = new double[bars.Count]; var lower = new double[bars.Count]; var middle = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var confirmedHigh = i >= 4 && Enumerable.Range(i - 4, 5).Where(j => j != i - 2).All(j => bars[j].High < bars[i - 2].High);
            var confirmedLow = i >= 4 && Enumerable.Range(i - 4, 5).Where(j => j != i - 2).All(j => bars[j].Low > bars[i - 2].Low);
            upper[i] = confirmedHigh ? bars[i - 2].High : i == 0 ? 0 : upper[i - 1]; lower[i] = confirmedLow ? bars[i - 2].Low : i == 0 ? 0 : lower[i - 1];
            middle[i] = ((ReferenceFraction.FromDouble(upper[i]) + ReferenceFraction.FromDouble(lower[i])) / new ReferenceFraction(2)).ToDouble();
        }
        return Outputs(("UpperBand", upper), ("MiddleBand", middle), ("LowerBand", lower));
    }
}
