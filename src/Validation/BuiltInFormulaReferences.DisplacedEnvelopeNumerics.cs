using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> RoundedDisplacedEnvelope(IReadOnlyList<Bar> bars, object options)
    {
        var average = RoundedBoundedStage(Closes(bars), Integer(options, "Length1", 9), BoundedMeanKind(options, 3));
        var delay = Integer(options, "Length2", 13);
        var fraction = ReferenceFraction.FromDouble(Number(options, .5, "Pct")) / new ReferenceFraction(100);
        var middle = bars.Select((_, i) => i < delay ? 0 : average[i - delay]).ToArray();
        var upper = middle.Select(v => (ReferenceFraction.FromDouble(v) * (new ReferenceFraction(1) + fraction)).ToDouble()).ToArray();
        var lower = middle.Select(v => (ReferenceFraction.FromDouble(v) * (new ReferenceFraction(1) - fraction)).ToDouble()).ToArray();
        return Outputs(("UpperBand", upper), ("MiddleBand", middle), ("LowerBand", lower));
    }
}
