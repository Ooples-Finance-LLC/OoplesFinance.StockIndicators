using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static class WindowChoppinessReference
{
    internal static double?[] Calculate(IReadOnlyList<Bar> bars, int period)
    {
        var result = new double?[bars.Count];
        for (var i = period; i < bars.Count; i++)
        {
            var intervals = Enumerable
                .Range(i - period + 1, period)
                .Select(j =>
                    (
                        High: Math.Max(bars[j].High, bars[j - 1].Close),
                        Low: Math.Min(bars[j].Low, bars[j - 1].Close)
                    )
                )
                .ToArray();
            var sum = intervals.Aggregate(
                new ReferenceFraction(0),
                (s, v) =>
                    s + ReferenceFraction.FromDouble(v.High) - ReferenceFraction.FromDouble(v.Low)
            );
            var span =
                ReferenceFraction.FromDouble(intervals.Max(v => v.High))
                - ReferenceFraction.FromDouble(intervals.Min(v => v.Low));
            if (span.Sign == 0)
                continue;
            if (sum.Sign == 0)
            {
                result[i] = double.NegativeInfinity;
                continue;
            }
            var ratio = sum / span;
            var relative = ratio - new ReferenceFraction(1);
            double logarithm;
            if (
                relative.Abs().CompareTo(new ReferenceFraction(1) / new ReferenceFraction(1024)) < 0
            )
            {
                // Rational log1p series preserves changes below the fixed-point oracle's precision.
                var total = new ReferenceFraction(0);
                var power = relative;
                for (var n = 1; n <= 20; n++)
                {
                    total +=
                        (n % 2 == 1 ? power : new ReferenceFraction(-1) * power)
                        / new ReferenceFraction(n);
                    power *= relative;
                }
                logarithm = total.ToDouble();
            }
            else
                logarithm = ratio.LogToDouble();
            result[i] = 100 * (logarithm / new ReferenceFraction(period).LogToDouble());
        }
        return result;
    }
}
