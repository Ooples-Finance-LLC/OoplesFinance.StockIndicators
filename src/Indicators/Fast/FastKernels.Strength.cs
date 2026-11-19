namespace OoplesFinance.StockIndicators.Indicators.Fast;

internal static partial class FastKernels
{
    /// <summary>
    /// Wilder-smoothed gains and losses of the close, seeded by their simple means over the first
    /// <paramref name="period"/> changes, published as a strength percentage from bar period + unstable.
    /// </summary>
    internal static void WilderStrength(ReadOnlySpan<double> close, Span<double> value, Span<double> defined,
        int period, WilderStrengthConvention convention, int unstable)
    {
        var n = close.Length;
        value = value.Slice(0, n);
        var first = (long)period + unstable;
        DefinedFrom(defined.Slice(0, n), first);
        value.Slice(0, (int)Math.Min(first, n)).Clear();
        if (n <= period) return;
        double gain = 0, loss = 0;
        for (var i = 1; i <= period; i++)
        {
            var change = close[i] - close[i - 1];
            if (change > 0) gain += change;
            else loss -= change;
        }
        gain /= period;
        loss /= period;
        var signed = convention == WilderStrengthConvention.ChandeZeroFlat;
        var flat = convention == WilderStrengthConvention.RsiHundredFlat ? 100.0 : 0.0;
        var scale = 1.0 / period;
        var weight = (period - 1.0) / period;
        if (first == period) value[period] = StrengthPercent(gain, loss, signed, flat);
        for (var i = period + 1; i < n; i++)
        {
            var change = close[i] - close[i - 1];
            gain = FastMath.Blend(weight, gain, scale * Math.Max(change, 0));
            loss = FastMath.Blend(weight, loss, scale * Math.Max(-change, 0));
            if (i >= first) value[i] = StrengthPercent(gain, loss, signed, flat);
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static double StrengthPercent(double gain, double loss, bool signed, double flat)
    {
        var total = gain + loss;
        if (total == 0) return flat;
        return 100 * (signed ? gain - loss : gain) / total;
    }
}
