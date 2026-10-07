using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static class ChandelierReference
{
    internal static double[][] Calculate(
        IReadOnlyList<Bar> bars,
        int period,
        double multiplier,
        ChandelierExitSelection selection,
        bool zeroFloorHigh
    )
    {
        var result = Enumerable.Range(0, 4).Select(_ => new double[bars.Count]).ToArray();
        var seed = new ReferenceFraction(0);
        var average = seed;
        for (var i = 1; i < bars.Count; i++)
        {
            var h = ReferenceFraction.FromDouble(bars[i].High);
            var l = ReferenceFraction.FromDouble(bars[i].Low);
            var c = ReferenceFraction.FromDouble(bars[i - 1].Close);
            var range = new[] { h - l, (h - c).Abs(), (l - c).Abs() }.Max().RoundExtendedBinary64();
            if (i <= period)
                seed += range;
            if (i < period)
                continue;
            average = (
                i == period
                    ? seed / new ReferenceFraction(period)
                    : (average * new ReferenceFraction(period - 1) + range)
                        / new ReferenceFraction(period)
            ).RoundExtendedBinary64();
            var window = bars.Skip(i - period + 1).Take(period).ToArray();
            var offset = average * ReferenceFraction.FromDouble(multiplier);
            if (selection != ChandelierExitSelection.Short)
            {
                var high = window.Max(b => b.High);
                result[0][i] = (
                    ReferenceFraction.FromDouble(zeroFloorHigh ? Math.Max(0, high) : high) - offset
                ).ToDouble();
                result[2][i] = 1;
            }
            if (selection != ChandelierExitSelection.Long)
            {
                result[1][i] = (
                    ReferenceFraction.FromDouble(window.Min(b => b.Low)) + offset
                ).ToDouble();
                result[3][i] = 1;
            }
        }
        return result;
    }
}
