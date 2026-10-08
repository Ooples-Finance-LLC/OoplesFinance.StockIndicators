using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static double[] RoundedMoveTracker(IReadOnlyList<Bar> bars, bool signal)
    {
        var movement = RoundedLaggedDifference(bars, 1, false);
        if (!signal) return movement;
        return movement.Select((v, i) => i == 0 ? 0
            : double.IsInfinity(v) || double.IsInfinity(movement[i - 1]) ? double.NaN
            : (ReferenceFraction.FromDouble(v) - ReferenceFraction.FromDouble(movement[i - 1])).ToDouble()).ToArray();
    }

    internal static double[] RoundedSimpleReturns(IReadOnlyList<Bar> bars, int length)
        => bars.Select((b, i) => i < length || bars[i - length].Close == 0 ? 0
            : (ReferenceFraction.FromDouble(b.Close) / ReferenceFraction.FromDouble(bars[i - length].Close)
                - new ReferenceFraction(1)).ToDouble()).ToArray();

    internal static double[] RoundedLaggedDifference(IReadOnlyList<Bar> bars, int length, bool volume)
    {
        var values = bars.Select(b => volume ? b.Volume : b.Close).ToArray();
        return Enumerable.Range(0, values.Length).Select(i => i < length ? 0
            : (ReferenceFraction.FromDouble(values[i]) - ReferenceFraction.FromDouble(values[i - length])).ToDouble()).ToArray();
    }

    internal static double[] RoundedMarketFacilitation(IReadOnlyList<Bar> bars)
        => bars.Select(b => b.Volume == 0 ? 0 : ((ReferenceFraction.FromDouble(b.High)
            - ReferenceFraction.FromDouble(b.Low)) / ReferenceFraction.FromDouble(b.Volume)).ToDouble()).ToArray();

    internal static bool HasRoundedMomentum(IBuiltInIndicator indicator) => indicator.BatchName == IndicatorName.MomentumOscillator
        && BoundedMeanKind(indicator.CreateOptions(), 2) is 1 or 2 or 3 or 6 or 7 or 8 or 9 or 10 or 11 or 12 or 13 or 14 or 15 or 16 or 17 or 18 or 19 or 20 or 21;

    internal static double[] RoundedMomentum(IReadOnlyList<Bar> bars, int length)
        => Enumerable.Range(0, bars.Count).Select(i => i < length || bars[i - length].Close == 0 ? 0
            : (new ReferenceFraction(100) * ReferenceFraction.FromDouble(bars[i].Close)
                / ReferenceFraction.FromDouble(bars[i - length].Close)).ToDouble()).ToArray();

    internal static double[] RoundedMomentumSignal(IReadOnlyList<Bar> bars, int length, int kind)
    {
        var raw = RoundedMomentum(bars, length);
        var count = Array.FindIndex(raw, double.IsInfinity);
        if (count < 0) count = raw.Length;
        var prefix = RoundedBoundedStage(raw.Take(count).ToArray(), length, kind);
        return prefix.Concat(Enumerable.Repeat(double.NaN, raw.Length - count)).ToArray();
    }
}
