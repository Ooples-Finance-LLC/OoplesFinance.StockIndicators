using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static bool HasRoundedHighLowIndex(IBuiltInIndicator indicator) => indicator.BatchName == IndicatorName.HighLowIndex
        && BoundedMeanKind(indicator.CreateOptions(), 3) is 1 or 2 or 3 or 6 or 7 or 8 or 9 or 10 or 11 or 12 or 13 or 14 or 15 or 16 or 17 or 18 or 19;

    internal static double[] RoundedHighLowIndex(IReadOnlyList<Bar> bars, int length, int kind)
    {
        var high = bars.Select((_, i) => bars.Skip(Math.Max(0, i - length + 1)).Take(Math.Min(length, i + 1)).Max(b => b.High)).ToArray();
        var low = bars.Select((_, i) => bars.Skip(Math.Max(0, i - length + 1)).Take(Math.Min(length, i + 1)).Min(b => b.Low)).ToArray();
        var up = high.Select((v, i) => v > (i == 0 ? 0 : high[i - 1])).ToArray();
        var down = low.Select((v, i) => v < (i == 0 ? 0 : low[i - 1])).ToArray();
        var raw = bars.Select((_, i) => {
            var first = Math.Max(0, i - length + 1);
            var count = Math.Min(length, i + 1);
            var advances = up.Skip(first).Take(count).Count(v => v);
            var declines = down.Skip(first).Take(count).Count(v => v);
            return advances + declines == 0 ? 0 : (new ReferenceFraction(100 * (long)advances)
                / new ReferenceFraction(advances + (long)declines)).ToDouble();
        }).ToArray();
        return RoundedBoundedStage(raw, length, kind);
    }
}
