namespace OoplesFinance.StockIndicators.Indicators.Fast;

internal static partial class FastKernels
{
    /// <summary>
    /// Wilder-smoothed gains and losses of the close, seeded by their simple means over the first
    /// <paramref name="period"/> changes, published as a strength percentage from bar period + unstable.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Hot)]
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

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | FastMath.Hot)]
    private static double StrengthPercent(double gain, double loss, bool signed, double flat)
    {
        var total = gain + loss;
        if (total == 0) return flat;
        return 100 * (signed ? gain - loss : gain) / total;
    }


    /// <summary>
    /// Connors strength: the close's strength, the strength of its up/down streak length, the percentage of
    /// the last returns that are smaller than this bar's, and the mean of the three.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Hot)]
    internal static void ConnorsStrength(ReadOnlySpan<double> close, double[][] outputs, int rsiPeriod, int streakPeriod,
        int rankPeriod)
    {
        var n = close.Length;
        for (var slot = 0; slot < 8; slot++) outputs[slot].AsSpan(0, n).Clear();
        if (n == 0) return;
        var price = new WilderStrengthOscillator(rsiPeriod, WilderStrengthConvention.RsiHundredFlat);
        WilderStrength(close, outputs[0], outputs[4], rsiPeriod, price.Convention, price.UnstablePeriods);
        var pool = System.Buffers.ArrayPool<double>.Shared;
        double[] rentedStreak = pool.Rent(n), rentedRatio = pool.Rent(n);
        try
        {
            // The streak strength reads the streak series from the second bar.
            var streaks = rentedStreak.AsSpan(0, n - 1);
            var streak = 0;
            for (var i = 1; i < n; i++)
            {
                streak = close[i] > close[i - 1] ? Math.Max(0, streak) + 1
                    : close[i] < close[i - 1] ? Math.Min(0, streak) - 1 : 0;
                streaks[i - 1] = streak;
            }
            Span<double> streakValue = outputs[1].AsSpan(1, n - 1), streakDefined = outputs[5].AsSpan(1, n - 1);
            var length = new WilderStrengthOscillator(streakPeriod, WilderStrengthConvention.RsiHundredFlat);
            WilderStrength(streaks, streakValue, streakDefined, streakPeriod, length.Convention, length.UnstablePeriods);
            var held = (int)Math.Min((long)streakPeriod + 2, n);
            outputs[1].AsSpan(0, held).Clear();
            outputs[5].AsSpan(0, held).Clear();

            // A return against a price that is not positive is absent and ranks below nothing.
            var ratio = rentedRatio.AsSpan(0, n);
            ratio[0] = 1;
            for (var i = 1; i < n; i++) ratio[i] = close[i - 1] > 0 ? close[i] / close[i - 1] : double.NaN;
            Span<double> rank = outputs[2].AsSpan(0, n), rankDefined = outputs[6].AsSpan(0, n);
            for (var i = rankPeriod; i < n; i++)
            {
                rankDefined[i] = 1;
                var current = ratio[i];
                if (double.IsNaN(current)) continue;
                var smaller = 0;
                var ties = 0;
                for (var j = i - rankPeriod; j < i; j++)
                {
                    if (ratio[j] < current) smaller++;
                    else if (ratio[j] == current) ties++; // NOSONAR an exact tie is the contract
                }
                // Equal quotients do not prove equal returns; those few are compared exactly.
                if (ties > 0) smaller += ExactlySmallerReturns(close, ratio, i, rankPeriod);
                rank[i] = (100L * smaller) / rankPeriod; // NOSONAR the rank truncates before it is published
            }
            Span<double> value = outputs[3].AsSpan(0, n), defined = outputs[7].AsSpan(0, n);
            var first = (long)Math.Max(rsiPeriod, Math.Max(streakPeriod, rankPeriod)) + 1;
            for (var i = (int)Math.Min(first, n); i < n; i++)
            {
                if (outputs[4][i] <= 0 || outputs[5][i] <= 0) continue;
                value[i] = (outputs[0][i] + outputs[1][i] + rank[i]) / 3;
                defined[i] = 1;
            }
        }
        finally
        {
            pool.Return(rentedStreak);
            pool.Return(rentedRatio);
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(FastMath.Hot)]
    private static int ExactlySmallerReturns(ReadOnlySpan<double> close, ReadOnlySpan<double> ratio, int i, int rankPeriod)
    {
        var current = new ReturnOrderStatistic.ReturnKey(close[i], close[i - 1]);
        var smaller = 0;
        for (var j = i - rankPeriod; j < i; j++)
        {
            if (ratio[j] != ratio[i]) continue; // NOSONAR an exact tie is the contract
            // The same two prices, or two unchanged bars, are the same return.
            if (j > 0 && ((close[j] == close[i] && close[j - 1] == close[i - 1]) // NOSONAR exact
                || (close[j] == close[j - 1] && close[i] == close[i - 1]))) continue; // NOSONAR exact
            var other = j == 0 ? new ReturnOrderStatistic.ReturnKey(1, 1) : new ReturnOrderStatistic.ReturnKey(close[j], close[j - 1]);
            if (other.CompareTo(current) < 0) smaller++;
        }
        return smaller;
    }


    /// <summary>
    /// Stochastic of the close's strength over its own rolling range, zero where that range is flat, then a
    /// mean of that and a signal mean of the result, each from its first full window.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Hot)]
    internal static void SmoothedStochasticStrength(ReadOnlySpan<double> close, FastOutputs outputs, SmoothedStochasticRsi owner)
    {
        var n = close.Length;
        for (var slot = 0; slot < 4; slot++) outputs[slot].AsSpan(0, n).Clear();
        var strength = new WilderStrengthOscillator(owner.RsiPeriod, WilderStrengthConvention.RsiHundredFlat);
        var start = (long)strength.Period + strength.UnstablePeriods;
        var first = start + owner.StochasticPeriod - 1 + owner.SmoothPeriod - 1;
        if (first >= n) return;
        var pool = System.Buffers.ArrayPool<double>.Shared;
        double[] rentedStrength = pool.Rent(n), rentedLow = pool.Rent(n), rentedHigh = pool.Rent(n);
        try
        {
            // The signal output is scratch for the strength's own flags until it is written below.
            Span<double> values = rentedStrength.AsSpan(0, n), signal = outputs[1].AsSpan(0, n);
            WilderStrength(close, values, signal, strength.Period, strength.Convention, strength.UnstablePeriods);
            signal.Clear();
            var from = (int)start;
            var count = n - from;
            var series = values.Slice(from);
            Span<double> lowest = rentedLow.AsSpan(0, count), highest = rentedHigh.AsSpan(0, count);
            RollingExtreme(series, lowest, owner.StochasticPeriod, false);
            RollingExtreme(series, highest, owner.StochasticPeriod, true);
            var filled = owner.StochasticPeriod - 1;
            for (var k = filled; k < count; k++)
            {
                // The strength carries a few units of rounding; a range no wider than that is a flat one.
                var range = highest[k] - lowest[k];
                highest[k] = range <= 64 * Ulp * Math.Abs(highest[k]) ? 0 : 100 * (series[k] - lowest[k]) / range;
            }
            var raw = highest.Slice(filled);
            var at = from + filled;
            var value = outputs[0].AsSpan(at);
            if (owner.SmoothPeriod == 1) raw.CopyTo(value);
            else Sma(raw, value, owner.SmoothPeriod);
            var valueFirst = (int)first;
            outputs.Flag(2, valueFirst);
            var signalFirst = first + owner.SignalPeriod - 1;
            if (signalFirst >= n) return;
            var smoothed = outputs[0].AsSpan(valueFirst, n - valueFirst);
            var line = signal.Slice(valueFirst);
            if (owner.SignalPeriod == 1) smoothed.CopyTo(line);
            else Sma(smoothed, line, owner.SignalPeriod);
            outputs.Flag(3, (int)signalFirst);
        }
        finally
        {
            pool.Return(rentedStrength);
            pool.Return(rentedLow);
            pool.Return(rentedHigh);
        }
    }
}
