namespace OoplesFinance.StockIndicators.Indicators.Fast;

/// <summary>Plain double column kernels. Each mirrors the published series of one indicator class.</summary>
internal static partial class FastKernels
{
    /// <summary>Rolling mean; zero until a full window is available.</summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
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
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
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
        if (n > 1) FastMath.BlendRun(weight, alpha, input, output, 1, previous);
    }

    /// <summary>
    /// Double or triple exponential average whose stages are seeded by simple means. Shared seeding starts
    /// every stage from the first window's mean; cascaded seeding seeds each stage from the previous one.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
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


    /// <summary>
    /// Whether the masses of a convolution kernel's partial windows can be trusted in doubles: each running
    /// sum of the weights is either exact, so a zero mass is a true zero, or far enough from zero that its
    /// rounding cannot matter.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    internal static bool SupportsConvolution(IReadOnlyList<double> kernel)
    {
        double sum = 0, magnitude = 0;
        var exact = true;
        for (var lag = 0; lag < kernel.Count; lag++)
        {
            var weight = kernel[lag];
            var next = sum + weight;
            // Knuth's error-free sum: the part of the true sum the rounded one dropped.
            var carried = next - sum;
            var error = sum - (next - carried) + (weight - carried);
            exact &= error == 0; // NOSONAR an exact zero is the proof that nothing was dropped
            sum = next;
            magnitude += Math.Abs(weight);
            if (!exact && !(Math.Abs(sum) >= 1e-6 * magnitude)) return false;
        }
        return !double.IsInfinity(magnitude);
    }

    /// <summary>
    /// Normalized convolution: the newest closes under the kernel, newest weight first, over the mass of
    /// the weights in use; a window whose weights cancel exactly is divided by its length instead.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    internal static void NormalizedConvolution(ReadOnlySpan<double> close, Span<double> output, IReadOnlyList<double> kernel)
    {
        var n = close.Length;
        output = output.Slice(0, n);
        var length = kernel.Count;
        // Oldest weight first, so a window of closes lines up with a run of it.
        var weights = new double[length];
        for (var lag = 0; lag < length; lag++) weights[length - 1 - lag] = kernel[lag];
        var filling = Math.Min(length - 1, n);
        double mass = 0;
        for (var i = 0; i < filling; i++)
        {
            mass += kernel[i];
            var held = i + 1;
            output[i] = FastMath.Dot(weights.AsSpan(length - held, held), close.Slice(0, held))
                / (mass == 0 ? held : mass); // NOSONAR the binder admits only kernels whose zero mass is exact
        }
        if (n < length) return;
        mass += kernel[length - 1];
        var divisor = mass == 0 ? length : mass; // NOSONAR the binder admits only kernels whose zero mass is exact
        for (var i = length - 1; i < n; i++)
            output[i] = FastMath.Dot(weights, close.Slice(i - length + 1, length)) / divisor;
    }
}
