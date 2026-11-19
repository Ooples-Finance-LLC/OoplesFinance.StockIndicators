namespace OoplesFinance.StockIndicators.Indicators.Fast;

/// <summary>Plain double column kernels. Each mirrors the published series of one indicator class.</summary>
internal static partial class FastKernels
{
    /// <summary>Rolling mean; zero until a full window is available.</summary>
    internal static void Sma(ReadOnlySpan<double> input, Span<double> output, int period)
    {
        period = Math.Max(1, period);
        var n = input.Length;
        var warm = Math.Min(period - 1, n);
        output.Slice(0, warm).Clear();
        if (period > n) return;
        double sum = 0;
        for (var i = 0; i < period; i++) sum += input[i];
        var reciprocal = 1.0 / period;
        output[period - 1] = sum * reciprocal;
        var entering = input.Slice(period);
        var leaving = input.Slice(0, entering.Length);
        var tail = output.Slice(period, entering.Length);
        for (var i = 0; i < tail.Length; i++)
        {
            sum += entering[i] - leaving[i];
            tail[i] = sum * reciprocal;
        }
    }

    /// <summary>Exponential average seeded by the first input, alpha = 2 / (period + 1).</summary>
    internal static void FirstValueEma(ReadOnlySpan<double> input, Span<double> output, int period)
    {
        var n = input.Length;
        if (n == 0) return;
        if (period <= 1) { input.CopyTo(output); return; }
        var alpha = 2.0 / (period + 1.0);
        var weight = 1.0 - alpha;
        var previous = input[0];
        output[0] = previous;
        output = output.Slice(0, n);
        for (var i = 1; i < n; i++)
        {
            previous = FastMath.Blend(weight, previous, alpha * input[i]);
            output[i] = previous;
        }
    }

    /// <summary>
    /// Double or triple exponential average whose stages are seeded by simple means. Shared seeding starts
    /// every stage from the first window's mean; cascaded seeding seeds each stage from the previous one.
    /// </summary>
    internal static void SeededExponential(ReadOnlySpan<double> input, Span<double> output, int period, int order, bool shared)
    {
        var n = input.Length;
        var warmup = (int)Math.Min(int.MaxValue, (long)(period - 1) * (shared ? 1 : order));
        output.Slice(0, Math.Min(warmup, n)).Clear();
        if (warmup >= n) return;
        var alpha = 2.0 / (period + 1.0);
        var weight = 1.0 - alpha;
        double a0 = 0, a1 = 0, a2 = 0;
        if (shared)
        {
            double seed = 0;
            for (var i = 0; i < period; i++) seed += input[i];
            seed /= period;
            a0 = a1 = a2 = seed;
            output[period - 1] = seed;
            for (var i = period; i < n; i++)
            {
                a0 = FastMath.Blend(weight, a0, alpha * input[i]);
                a1 = FastMath.Blend(weight, a1, alpha * a0);
                if (order == 3)
                {
                    a2 = FastMath.Blend(weight, a2, alpha * a1);
                    output[i] = 3 * (a0 - a1) + a2;
                }
                else output[i] = 2 * a0 - a1;
            }
            return;
        }
        // Cascaded: each stage is seeded by the mean of the first (period) values the stage before it publishes.
        double s0 = 0, s1 = 0, s2 = 0;
        int c0 = 0, c1 = 0, c2 = 0;
        for (var i = 0; i < n; i++)
        {
            if (c0 < period) { s0 += input[i]; if (++c0 < period) continue; a0 = s0 / period; }
            else a0 = FastMath.Blend(weight, a0, alpha * input[i]);
            if (c1 < period) { s1 += a0; if (++c1 < period) continue; a1 = s1 / period; }
            else a1 = FastMath.Blend(weight, a1, alpha * a0);
            if (order == 3)
            {
                if (c2 < period) { s2 += a1; if (++c2 < period) continue; a2 = s2 / period; }
                else a2 = FastMath.Blend(weight, a2, alpha * a1);
                output[i] = 3 * (a0 - a1) + a2;
            }
            else output[i] = 2 * a0 - a1;
        }
    }
}
