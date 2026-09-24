using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static bool HasRoundedBalanceOfPower(IBuiltInIndicator indicator) => indicator.BatchName == IndicatorName.BalanceOfPower
        && BoundedMeanKind(indicator.CreateOptions(), 3) is 1 or 2 or 3 or 6 or 7 or 8 or 9 or 10 or 11 or 12 or 13 or 14 or 15 or 16 or 17 or 18 or 19 or 20;

    internal static double[] RoundedBalanceOfPowerLine(IReadOnlyList<Bar> bars)
        => bars.Select(b => b.High == b.Low ? 0 : ((ReferenceFraction.FromDouble(b.Close) - ReferenceFraction.FromDouble(b.Open)) // NOSONAR: S1244 - Equal bounds define an exactly zero range; a nonzero range must still be evaluated.
            / (ReferenceFraction.FromDouble(b.High) - ReferenceFraction.FromDouble(b.Low))).ToDouble()).ToArray();

    internal static double[] RoundedBalanceOfPowerSignal(IReadOnlyList<Bar> bars, int length, int kind)
    {
        var raw = RoundedBalanceOfPowerLine(bars);
        var count = Array.FindIndex(raw, double.IsInfinity);
        if (count < 0) count = raw.Length;
        return RoundedBoundedStage(raw.Take(count).ToArray(), length, kind)
            .Concat(Enumerable.Repeat(double.NaN, raw.Length - count)).ToArray();
    }
}
