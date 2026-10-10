using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

// Deliberately straightforward complete-window references. No kernel, monotonic
// deque, FMA fast path, snapshot implementation, or production state is called.
internal static class BuilderCpuReferences
{
    private static ReferenceFraction Q(double value) => ReferenceFraction.FromDouble(value);
    private static ReferenceFraction N(int value) => new(value);
    private static ReferenceFraction Rounded(ReferenceFraction value) => value.RoundExtendedBinary64();
    private static ReferenceFraction Blend(ReferenceFraction a, ReferenceFraction b, double weight) =>
        Rounded(a * (N(1) - Q(weight)) + b * Q(weight));
    private static ReferenceFraction AddScaled(ReferenceFraction a, ReferenceFraction b, double weight) =>
        Rounded(a + b * Q(weight));

    internal static double[] Jurik(IReadOnlyList<Bar> bars, int period, double phase, int shortPeriod)
    {
        var result = new double[bars.Count];
        if (bars.Count == 0) return result;
        var beta = .45 * (period - 1) / (.45 * (period - 1) + 2);
        var length = Math.Max(Math.Log(Math.Sqrt(period - 1)) / Math.Log(2) + 2, 0);
        var exponent = Math.Max(length - 2, .5);
        var gain = Math.Min(Math.Max(phase * .01 + 1.5, .5), 2.5) + 1;
        var upper = N(0); var lower = N(0); var ma = Q(bars[0].Close); var jma = ma;
        var d0 = N(0); var d1 = N(0); var sum = N(0); var average = N(0);
        var volatility = new List<ReferenceFraction>();
        result[0] = bars[0].Close;
        for (var i = 1; i < bars.Count; i++)
        {
            var window = bars.Skip(Math.Max(0, i - period + 1)).Take(Math.Min(i + 1, period)).ToArray();
            var high = Q(window.Max(b => b.Close)); var low = Q(window.Min(b => b.Close));
            var dh = high - upper; var dl = low - lower;
            var v = dh.Abs().CompareTo(dl.Abs()) >= 0 ? dh.Abs() : dl.Abs();
            volatility.Add(v);
            sum = AddScaled(sum, v - volatility[Math.Max(0, volatility.Count - shortPeriod)], .1);
            average = AddScaled(average, sum - average, 2 / (Math.Max(4d * period, 30) + 1));
            var relative = average.Sign <= 0 ? 0 : (v / average).ToDouble();
            relative = Math.Min(Math.Max(relative, 1), Math.Pow(length, 1 / exponent));
            var power = Math.Pow(relative, exponent);
            var length2 = Math.Sqrt(.5 * (period - 1)) * length;
            var kv = Math.Pow(length2 / (length2 + 1), Math.Sqrt(power));
            upper = dh.Sign > 0 ? high : AddScaled(high, N(0) - dh, kv);
            lower = dl.Sign < 0 ? low : AddScaled(low, N(0) - dl, kv);
            var alpha = Math.Pow(beta, power);
            var price = Q(bars[i].Close);
            ma = Blend(price, ma, alpha);
            d0 = Blend(price - ma, d0, beta);
            var ma2 = AddScaled(ma, d0, gain);
            d1 = Rounded((ma2 - jma) * Q((1 - alpha) * (1 - alpha)) + d1 * Q(alpha * alpha));
            jma = Rounded(jma + d1);
            result[i] = jma.ToDouble();
        }
        return result;
    }

    internal static double[][] Fractals(IReadOnlyList<Bar> bars, int left, int right, bool close)
    {
        var output = Enumerable.Range(0, 4).Select(_ => new double[bars.Count]).ToArray();
        for (var center = left; (long)center + right < bars.Count; center++)
        {
            var high = close ? bars[center].Close : bars[center].High;
            var low = close ? bars[center].Close : bars[center].Low;
            var bear = true; var bull = true;
            for (var i = center - left; i <= center + right; i++)
            {
                if (i == center) continue;
                bear &= high > (close ? bars[i].Close : bars[i].High);
                bull &= low < (close ? bars[i].Close : bars[i].Low);
            }
            output[0][center] = bear ? high : 0; output[1][center] = bull ? low : 0;
            output[2][center] = bear ? 1 : 0; output[3][center] = bull ? 1 : 0;
        }
        return output;
    }

    internal static double[][] Pivots(IReadOnlyList<Bar> bars, int period, int offset, PivotLevelStyle style)
    {
        var output = Enumerable.Range(0, 18).Select(_ => new double[bars.Count]).ToArray();
        for (long index = (long)period + offset; index < bars.Count; index++)
        {
            var i = (int)index;
            var window = bars.Skip(i - period - offset).Take(period).ToArray();
            var high = Q(window.Max(b => b.High)); var low = Q(window.Min(b => b.Low));
            var close = Q(window[window.Length - 1].Close); var open = Q(bars[i].Open);
            var range = high - low;
            var values = new ReferenceFraction?[9];
            if (style == PivotLevelStyle.Camarilla)
            {
                values[0] = close;
                var divisors = new[] { 12, 6, 4, 2 };
                for (var level = 0; level < 4; level++)
                {
                    var distance = range * N(11) / N(10 * divisors[level]);
                    values[level + 1] = close - distance; values[level + 5] = close + distance;
                }
            }
            else if (style == PivotLevelStyle.Demark)
            {
                var comparison = close.CompareTo(open);
                var x = high + low + close + (comparison == 0 ? close : comparison > 0 ? high : low);
                values[0] = x / N(4); values[1] = x / N(2) - high; values[5] = x / N(2) - low;
            }
            else
            {
                var pivot = style == PivotLevelStyle.Woodie ? (high + low + N(2) * open) / N(4)
                    : (high + low + close) / N(3);
                values[0] = pivot;
                if (style == PivotLevelStyle.Fibonacci)
                {
                    var factors = new[] { 382, 618, 1000 };
                    for (var level = 0; level < 3; level++)
                    {
                        var distance = range * N(factors[level]) / N(1000);
                        values[level + 1] = pivot - distance; values[level + 5] = pivot + distance;
                    }
                }
                else
                {
                    values[1] = N(2) * pivot - high; values[5] = N(2) * pivot - low;
                    values[2] = pivot - range; values[6] = pivot + range;
                    values[3] = N(2) * pivot - high - range; values[7] = N(2) * pivot - low + range;
                }
            }
            for (var slot = 0; slot < 9; slot++)
            {
                output[slot][i] = values[slot]?.ToDouble() ?? 0;
                output[slot + 9][i] = values[slot].HasValue ? 1 : 0;
            }
        }
        return output;
    }
}
