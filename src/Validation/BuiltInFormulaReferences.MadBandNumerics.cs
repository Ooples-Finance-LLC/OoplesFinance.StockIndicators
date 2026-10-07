using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> RoundedMadBands(IReadOnlyList<Bar> bars, object options)
    {
        var length = Integer(options, "Length", 20);
        var middle = RoundedBoundedStage(Closes(bars), length, BoundedMeanKind(options, 1));
        var factor = ReferenceFraction.FromDouble(Number(options, 2, "StdDevFactor"));
        var upper = new double[bars.Count]; var lower = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var values = bars.Skip(Math.Max(0, i - length + 1)).Take(Math.Min(i + 1, length))
                .Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray();
            var n = new ReferenceFraction(values.Length);
            var mean = values.Aggregate(new ReferenceFraction(0), (sum, v) => sum + v) / n;
            var deviation = (values.Aggregate(new ReferenceFraction(0), (sum, v) => sum + (v - mean).Abs()) / n).ToDouble();
            var spread = ReferenceFraction.FromDouble(deviation) * factor;
            upper[i] = (ReferenceFraction.FromDouble(middle[i]) + spread).ToDouble();
            lower[i] = (ReferenceFraction.FromDouble(middle[i]) - spread).ToDouble();
        }
        return Outputs(("UpperBand", upper), ("MiddleBand", middle), ("LowerBand", lower));
    }
}
