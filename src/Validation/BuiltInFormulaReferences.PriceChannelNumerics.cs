using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static bool HasRoundedEnvelope(IBuiltInIndicator indicator) => indicator.BatchName == IndicatorName.MovingAverageEnvelope
        && BoundedMeanKind(indicator.CreateOptions(), 1) is 1 or 2 or 3 or 6 or 7 or 8 or 9 or 10 or 11 or 12 or 13 or 14 or 15 or 16 or 17 or 18 or 19;

    internal static IReadOnlyDictionary<string, double[]> RoundedEnvelope(IReadOnlyList<Bar> bars, int length, int kind, double fraction)
    {
        var channel = RoundedPriceChannel(bars, length, kind, fraction);
        return Outputs(("MiddleBand", channel["MiddleChannel"]), ("UpperBand", channel["UpperChannel"]), ("LowerBand", channel["LowerChannel"]));
    }

    internal static bool HasRoundedPriceChannel(IBuiltInIndicator indicator) => indicator.BatchName == IndicatorName.PriceChannel
        && BoundedMeanKind(indicator.CreateOptions(), 3) is 1 or 2 or 3 or 6 or 7 or 8 or 9 or 10 or 11 or 12 or 13 or 14 or 15 or 16 or 17 or 18 or 19;

    internal static IReadOnlyDictionary<string, double[]> RoundedPriceChannel(IReadOnlyList<Bar> bars, int length, int kind, double fraction)
    {
        var center = RoundedBoundedStage(bars.Select(b => b.Close).ToArray(), length, kind);
        var width = ReferenceFraction.FromDouble(fraction);
        double[] Band(int direction) => center.Select(value => (ReferenceFraction.FromDouble(value)
            * (new ReferenceFraction(1) + new ReferenceFraction(direction) * width)).ToDouble()).ToArray();
        return Outputs(("MiddleChannel", center), ("UpperChannel", Band(1)), ("LowerChannel", Band(-1)));
    }
}
