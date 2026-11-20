namespace OoplesFinance.StockIndicators.Indicators.Fast;

internal static partial class FastKernels
{
    /// <summary>
    /// Wilder average of the true range, seeded by the mean of the first <paramref name="period"/> ranges.
    /// The first bar has no previous close and so no range; the output is zero until the seed completes.
    /// With <paramref name="percent"/> the average is published as a percentage of the close.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
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

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | FastMath.Hot)]
    internal static double TrueRange(double high, double low, double previousClose) =>
        FastMath.Larger(high - low, FastMath.Larger(Math.Abs(high - previousClose), Math.Abs(low - previousClose)));

    /// <summary>
    /// Accumulation/distribution line smoothed by two exponential averages, published as fast minus slow,
    /// with the money-flow multiplier, the bar's flow and the running line as optional details.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    internal static void SmoothedAccumulation(FastColumns columns, FastOutputs outputs, SmoothedAccumulationOscillator owner)
    {
        columns.RequireHighLowClose();
        ReadOnlySpan<double> high = columns.High, low = columns.Low, close = columns.Close, volume = columns.Volume;
        var n = close.Length;
        var details = owner.IncludeDetails;
        var firstValue = owner.FirstValue;
        var fast = new Seeded(owner.FastPeriod, firstValue, 2.0 / (owner.FastPeriod + 1.0));
        var slow = new Seeded(owner.SlowPeriod, firstValue, 2.0 / (owner.SlowPeriod + 1.0));
        var start = (int)Math.Min((long)Math.Max(owner.FastPeriod, owner.SlowPeriod) - 1 + owner.Suppression, n);
        Span<double> oscillator = outputs[0];
        oscillator.Slice(0, start).Clear();
        // Without details the three detail series are zero throughout and their flags never turn on.
        for (var slot = 1; slot < 4 && !details; slot++) outputs.Flag(slot, n);
        Span<double> multipliers = details ? outputs[1] : default, flows = details ? outputs[2] : default,
            lines = details ? outputs[3] : default;
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
        outputs.Flag(4, start);
        for (var slot = 5; slot < 8; slot++) outputs.Flag(slot, details ? 0 : n);
    }


    /// <summary>
    /// True range, its Wilder average seeded by the mean of the first ranges, and that average as a
    /// percentage of the close, each with a flag for the bars that have it.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    internal static void AverageTrueRangeDetails(FastColumns columns, FastOutputs outputs, int period)
    {
        columns.RequireHighLowClose();
        ReadOnlySpan<double> high = columns.High, low = columns.Low, close = columns.Close;
        var n = close.Length;
        for (var slot = 0; slot < 6; slot++) outputs[slot].AsSpan(0, n).Clear();
        Span<double> range = outputs[0].AsSpan(0, n), average = outputs[1].AsSpan(0, n), percent = outputs[2].AsSpan(0, n);
        for (var i = 1; i < n; i++) range[i] = TrueRange(high[i], low[i], close[i - 1]);
        if (n > 1) outputs.Flag(3, 1);
        if (n <= period) return;
        double sum = 0;
        for (var i = 1; i <= period; i++) sum += range[i];
        var value = sum / period;
        average[period] = value;
        var weight = (period - 1.0) / period;
        // The same recurrence as an exponential average with alpha = 1 / period.
        FastMath.BlendRun(weight, 1.0 / period, range, average, period + 1, value);
        outputs.Flag(4, period);
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
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
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
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
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
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
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
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
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


    /// <summary>
    /// Vortex: the summed upward and downward movement over the window, each over the summed true range,
    /// published where the window is full and has any range at all.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    internal static void Vortex(FastColumns columns, double[][] outputs, int period)
    {
        columns.RequireHighLowClose();
        ReadOnlySpan<double> high = columns.High, low = columns.Low, close = columns.Close;
        var n = close.Length;
        for (var slot = 0; slot < 4; slot++) outputs[slot].AsSpan(0, n).Clear();
        if (n <= period) return;
        Span<double> positive = outputs[0].AsSpan(0, n), negative = outputs[1].AsSpan(0, n);
        Span<double> positiveDefined = outputs[2].AsSpan(0, n), negativeDefined = outputs[3].AsSpan(0, n);
        double ranges = 0, ups = 0, downs = 0;
        // A sum of magnitudes is zero only when every term is, so the count of non-zero ranges decides it exactly.
        var moving = 0;
        for (var i = 1; i < n; i++)
        {
            if (i > period && (i & RebuildMask) == 0)
            {
                ranges = ups = downs = 0;
                moving = 0;
                for (var j = i - period + 1; j <= i; j++)
                {
                    var term = TrueRange(high[j], low[j], close[j - 1]);
                    ranges += term;
                    if (term != 0) moving++; // NOSONAR an exact tie is the contract
                    ups += Math.Abs(high[j] - low[j - 1]);
                    downs += Math.Abs(low[j] - high[j - 1]);
                }
            }
            else
            {
                if (i > period)
                {
                    var j = i - period;
                    var old = TrueRange(high[j], low[j], close[j - 1]);
                    ranges -= old;
                    if (old != 0) moving--; // NOSONAR an exact tie is the contract
                    ups -= Math.Abs(high[j] - low[j - 1]);
                    downs -= Math.Abs(low[j] - high[j - 1]);
                }
                var range = TrueRange(high[i], low[i], close[i - 1]);
                ranges += range;
                if (range != 0) moving++; // NOSONAR an exact tie is the contract
                ups += Math.Abs(high[i] - low[i - 1]);
                downs += Math.Abs(low[i] - high[i - 1]);
            }
            if (i < period || moving == 0) continue;
            positive[i] = ups / ranges;
            negative[i] = downs / ranges;
            positiveDefined[i] = negativeDefined[i] = 1;
        }
    }

    /// <summary>
    /// Choppiness index: the log of the summed true intervals over the span they cover, scaled by the log of
    /// the period, published where the window is full and spans a range.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    internal static void Choppiness(FastColumns columns, Span<double> value, Span<double> defined, int period)
    {
        columns.RequireHighLowClose();
        ReadOnlySpan<double> high = columns.High, low = columns.Low, close = columns.Close;
        var n = close.Length;
        value = value.Slice(0, n);
        defined = defined.Slice(0, n);
        value.Clear();
        defined.Clear();
        if (n <= period) return;
        var count = n - 1;
        var pool = System.Buffers.ArrayPool<double>.Shared;
        double[] rentedTop = pool.Rent(count), rentedBottom = pool.Rent(count), rentedHighest = pool.Rent(count),
            rentedLowest = pool.Rent(count);
        try
        {
            Span<double> top = rentedTop.AsSpan(0, count), bottom = rentedBottom.AsSpan(0, count);
            Span<double> highest = rentedHighest.AsSpan(0, count), lowest = rentedLowest.AsSpan(0, count);
            for (var i = 1; i < n; i++)
            {
                top[i - 1] = Math.Max(high[i], close[i - 1]);
                bottom[i - 1] = Math.Min(low[i], close[i - 1]);
            }
            RollingExtreme(top, highest, period, true);
            RollingExtreme(bottom, lowest, period, false);
            var scale = 100 / Math.Log(period);
            double sum = 0;
            for (var k = 0; k < count; k++)
            {
                if (k >= period && (k & RebuildMask) == 0)
                {
                    sum = 0;
                    for (var j = k - period + 1; j <= k; j++) sum += top[j] - bottom[j];
                }
                else
                {
                    if (k >= period) sum -= top[k - period] - bottom[k - period];
                    sum += top[k] - bottom[k];
                }
                if (k < period - 1) continue;
                var span = highest[k] - lowest[k];
                if (span == 0) continue; // NOSONAR an exact tie is the contract
                value[k + 1] = scale * Math.Log(sum / span);
                defined[k + 1] = 1;
            }
        }
        finally
        {
            pool.Return(rentedTop);
            pool.Return(rentedBottom);
            pool.Return(rentedHighest);
            pool.Return(rentedLowest);
        }
    }


    /// <summary>
    /// Chandelier exits: the window's highest high less a multiple of the average true range, and its
    /// lowest low plus the same, from the bar the average first exists.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    internal static void ChandelierExit(FastColumns columns, double[][] outputs, int period, double multiplier,
        ChandelierExitSelection selection, bool zeroFloorHigh)
    {
        columns.RequireHighLowClose();
        ReadOnlySpan<double> high = columns.High, low = columns.Low, close = columns.Close;
        var n = close.Length;
        for (var slot = 0; slot < 4; slot++) outputs[slot].AsSpan(0, n).Clear();
        if (n <= period) return;
        var pool = System.Buffers.ArrayPool<double>.Shared;
        double[] rentedAverage = pool.Rent(n), rentedExtreme = pool.Rent(n);
        try
        {
            Span<double> average = rentedAverage.AsSpan(0, n), extreme = rentedExtreme.AsSpan(0, n);
            SeededAverageTrueRange(high, low, close, average, period, false);
            var count = n - period;
            if (selection != ChandelierExitSelection.Short)
            {
                RollingExtreme(high, extreme, period, true);
                var exit = outputs[0].AsSpan(0, n);
                for (var i = period; i < n; i++)
                    exit[i] = (zeroFloorHigh ? Math.Max(0, extreme[i]) : extreme[i]) - multiplier * average[i];
                outputs[2].AsSpan(period, count).Fill(1);
            }
            if (selection != ChandelierExitSelection.Long)
            {
                RollingExtreme(low, extreme, period, false);
                var exit = outputs[1].AsSpan(0, n);
                for (var i = period; i < n; i++) exit[i] = extreme[i] + multiplier * average[i];
                outputs[3].AsSpan(period, count).Fill(1);
            }
        }
        finally
        {
            pool.Return(rentedAverage);
            pool.Return(rentedExtreme);
        }
    }


    /// <summary>
    /// Fisher transform of where the price sits in its rolling range, with the previous value as trigger.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    internal static void FisherTransform(FastColumns columns, FastOutputs outputs, int period, bool useMidpoint)
    {
        var n = columns.Count;
        Span<double> fisher = outputs[0].AsSpan(0, n), trigger = outputs[1].AsSpan(0, n);
        fisher.Clear();
        trigger.Clear();
        outputs.Flag(2, 0);
        outputs[3].AsSpan(0, n).Clear();
        if (n == 0) return;
        if (n > 1) outputs.Flag(3, 1);
        var pool = System.Buffers.ArrayPool<double>.Shared;
        double[] rentedPrice = pool.Rent(n), rentedLow = pool.Rent(n), rentedHigh = pool.Rent(n);
        try
        {
            Span<double> price = rentedPrice.AsSpan(0, n), lowest = rentedLow.AsSpan(0, n), highest = rentedHigh.AsSpan(0, n);
            if (useMidpoint)
            {
                ReadOnlySpan<double> high = columns.High, low = columns.Low;
                for (var i = 0; i < n; i++) price[i] = 0.5 * high[i] + 0.5 * low[i];
            }
            else columns.Close.AsSpan(0, n).CopyTo(price);
            RollingExtreme(price, lowest, period, false);
            RollingExtreme(price, highest, period, true);
            double position = 0, value = 0;
            for (var i = 1; i < n; i++)
            {
                var range = highest[i] - lowest[i];
                position = range == 0 ? 0 // NOSONAR an exact tie is the contract
                    : .33 * 2 * ((price[i] - lowest[i]) / range - .5) + .67 * position;
                if (position > .99) position = .999;
                if (position < -.99) position = -.999;
                trigger[i] = value;
                value = .5 * Math.Log((1 + position) / (1 - position)) + .5 * value;
                fisher[i] = value;
            }
        }
        finally
        {
            pool.Return(rentedPrice);
            pool.Return(rentedLow);
            pool.Return(rentedHigh);
        }
    }

    /// <summary>
    /// Chaikin money flow with its parts: the money-flow multiplier, the bar's flow, and the window's
    /// summed flow over its summed volume where that volume is not zero.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    internal static void MoneyFlowDetails(FastColumns columns, double[][] outputs, int period)
    {
        columns.RequireHighLowClose();
        ReadOnlySpan<double> high = columns.High, low = columns.Low, close = columns.Close, volume = columns.Volume;
        var n = close.Length;
        Span<double> multipliers = outputs[0].AsSpan(0, n), flows = outputs[1].AsSpan(0, n);
        Span<double> ratio = outputs[2].AsSpan(0, n), defined = outputs[3].AsSpan(0, n);
        ratio.Clear();
        defined.Clear();
        for (var i = 0; i < n; i++)
        {
            var range = high[i] - low[i];
            double multiplier = 0, flow = 0;
            if (range != 0) // NOSONAR an exact tie is the contract
            {
                multiplier = (2 * close[i] - high[i] - low[i]) / range;
                flow = multiplier * volume[i];
            }
            multipliers[i] = multiplier;
            flows[i] = flow;
        }
        double flowSum = 0, volumeSum = 0, volumeSize = 0;
        for (var i = 0; i < n; i++)
        {
            // An infinite part fails the build when the outputs are checked.
            if (double.IsInfinity(multipliers[i]) || double.IsInfinity(flows[i])) return;
            if (i >= period && (i & RebuildMask) == 0)
            {
                flowSum = volumeSum = volumeSize = 0;
                for (var j = i - period + 1; j <= i; j++)
                {
                    flowSum += flows[j];
                    volumeSum += volume[j];
                    volumeSize += Math.Abs(volume[j]);
                }
            }
            else
            {
                if (i >= period)
                {
                    var old = i - period;
                    flowSum -= flows[old];
                    volumeSum -= volume[old];
                    volumeSize -= Math.Abs(volume[old]);
                }
                flowSum += flows[i];
                volumeSum += volume[i];
                volumeSize += Math.Abs(volume[i]);
            }
            if (i < period - 1) continue;
            var total = volumeSum;
            // Where the summed volume is not clearly away from zero, the window is summed again in order
            // and, if that is still not decisive, exactly.
            if (Math.Abs(total) <= 1e-9 * volumeSize || volumeSize <= 0)
            {
                var exact = new ExactMeanAccumulator();
                for (var j = i - period + 1; j <= i; j++) exact.Add(volume[j]);
                if (exact.IsExactlyZero) continue;
                total = exact.Mean(1);
            }
            ratio[i] = flowSum / total;
            defined[i] = 1;
        }
    }


    /// <summary>
    /// Gator: the distance between the alligator's jaw and teeth above zero and between its teeth and lips
    /// below, each with whether it grew since the bar before.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    internal static void GatorDetails(FastColumns columns, double[][] outputs, AlligatorWithDetails alligator)
    {
        var n = columns.Count;
        for (var slot = 0; slot < 8; slot++) outputs[slot].AsSpan(0, n).Clear();
        if (n == 0) return;
        var pool = System.Buffers.ArrayPool<double>.Shared;
        var lines = new double[6][];
        for (var slot = 0; slot < 6; slot++) lines[slot] = pool.Rent(n);
        try
        {
            AlligatorDetails(columns, lines, alligator);
            GatorSide(lines[0], lines[1], lines[3], lines[4], outputs[0], outputs[2], outputs[4], outputs[6], n, 1);
            GatorSide(lines[1], lines[2], lines[4], lines[5], outputs[1], outputs[3], outputs[5], outputs[7], n, -1);
        }
        finally
        {
            for (var slot = 0; slot < 6; slot++) pool.Return(lines[slot]);
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(FastMath.Hot)]
    private static void GatorSide(double[] a, double[] b, double[] aDefined, double[] bDefined, double[] value,
        double[] expanding, double[] valueDefined, double[] expandingDefined, int n, int sign)
    {
        var had = false;
        double previous = 0;
        for (var i = 0; i < n; i++)
        {
            var has = aDefined[i] > 0 && bDefined[i] > 0;
            double current = 0;
            if (has)
            {
                current = sign * Math.Abs(a[i] - b[i]);
                value[i] = current;
                valueDefined[i] = 1;
            }
            if (had)
            {
                expandingDefined[i] = 1;
                // The averages carry a few units of rounding, which is not growth on a flat series.
                var noise = 16 * Ulp * (Math.Abs(a[i]) + Math.Abs(b[i]));
                if (has && sign * (current - previous) > noise) expanding[i] = 1;
            }
            had = has;
            previous = current;
        }
    }
}
