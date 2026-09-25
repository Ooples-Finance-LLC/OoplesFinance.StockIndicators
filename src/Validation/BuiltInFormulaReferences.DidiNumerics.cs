using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> RoundedDidi(IReadOnlyList<Bar> bars, object options)
    {
        var values = Closes(bars);
        var kind = BoundedMeanKind(options, 1);
        var middle = RoundedBoundedStage(values, Integer(options, "MediumLength", 8), kind);
        var first = RoundedBoundedStage(values, Integer(options, "ShortLength", 3), kind);
        var last = RoundedBoundedStage(values, Integer(options, "LongLength", 20), kind);
        double[] Ratio(double[] numerator) => numerator.Select((v, i) => middle[i] == 0 ? 0 :
            (ReferenceFraction.FromDouble(v) / ReferenceFraction.FromDouble(middle[i])).ToDouble()).ToArray();
        return Outputs(("Curta", Ratio(first)), ("Media", middle.Select(v => v == 0 ? 0d : 1).ToArray()), ("Longa", Ratio(last)));
    }
}
