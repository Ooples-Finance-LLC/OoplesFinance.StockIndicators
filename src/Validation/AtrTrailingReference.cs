using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static class AtrTrailingReference
{
    private static ReferenceFraction F(double v) => ReferenceFraction.FromDouble(v);

    private static ReferenceFraction R(ReferenceFraction v) => v.RoundExtendedBinary64();

    internal static double?[][] Values(
        IReadOnlyList<Bar> bars,
        int period,
        double multiplier,
        AtrTrailBasis basis
    )
    {
        var output = Enumerable.Range(0, 3).Select(_ => new double?[bars.Count]).ToArray();
        var seed = F(0);
        var atr = seed;
        var upper = seed;
        var lower = seed;
        var bullish = true;
        for (var i = 1; i < bars.Count; i++)
        {
            var previous = F(bars[i - 1].Close);
            var close = F(bars[i].Close);
            var high = F(bars[i].High);
            var low = F(bars[i].Low);
            var tr = R(new[] { high - low, (high - previous).Abs(), (low - previous).Abs() }.Max());
            if (i <= period)
                seed += tr;
            if (i < period)
                continue;
            atr = R(i == period ? seed / F(period) : (atr * F(period - 1) + tr) / F(period));
            var mid = R((high + low) / F(2));
            var width = R(atr * F(multiplier));
            var ue = R(
                (
                    basis == AtrTrailBasis.Close ? close
                    : basis == AtrTrailBasis.HighLow ? high
                    : mid
                ) + width
            );
            var le = R(
                (
                    basis == AtrTrailBasis.Close ? close
                    : basis == AtrTrailBasis.HighLow ? low
                    : mid
                ) - width
            );
            if (i == period)
            {
                bullish = close.CompareTo(basis == AtrTrailBasis.Midpoint ? mid : previous) >= 0;
                upper = ue;
                lower = le;
            }
            if (ue.CompareTo(upper) < 0 || previous.CompareTo(upper) > 0)
                upper = ue;
            if (le.CompareTo(lower) > 0 || previous.CompareTo(lower) < 0)
                lower = le;
            bullish = close.CompareTo(bullish ? lower : upper) > 0;
            output[0][i] = output[bullish ? 2 : 1][i] = (bullish ? lower : upper).ToDouble();
        }
        return output;
    }
}
