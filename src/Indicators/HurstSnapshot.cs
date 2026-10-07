namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Raw and Anis-Lloyd corrected rescaled-range exponents.</summary>
public sealed record HurstValue(double? HurstExponent, double? HurstExponentAL);

/// <summary>Rolling rescaled-range regression over powers-of-two partitions.</summary>
public static class HurstSnapshot
{
    /// <summary>Calculates both exponents using at least twenty returns, chunks of at least eight, and at most thirty-two chunks.</summary>
    /// <remarks>Nonpositive prices and undefined regressions produce absent outputs. Positive extreme price ratios use log differences when direct division overflows or underflows.
    /// Each partition discards its oldest remainder. The Anis-Lloyd correction uses an exact half-integer gamma recurrence up to size 340 and the conventional asymptotic factor thereafter.</remarks>
    public static IReadOnlyList<HurstValue> Calculate(IReadOnlyList<Bar> bars, int period = 100)
    {
        ArgumentNullException.ThrowIfNull(bars);
        if (period < 20)
            throw new ArgumentOutOfRangeException(nameof(period));
        foreach (var b in bars)
            if (!double.IsFinite(b.Close))
                throw new ArgumentOutOfRangeException(nameof(bars));
        var result = Enumerable
            .Range(0, bars.Count)
            .Select(_ => new HurstValue(null, null))
            .ToArray();
        if (period >= bars.Count)
            return result;
        var returns = new double[bars.Count];
        for (var i = 1; i < bars.Count; i++)
        {
            var c = bars[i].Close;
            var p = bars[i - 1].Close;
            var ratio = c / p;
            returns[i] =
                c <= 0 || p <= 0 ? double.NaN
                : ratio > 0 && double.IsFinite(ratio) ? Math.Log(ratio)
                : Math.Log(c) - Math.Log(p);
        }
        var sizes = Enumerable
            .Range(0, 6)
            .Select(k => period / (1 << k))
            .Where(n => n >= 8)
            .ToArray();
        var corrections = sizes.Select(Correction).ToArray();
        var x = sizes.Select(n => Math.Log10(n)).ToArray();
        for (var end = period; end < bars.Count; end++)
        {
            var raw = new double[sizes.Length];
            var corrected = new double[sizes.Length];
            for (var k = 0; k < sizes.Length; k++)
            {
                var n = sizes[k];
                var chunks = 1 << k;
                double total = 0;
                for (var j = 0; j < chunks; j++)
                {
                    var start = end + 1 - n * (chunks - j);
                    double sum = 0;
                    for (var q = start; q < start + n; q++)
                        sum += returns[q];
                    var mean = sum / n;
                    double cumulative = 0,
                        minimum = double.PositiveInfinity,
                        maximum = double.NegativeInfinity,
                        squares = 0;
                    for (var q = start; q < start + n; q++)
                    {
                        var delta = returns[q] - mean;
                        cumulative += delta;
                        squares += delta * delta;
                        minimum = Math.Min(minimum, cumulative);
                        maximum = Math.Max(maximum, cumulative);
                    }
                    total += squares == 0 ? 0 : (maximum - minimum) / Math.Sqrt(squares / n);
                }
                raw[k] = Math.Log10(total / chunks);
                corrected[k] = Math.Log10(total / chunks + corrections[k]);
            }
            result[end] = new(Slope(x, raw), Slope(x, corrected));
        }
        return result;
    }

    private static double Correction(int n)
    {
        double factor;
        if (n > 340)
            factor = Math.Sqrt(2 / (Math.PI * (n - 1d)));
        else
        {
            factor = n % 2 == 0 ? 1 : 2 / Math.PI;
            for (var m = n % 2 == 0 ? 2 : 3; m < n; m += 2)
                factor *= (m - 1d) / m;
        }
        double sum = 0;
        for (var j = 1; j < n; j++)
            sum += Math.Sqrt((n - j) / (double)j);
        return Math.Sqrt(Math.PI * n / 2) - factor * sum;
    }

    private static double? Slope(double[] x, double[] y)
    {
        var xm = x.Average();
        var ym = y.Average();
        double numerator = 0,
            denominator = 0;
        for (var i = 0; i < x.Length; i++)
        {
            numerator += (x[i] - xm) * (y[i] - ym);
            denominator += (x[i] - xm) * (x[i] - xm);
        }
        var value = numerator / denominator;
        return double.IsFinite(value) ? value : null;
    }
}
