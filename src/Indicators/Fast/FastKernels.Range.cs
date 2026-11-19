namespace OoplesFinance.StockIndicators.Indicators.Fast;

internal static partial class FastKernels
{
    /// <summary>
    /// Wilder average of the true range, seeded by the mean of the first <paramref name="period"/> ranges.
    /// The first bar has no previous close and so no range; the output is zero until the seed completes.
    /// With <paramref name="percent"/> the average is published as a percentage of the close.
    /// </summary>
    internal static void SeededAverageTrueRange(ReadOnlySpan<double> high, ReadOnlySpan<double> low,
        ReadOnlySpan<double> close, Span<double> output, int period, bool percent)
    {
        var n = close.Length;
        high = high.Slice(0, n);
        low = low.Slice(0, n);
        output = output.Slice(0, n);
        var seeded = Math.Min((long)period, n - 1L);
        output.Slice(0, (int)Math.Max(0, Math.Min(period, (long)n))).Clear();
        if (n <= period) return;
        double sum = 0;
        for (var i = 1; i <= seeded; i++) sum += TrueRange(high[i], low[i], close[i - 1]);
        var average = sum / period;
        var scale = 1.0 / period;
        var weight = (period - 1.0) / period;
        output[period] = percent ? Percent(average, close[period]) : average;
        if (percent)
            for (var i = period + 1; i < n; i++)
            {
                average = FastMath.Blend(weight, average, scale * TrueRange(high[i], low[i], close[i - 1]));
                output[i] = Percent(average, close[i]);
            }
        else
            for (var i = period + 1; i < n; i++)
            {
                average = FastMath.Blend(weight, average, scale * TrueRange(high[i], low[i], close[i - 1]));
                output[i] = average;
            }
    }

    private static double Percent(double average, double close) => close == 0 ? 0 : 100 * average / close;

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal static double TrueRange(double high, double low, double previousClose) =>
        FastMath.Larger(high - low, FastMath.Larger(Math.Abs(high - previousClose), Math.Abs(low - previousClose)));

    /// <summary>
    /// Accumulation/distribution line smoothed by two exponential averages, published as fast minus slow,
    /// with the money-flow multiplier, the bar's flow and the running line as optional details.
    /// </summary>
    internal static void SmoothedAccumulation(FastColumns columns, double[][] outputs, SmoothedAccumulationOscillator owner)
    {
        columns.RequireHighLowClose();
        ReadOnlySpan<double> high = columns.High, low = columns.Low, close = columns.Close, volume = columns.Volume;
        var n = close.Length;
        var details = owner.IncludeDetails;
        var firstValue = owner.FirstValue;
        var fast = new Seeded(owner.FastPeriod, firstValue, 2.0 / (owner.FastPeriod + 1.0));
        var slow = new Seeded(owner.SlowPeriod, firstValue, 2.0 / (owner.SlowPeriod + 1.0));
        var start = (int)Math.Min((long)Math.Max(owner.FastPeriod, owner.SlowPeriod) - 1 + owner.Suppression, n);
        Span<double> oscillator = outputs[0], multipliers = outputs[1], flows = outputs[2], lines = outputs[3];
        oscillator.Slice(0, start).Clear();
        if (!details)
        {
            multipliers.Clear();
            flows.Clear();
            lines.Clear();
        }
        double total = 0;
        for (var i = 0; i < n; i++)
        {
            var range = high[i] - low[i];
            double multiplier = 0, flow = 0;
            if (firstValue ? range > 0 : range != 0)
            {
                // Dividing by the signed range is the same as the sign-adjusted numerator over its magnitude.
                multiplier = (2 * close[i] - high[i] - low[i]) / range;
                flow = multiplier * volume[i];
            }
            total += flow;
            fast.Add(total);
            slow.Add(total);
            if (details)
            {
                multipliers[i] = multiplier;
                flows[i] = flow;
                lines[i] = total;
            }
            if (i >= start) oscillator[i] = fast.Value - slow.Value;
        }
        DefinedFrom(outputs[4], start);
        for (var slot = 5; slot < 8; slot++)
            if (details) outputs[slot].AsSpan().Fill(1);
            else outputs[slot].AsSpan().Clear();
    }
}
