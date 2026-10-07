using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static class TrueRangeRatioReference
{
    private static ReferenceFraction R(double v) => ReferenceFraction.FromDouble(v);

    internal static double[][] Vortex(IReadOnlyList<Bar> bars, int period)
    {
        var result = Enumerable.Range(0, 4).Select(_ => new double[bars.Count]).ToArray();
        for (var i = period; i < bars.Count; i++)
        {
            var range = new ReferenceFraction(0);
            var positive = range;
            var negative = range;
            for (var j = i - period + 1; j <= i; j++)
            {
                var h = R(bars[j].High);
                var l = R(bars[j].Low);
                var c = R(bars[j - 1].Close);
                range += new[] { h - l, (h - c).Abs(), (l - c).Abs() }.Max();
                positive += (h - R(bars[j - 1].Low)).Abs();
                negative += (l - R(bars[j - 1].High)).Abs();
            }
            if (range.Sign == 0)
                continue;
            result[0][i] = (positive / range).ToDouble();
            result[1][i] = (negative / range).ToDouble();
            result[2][i] = result[3][i] = 1;
        }
        return result;
    }

    internal static double?[] Ultimate(IReadOnlyList<Bar> bars, int[] periods, bool zero)
    {
        var result = new double?[bars.Count];
        for (var i = periods[2]; i < bars.Count; i++)
        {
            var weighted = new ReferenceFraction(0);
            var present = true;
            for (var k = 0; k < 3; k++)
            {
                var pressure = new ReferenceFraction(0);
                var range = pressure;
                for (var j = i - periods[k] + 1; j <= i; j++)
                {
                    var h = R(bars[j].High);
                    var l = R(bars[j].Low);
                    var c = R(bars[j - 1].Close);
                    var low = new[] { l, c }.Min();
                    pressure += R(bars[j].Close) - low;
                    range += zero
                        ? new[] { h - l, (h - c).Abs(), (l - c).Abs() }.Max()
                        : new[] { h, c }.Max() - low;
                }
                if (range.Sign == 0)
                {
                    if (!zero)
                        present = false;
                    continue;
                }
                weighted += new ReferenceFraction(4 >> k) * pressure / range;
            }
            if (present)
                result[i] = (
                    new ReferenceFraction(100) * weighted / new ReferenceFraction(7)
                ).ToDouble();
        }
        return result;
    }
}
