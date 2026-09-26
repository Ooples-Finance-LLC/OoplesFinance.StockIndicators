using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> RoundedMaeBands(IReadOnlyList<Bar> bars, object options)
    {
        var middle = RoundedBoundedStage(Closes(bars), Integer(options, "Length", 14), BoundedMeanKind(options, 1));
        var factor = ReferenceFraction.FromDouble(Number(options, 1, "StdDevFactor"));
        var upper = new double[bars.Count]; var lower = new double[bars.Count];
        var errors = new ReferenceFraction(0);
        for (var i = 0; i < bars.Count; i++)
        {
            var center = ReferenceFraction.FromDouble(middle[i]);
            errors += (ReferenceFraction.FromDouble(bars[i].Close) - center).Abs();
            var spread = errors / new ReferenceFraction(i + 1) * factor;
            upper[i] = (center + spread).ToDouble();
            lower[i] = (center - spread).ToDouble();
        }
        return Outputs(("UpperBand", upper), ("MiddleBand", middle), ("LowerBand", lower));
    }
}
