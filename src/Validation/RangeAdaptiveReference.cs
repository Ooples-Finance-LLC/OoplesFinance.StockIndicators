using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static class RangeAdaptiveReference
{
    private static ReferenceFraction F(double v) => ReferenceFraction.FromDouble(v);

    private static ReferenceFraction R(ReferenceFraction v) => v.RoundExtendedBinary64();

    internal static double[] Fractal(IReadOnlyList<Bar> bars, int period)
    {
        var result = new double[bars.Count];
        var prior = F(0);
        for (var i = 0; i < bars.Count; i++)
        {
            var window = bars.Skip(Math.Max(0, i - period + 1))
                .Take(Math.Min(i + 1, period))
                .Select(b => b.Close)
                .ToArray();
            if (window.Length < period)
                prior = R(window.Aggregate(F(0), (s, p) => s + F(p)) / F(window.Length));
            else
            {
                var half = period / 2;
                var first = window.Take(half).ToArray();
                var second = window.Skip(half).ToArray();
                var a = (F(window.Max()) - F(window.Min())) / F(period) + F(double.Epsilon);
                var b =
                    (F(first.Max()) - F(first.Min()) + F(second.Max()) - F(second.Min())) / F(half)
                    + F(double.Epsilon);
                var dimension = Math.Log((b / a).ToDouble()) / Math.Log(2);
                var gain = F(Math.Clamp(Math.Exp(-4.6 * (dimension - 1)), .01, 1));
                prior = R(gain * F(bars[i].Close) + (F(1) - gain) * prior);
            }
            result[i] = prior.ToDouble();
        }
        return result;
    }

    internal static double[] Deviation(IReadOnlyList<Bar> bars, int period, double scale)
    {
        var result = new double[bars.Count];
        if (bars.Count == 0)
            return result;
        var a = Math.Exp(-1.414 * Math.PI / (.5 * period));
        var b = 2 * a * Math.Cos(1.414 * Math.PI / (.5 * period));
        var c = -a * a;
        var c1 = F(1 - b - c);
        var c2 = F(b);
        var c3 = F(c);
        var filters = Enumerable.Repeat(F(0), bars.Count).ToArray();
        var zeros = filters.ToArray();
        var prior = F(bars[0].Close);
        result[0] = bars[0].Close;
        for (var i = 1; i < bars.Count; i++)
        {
            zeros[i] = R(F(bars[i].Close) - prior);
            filters[i] = R(
                c1 * (zeros[i] + zeros[i - 1]) / F(2)
                    + c2 * filters[i - 1]
                    + c3 * (i > 1 ? filters[i - 2] : F(0))
            );
            var sum = filters
                .Skip(Math.Max(1, i - period + 1))
                .Take(Math.Min(i, period))
                .Aggregate(F(0), (s, f) => s + f * f);
            var normalized =
                sum.Sign == 0 ? 0 : (F(period) * filters[i] * filters[i] / sum).SqrtToDouble();
            var gain = F(Math.Clamp(scale * normalized * 5 / period, .1, 1));
            prior = R(gain * F(bars[i].Close) + (F(1) - gain) * prior);
            result[i] = prior.ToDouble();
        }
        return result;
    }
}
