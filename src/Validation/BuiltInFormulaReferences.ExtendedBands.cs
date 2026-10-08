using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> ExtendedBandOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator) => ExtendedBandOutputs(bars, Math.Max(3, Integer(indicator.CreateOptions(), "Length", 100)));
    internal static IReadOnlyDictionary<string, double[]> ExtendedBandOutputs(IReadOnlyList<Bar> bars, int length)
    {
        var gain = new ReferenceFraction(2) / new ReferenceFraction(Math.Max(3, length) + 1L); var previousUpper = new ReferenceFraction(0); var previousLower = previousUpper;
        var upper = new double[bars.Count]; var middle = new double[bars.Count]; var lower = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var close = ReferenceFraction.FromDouble(bars[i].Close); if (i == 0) previousUpper = previousLower = close;
            upper[i] = (new[] { close, previousUpper }.Max() - gain * (close - previousUpper).Abs()).ToDouble();
            lower[i] = (new[] { close, previousLower }.Min() + gain * (close - previousLower).Abs()).ToDouble();
            previousUpper = ReferenceFraction.FromDouble(upper[i]); previousLower = ReferenceFraction.FromDouble(lower[i]);
            middle[i] = ((previousUpper + previousLower) / new ReferenceFraction(2)).ToDouble();
        }
        return Outputs(("UpperBand", upper), ("MiddleBand", middle), ("LowerBand", lower));
    }
}
