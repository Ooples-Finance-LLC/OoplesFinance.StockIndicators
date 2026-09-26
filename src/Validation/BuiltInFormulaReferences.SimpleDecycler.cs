using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> SimpleDecyclerOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator, double upperPercent = .5, double lowerPercent = .5)
    {
        var highPass = HighPassStates(bars, indicator); var upper = ReferenceFraction.FromDouble(1 + upperPercent / 100); var lower = ReferenceFraction.FromDouble(1 - lowerPercent / 100);
        var middle = bars.Select((b, i) => (ReferenceFraction.FromDouble(b.Close) - highPass[i]).RoundExtendedBinary64()).ToArray();
        return Outputs(("UpperBand", middle.Select(value => (value * upper).ToDouble()).ToArray()),
            ("MiddleBand", middle.Select(value => value.ToDouble()).ToArray()), ("LowerBand", middle.Select(value => (value * lower).ToDouble()).ToArray()));
    }
}
