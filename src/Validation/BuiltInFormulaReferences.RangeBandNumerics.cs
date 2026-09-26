using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> RoundedQuartileBands(IReadOnlyList<Bar> bars, int length, double multiplier)
    {
        var upper = new double[bars.Count]; var middle = new double[bars.Count]; var lower = new double[bars.Count];
        var factor = ReferenceFraction.FromDouble(multiplier);
        for (var i = 0; i < bars.Count; i++)
        {
            var values = bars.Skip(Math.Max(0, i - length + 1)).Take(Math.Min(i + 1, length)).Select(b => b.Close).OrderBy(v => v).ToArray();
            var first = ReferenceFraction.FromDouble(values[(values.Length + 3) / 4 - 1]);
            var third = ReferenceFraction.FromDouble(values[(3 * values.Length + 3) / 4 - 1]);
            upper[i] = (third + factor * (third - first)).ToDouble();
            lower[i] = (first - factor * (third - first)).ToDouble();
            middle[i] = ((first + third) / new ReferenceFraction(2)).ToDouble();
        }
        return Outputs(("UpperBand", upper), ("MiddleBand", middle), ("LowerBand", lower));
    }

    internal static IReadOnlyDictionary<string, double[]> RoundedRangeBands(IReadOnlyList<Bar> bars, object options)
    {
        var length = Integer(options, "Length", 14);
        var middle = RoundedBoundedStage(Closes(bars), length, BoundedMeanKind(options, 1));
        var factor = ReferenceFraction.FromDouble(Number(options, 1, "StdDevFactor"));
        var upper = new double[bars.Count]; var lower = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var values = middle.Skip(Math.Max(0, i - length + 1)).Take(Math.Min(i + 1, length)).ToArray();
            var spread = (ReferenceFraction.FromDouble(values.Max()) - ReferenceFraction.FromDouble(values.Min())) * factor;
            upper[i] = (ReferenceFraction.FromDouble(middle[i]) + spread).ToDouble();
            lower[i] = (ReferenceFraction.FromDouble(middle[i]) - spread).ToDouble();
        }
        return Outputs(("UpperBand", upper), ("MiddleBand", middle), ("LowerBand", lower));
    }
}
