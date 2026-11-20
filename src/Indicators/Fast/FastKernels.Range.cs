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


    /// <summary>
    /// True range, its Wilder average seeded by the mean of the first ranges, and that average as a
    /// percentage of the close, each with a flag for the bars that have it.
    /// </summary>
    internal static void AverageTrueRangeDetails(FastColumns columns, double[][] outputs, int period)
    {
        columns.RequireHighLowClose();
        ReadOnlySpan<double> high = columns.High, low = columns.Low, close = columns.Close;
        var n = close.Length;
        for (var slot = 0; slot < 6; slot++) outputs[slot].AsSpan(0, n).Clear();
        Span<double> range = outputs[0].AsSpan(0, n), average = outputs[1].AsSpan(0, n), percent = outputs[2].AsSpan(0, n);
        for (var i = 1; i < n; i++) range[i] = TrueRange(high[i], low[i], close[i - 1]);
        if (n > 1) outputs[3].AsSpan(1, n - 1).Fill(1);
        if (n <= period) return;
        double sum = 0;
        for (var i = 1; i <= period; i++) sum += range[i];
        var value = sum / period;
        average[period] = value;
        var weight = (period - 1.0) / period;
        // The same recurrence as an exponential average with alpha = 1 / period.
        FastMath.BlendRun(weight, 1.0 / period, range, average, period + 1, value);
        outputs[4].AsSpan(period, n - period).Fill(1);
        var percentDefined = outputs[5].AsSpan(0, n);
        for (var i = period; i < n; i++)
        {
            var c = close[i];
            if (c == 0) continue; // NOSONAR an exact tie is the contract
            percent[i] = 100 * average[i] / c;
            percentDefined[i] = 1;
        }
    }


    /// <summary>
    /// Accumulation/distribution with its parts: the money-flow multiplier, the bar's flow, the running
    /// line and, when asked for, the rolling mean of the line.
    /// </summary>
    internal static void AccumulationDetails(FastColumns columns, double[][] outputs, int? averagePeriod)
    {
        columns.RequireHighLowClose();
        ReadOnlySpan<double> high = columns.High, low = columns.Low, close = columns.Close, volume = columns.Volume;
        var n = close.Length;
        Span<double> multipliers = outputs[0].AsSpan(0, n), flows = outputs[1].AsSpan(0, n), line = outputs[2].AsSpan(0, n);
        Span<double> average = outputs[3].AsSpan(0, n), defined = outputs[4].AsSpan(0, n);
        average.Clear();
        defined.Clear();
        double total = 0;
        for (var i = 0; i < n; i++)
        {
            var range = high[i] - low[i];
            double multiplier = 0, flow = 0;
            if (range != 0) // NOSONAR an exact tie is the contract
            {
                multiplier = (2 * close[i] - high[i] - low[i]) / range;
                flow = multiplier * volume[i];
            }
            total += flow;
            multipliers[i] = multiplier;
            flows[i] = flow;
            line[i] = total;
        }
        if (averagePeriod is not int period) return;
        double sum = 0;
        for (var i = 0; i < n; i++)
        {
            // An infinite line fails the build when the outputs are checked.
            if (double.IsInfinity(line[i])) return;
            if (i >= period && (i & RebuildMask) == 0)
            {
                sum = 0;
                for (var j = i - period + 1; j <= i; j++) sum += line[j];
            }
            else
            {
                if (i >= period) sum -= line[i - period];
                sum += line[i];
            }
            if (i < period - 1) continue;
            average[i] = period == 1 ? line[i] : sum / period;
            defined[i] = 1;
        }
    }

    /// <summary>
    /// Awesome oscillator: the fast mean of the bar midpoint less the slow one, and that difference as a
    /// percentage of the midpoint where the midpoint is not zero.
    /// </summary>
    internal static void AwesomeDetails(FastColumns columns, double[][] outputs, int fast, int slow)
    {
        ReadOnlySpan<double> high = columns.High, low = columns.Low;
        var n = high.Length;
        for (var slot = 0; slot < 4; slot++) outputs[slot].AsSpan(0, n).Clear();
        if (n < slow) return;
        var pool = System.Buffers.ArrayPool<double>.Shared;
        double[] rentedPrice = pool.Rent(n), rentedFast = pool.Rent(n), rentedSlow = pool.Rent(n);
        try
        {
            Span<double> price = rentedPrice.AsSpan(0, n), fastMean = rentedFast.AsSpan(0, n), slowMean = rentedSlow.AsSpan(0, n);
            for (var i = 0; i < n; i++) price[i] = 0.5 * high[i] + 0.5 * low[i];
            Sma(price, fastMean, fast);
            Sma(price, slowMean, slow);
            Span<double> oscillator = outputs[0].AsSpan(0, n), percent = outputs[1].AsSpan(0, n);
            Span<double> defined = outputs[2].AsSpan(0, n), percentDefined = outputs[3].AsSpan(0, n);
            for (var i = slow - 1; i < n; i++)
            {
                var value = fastMean[i] - slowMean[i];
                oscillator[i] = value;
                defined[i] = 1;
                if (price[i] == 0 || double.IsInfinity(value)) continue; // NOSONAR an exact tie is the contract
                percent[i] = 100 * value / price[i];
                percentDefined[i] = 1;
            }
        }
        finally
        {
            pool.Return(rentedPrice);
            pool.Return(rentedFast);
            pool.Return(rentedSlow);
        }
    }


    /// <summary>
    /// Alligator: three Wilder averages of the bar midpoint, each published a fixed number of bars late.
    /// </summary>
    internal static void AlligatorDetails(FastColumns columns, double[][] outputs, AlligatorWithDetails owner)
    {
        ReadOnlySpan<double> high = columns.High, low = columns.Low;
        var n = high.Length;
        for (var slot = 0; slot < 6; slot++) outputs[slot].AsSpan(0, n).Clear();
        if (n == 0) return;
        var pool = System.Buffers.ArrayPool<double>.Shared;
        double[] rentedPrice = pool.Rent(n), rentedMean = pool.Rent(n);
        try
        {
            Span<double> price = rentedPrice.AsSpan(0, n), mean = rentedMean.AsSpan(0, n);
            for (var i = 0; i < n; i++) price[i] = 0.5 * high[i] + 0.5 * low[i];
            Span<int> periods = stackalloc int[] { owner.JawPeriod, owner.TeethPeriod, owner.LipsPeriod };
            Span<int> offsets = stackalloc int[] { owner.JawOffset, owner.TeethOffset, owner.LipsOffset };
            for (var slot = 0; slot < 3; slot++)
            {
                var period = periods[slot];
                var first = (long)period - 1 + offsets[slot];
                if (first >= n) continue;
                WilderAverage(price, mean, period, new WilderMovingAverage(period).SeedWithAverage);
                var count = n - (int)first;
                mean.Slice(period - 1, count).CopyTo(outputs[slot].AsSpan((int)first, count));
                outputs[slot + 3].AsSpan((int)first, count).Fill(1);
            }
        }
        finally
        {
            pool.Return(rentedPrice);
            pool.Return(rentedMean);
        }
    }

    /// <summary>
    /// Elder ray: an exponential average of the close seeded by the mean of the first period closes, with
    /// the high and the low measured from it.
    /// </summary>
    internal static void ElderRayDetails(FastColumns columns, double[][] outputs, int period)
    {
        columns.RequireHighLowClose();
        ReadOnlySpan<double> high = columns.High, low = columns.Low, close = columns.Close;
        var n = close.Length;
        for (var slot = 0; slot < 4; slot++) outputs[slot].AsSpan(0, n).Clear();
        if (n < period) return;
        Span<double> average = outputs[0].AsSpan(0, n), bull = outputs[1].AsSpan(0, n), bear = outputs[2].AsSpan(0, n);
        double sum = 0;
        for (var i = 0; i < period; i++) sum += close[i];
        var value = period == 1 ? close[0] : sum / period;
        average[period - 1] = value;
        FastMath.BlendRun((period - 1.0) / (period + 1.0), 2.0 / (period + 1.0), close, average, period, value);
        for (var i = period - 1; i < n; i++)
        {
            bull[i] = high[i] - average[i];
            bear[i] = low[i] - average[i];
        }
        outputs[3].AsSpan(period - 1, n - period + 1).Fill(1);
    }
}
