using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static bool HasRoundedObv(IBuiltInIndicator indicator) => indicator.BatchName == IndicatorName.OnBalanceVolume
        && BoundedMeanKind(indicator.CreateOptions(), 3) is 1 or 2 or 3 or 6 or 7 or 8 or 9 or 10 or 11 or 12 or 13 or 14 or 15 or 16 or 17 or 18 or 19 or 20 or 21;

    internal static double[] RoundedObv(IReadOnlyList<Bar> bars)
    {
        var sum = new ReferenceFraction(0);
        return bars.Select((b, i) => {
            var previous = i == 0 ? new ReferenceFraction(0) : ReferenceFraction.FromDouble(bars[i - 1].Close);
            var direction = (ReferenceFraction.FromDouble(b.Close) - previous).Sign;
            sum += new ReferenceFraction(direction) * ReferenceFraction.FromDouble(b.Volume);
            return sum.ToDouble();
        }).ToArray();
    }

    internal static double[] RoundedObvSignal(IReadOnlyList<Bar> bars, int length, int kind)
    {
        var raw = RoundedObv(bars);
        var count = Array.FindIndex(raw, double.IsInfinity);
        if (count < 0) count = raw.Length;
        return RoundedBoundedStage(raw.Take(count).ToArray(), length, kind)
            .Concat(Enumerable.Repeat(double.NaN, raw.Length - count)).ToArray();
    }
}
