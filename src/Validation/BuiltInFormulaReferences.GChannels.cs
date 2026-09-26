using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> GChannelOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator) => GChannelOutputs(bars, Math.Max(2, Integer(indicator.CreateOptions(), "Length", 100)));
    internal static IReadOnlyDictionary<string, double[]> GChannelOutputs(IReadOnlyList<Bar> bars, int length)
    {
        length = Math.Max(2, length); var previousUpper = new ReferenceFraction(0); var previousLower = new ReferenceFraction(0);
        var upper = new double[bars.Count]; var middle = new double[bars.Count]; var lower = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var close = ReferenceFraction.FromDouble(bars[i].Close); var contraction = (previousUpper - previousLower) / new ReferenceFraction(length);
            upper[i] = ((close.CompareTo(previousUpper) > 0 ? close : previousUpper) - contraction).ToDouble();
            lower[i] = ((close.CompareTo(previousLower) < 0 ? close : previousLower) + contraction).ToDouble();
            previousUpper = ReferenceFraction.FromDouble(upper[i]); previousLower = ReferenceFraction.FromDouble(lower[i]);
            middle[i] = ((previousUpper + previousLower) / new ReferenceFraction(2)).ToDouble();
        }
        return Outputs(("UpperBand", upper), ("MiddleBand", middle), ("LowerBand", lower));
    }
}
