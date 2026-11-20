namespace OoplesFinance.StockIndicators.Indicators.Fast;

internal static partial class FastSnapshots
{
    // Fibonacci numbers pass the largest double a little after this many terms.
    internal const int FibonacciPeriodLimit = 1400;

    private static double[] Closes(IReadOnlyList<Bar> bars, out double[] rented)
    {
        var rows = Rows(bars);
        rented = System.Buffers.ArrayPool<double>.Shared.Rent(rows.Length);
        for (var i = 0; i < rows.Length; i++) rented[i] = rows[i].Close;
        return rented;
    }

    /// <summary>
    /// Fibonacci-weighted average: the newest close carries the largest Fibonacci number of the period, over
    /// the bars so far while the window fills.
    /// </summary>
    internal static double[] FibonacciWeighted(IReadOnlyList<Bar> bars, int period)
    {
        var count = bars.Count;
        if (count == 0) return [];
        var output = new double[count];
        var weights = new double[period];
        // Held mass by window length: the newest weights are the last ones.
        var mass = new double[period + 1];
        weights[0] = 1;
        if (period > 1) weights[1] = 1;
        for (var i = 2; i < period; i++) weights[i] = weights[i - 1] + weights[i - 2];
        for (var held = 1; held <= period; held++) mass[held] = mass[held - 1] + weights[period - held];
        ReadOnlySpan<double> close = Closes(bars, out var rented).AsSpan(0, count);
        try
        {
            for (var i = 0; i < count; i++)
            {
                var held = Math.Min(i + 1, period);
                output[i] = FastMath.Dot(weights.AsSpan(period - held, held), close.Slice(i - held + 1, held)) / mass[held];
            }
            return output;
        }
        finally
        {
            System.Buffers.ArrayPool<double>.Shared.Return(rented);
        }
    }

    /// <summary>
    /// Windowed-sinc filter: the last taps closes under a fixed kernel, the newest close standing in for
    /// the taps a short history does not have yet.
    /// </summary>
    internal static double[] WindowedSinc(IReadOnlyList<Bar> bars, double[] weights, double mass)
    {
        var count = bars.Count;
        var taps = weights.Length;
        var output = new double[count];
        ReadOnlySpan<double> close = Closes(bars, out var rented).AsSpan(0, count);
        try
        {
            // While the window fills, the newest close stands in for every tap the history does not reach.
            var filling = Math.Min(taps - 1, count);
            for (var i = 0; i < filling; i++)
            {
                var value = (FastMath.Dot(weights.AsSpan(0, i), close.Slice(0, i)) + Remaining(weights, i) * close[i]) / mass;
                output[i] = Representable(value);
            }
            for (var i = filling; i < count; i++)
                output[i] = Representable(FastMath.Dot(weights, close.Slice(i - taps + 1, taps)) / mass);
            return output;
        }
        finally
        {
            System.Buffers.ArrayPool<double>.Shared.Return(rented);
        }
    }

    // The sum of the taps from the given one on, taken afresh so a short history does not inherit rounding.
    private static double Remaining(double[] weights, int from)
    {
        double sum = 0;
        for (var k = from; k < weights.Length; k++) sum += weights[k];
        return sum;
    }

    private static double Representable(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value)
            ? value
            : throw new OverflowException("Sinc output is not representable.");
}
