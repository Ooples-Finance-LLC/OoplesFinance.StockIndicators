namespace OoplesFinance.StockIndicators.Indicators.Fast;

internal static partial class FastKernels
{
    /// <summary>
    /// Wilder directional movement, indicator, index, average index or rating, seeded by the sum of the bars
    /// before the period completes. Each stage keeps the running-sum form previous - previous / period +
    /// current, so the movement outputs are sums rather than averages.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    internal static void Directional(FastColumns columns, FastOutputs outputs, PriorSeededDirectionalMeasure owner)
    {
        var measure = owner.Measure;
        var period = owner.Period;
        long unstable = owner.UnstablePeriod;
        var n = columns.Count;
        // The first bar each measure publishes; every bar from it on is published.
        var first = measure switch
        {
            PriorDirectionalMeasure.Average => 2L * period - 1 + unstable,
            PriorDirectionalMeasure.Rating => 3L * period - 2 + unstable,
            _ => period + unstable
        };
        // The movements, a period of one bar and a series too short to publish take the general loop.
        if (measure < PriorDirectionalMeasure.PositiveIndicator || period < 2 || first >= n)
        {
            DirectionalGeneral(columns, outputs[0], outputs[1], owner);
            return;
        }
        columns.RequireHighLowClose();
        ReadOnlySpan<double> high = columns.High, low = columns.Low, close = columns.Close;
        var start = (int)first;
        Span<double> value = outputs[0].AsSpan(0, n);
        value.Slice(0, start).Clear();
        outputs.Flag(1, start);
        // The work is staged, each stage a short loop of its own: the three smoothed sums, which wait on
        // one another bar to bar, then the quotients, which do not and are taken a vector of bars at a time.
        var pool = System.Buffers.ArrayPool<double>.Shared;
        double[] positives = pool.Rent(n), negatives = pool.Rent(n), ranges = pool.Rent(n);
        SmoothDirections(high, low, close, positives, negatives, ranges, period,
            measure != PriorDirectionalMeasure.NegativeIndicator, measure != PriorDirectionalMeasure.PositiveIndicator,
            measure == PriorDirectionalMeasure.Index ? period + unstable : long.MaxValue);
        if (measure <= PriorDirectionalMeasure.NegativeIndicator)
        {
            ReadOnlySpan<double> side = measure == PriorDirectionalMeasure.PositiveIndicator ? positives : negatives;
            HundredQuotients(side.Slice(start, n - start), ranges.AsSpan(start, n - start), value.Slice(start));
        }
        else
        {
            // From here the positives hold each bar's index and the negatives the total it was divided by,
            // which is zero on a bar with no direction.
            DirectionalIndexes(positives.AsSpan(period, n - period), negatives.AsSpan(period, n - period),
                ranges.AsSpan(period, n - period));
            ReadOnlySpan<double> indexes = positives, totals = negatives;
            if (measure == PriorDirectionalMeasure.Index)
            {
                double last = 0;
                for (var i = start; i < n; i++)
                {
                    if (i == start || totals[i] != 0) last = indexes[i]; // NOSONAR an exact zero is the contract
                    value[i] = last;
                }
            }
            else
            {
                var seedIndex = 2 * period - 1;
                var rating = measure == PriorDirectionalMeasure.Rating;
                // A rating pairs each average with the one period - 1 bars earlier, so its averages are kept.
                var averages = rating ? ranges.AsSpan(0, n) : value;
                var available = (int)(seedIndex + unstable);
                double average = 0;
                for (var i = period; i <= seedIndex; i++) average += indexes[i];
                average /= period;
                if (available == seedIndex) averages[seedIndex] = average;
                for (var i = seedIndex + 1; i < n; i++)
                {
                    if (totals[i] != 0) average = (average * (period - 1) + indexes[i]) / period; // NOSONAR an exact zero is the contract
                    if (i >= available) averages[i] = average;
                }
                if (rating)
                    for (var i = start; i < n; i++) value[i] = (averages[i] + averages[i - (period - 1)]) / 2;
            }
        }
        pool.Return(positives);
        pool.Return(negatives);
        pool.Return(ranges);
    }

    // The smoothed positive movement, negative movement and true range of every bar from the period's last
    // on: sums over the bars before the period completes, then previous - previous / period + current.
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    private static void SmoothDirections(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close,
        Span<double> positives, Span<double> negatives, Span<double> ranges, int period, bool needsPositive, bool needsNegative,
        long foldedFrom)
    {
        var n = high.Length;
        var weight = (period - 1.0) / period;
        // Each bar's own movements and range first: they wait on nothing, so a vector of bars is taken at a
        // time, and the sums that follow have only themselves to wait on.
        var bar = 1;
#if !NETFRAMEWORK
        if (System.Numerics.Vector.IsHardwareAccelerated)
        {
            var width = System.Numerics.Vector<double>.Count;
            var zero = System.Numerics.Vector<double>.Zero;
            System.Numerics.Vector<long> keepPositive = needsPositive ? System.Numerics.Vector<long>.AllBitsSet : System.Numerics.Vector<long>.Zero,
                keepNegative = needsNegative ? System.Numerics.Vector<long>.AllBitsSet : System.Numerics.Vector<long>.Zero;
            for (; bar <= n - width; bar += width)
            {
                System.Numerics.Vector<double> h = new System.Numerics.Vector<double>(high.Slice(bar, width)), l = new System.Numerics.Vector<double>(low.Slice(bar, width));
                var previous = new System.Numerics.Vector<double>(close.Slice(bar - 1, width));
                System.Numerics.Vector<double> up = h - new System.Numerics.Vector<double>(high.Slice(bar - 1, width)), down = new System.Numerics.Vector<double>(low.Slice(bar - 1, width)) - l;
                // Which way a bar moved is as good as random, so each movement is kept or zeroed by a mask.
                System.Numerics.Vector.ConditionalSelect(keepPositive & System.Numerics.Vector.GreaterThan(up, zero) & System.Numerics.Vector.GreaterThan(up, down), up, zero)
                    .CopyTo(positives.Slice(bar, width));
                System.Numerics.Vector.ConditionalSelect(keepNegative & System.Numerics.Vector.GreaterThan(down, zero) & System.Numerics.Vector.GreaterThan(down, up), down, zero)
                    .CopyTo(negatives.Slice(bar, width));
                System.Numerics.Vector<double> fromHigh = System.Numerics.Vector.Abs(h - previous), fromLow = System.Numerics.Vector.Abs(l - previous);
                var gap = System.Numerics.Vector.ConditionalSelect(System.Numerics.Vector.GreaterThan(fromHigh, fromLow), fromHigh, fromLow);
                var spread = h - l;
                System.Numerics.Vector.ConditionalSelect(System.Numerics.Vector.GreaterThan(spread, gap), spread, gap).CopyTo(ranges.Slice(bar, width));
            }
        }
#endif
        for (; bar < n; bar++)
        {
            double up = high[bar] - high[bar - 1], down = low[bar - 1] - low[bar];
            // Every test is evaluated on purpose: a short circuit would be a branch the data decides.
            positives[bar] = FastMath.KeepIf(up, needsPositive & up > 0 & up > down); // NOSONAR no short circuit
            negatives[bar] = FastMath.KeepIf(down, needsNegative & down > 0 & down > up); // NOSONAR no short circuit
            ranges[bar] = TrueRange(high[bar], low[bar], close[bar - 1]);
        }
        double positive = 0, negative = 0, range = 0;
        var seeded = Math.Min(period, n);
        for (var i = 1; i < seeded; i++)
        {
            positive += positives[i];
            negative += negatives[i];
            range += ranges[i];
        }
        var plain = foldedFrom >= n ? n : (int)Math.Max(foldedFrom + 1, period);
        for (var i = seeded; i < plain; i++)
        {
            positive = FastMath.Blend(weight, positive, positives[i]);
            negative = FastMath.Blend(weight, negative, negatives[i]);
            range = FastMath.Blend(weight, range, ranges[i]);
            positives[i] = positive;
            negatives[i] = negative;
            ranges[i] = range;
        }
        // The index folds the bar's own range in a second time once it is past its first value. The range
        // cancels out of the index, so this only keeps the stage identical to the exact state.
        for (var i = Math.Max(plain, seeded); i < n; i++)
        {
            positive = FastMath.Blend(weight, positive, positives[i]);
            negative = FastMath.Blend(weight, negative, negatives[i]);
            range = FastMath.Blend(weight, range, ranges[i]);
            range = FastMath.Blend(weight, range, TrueRange(high[i], low[i], close[i]));
            positives[i] = positive;
            negatives[i] = negative;
            ranges[i] = range;
        }
    }

    // A hundred times each numerator over its denominator, zero where either is zero.
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    private static void HundredQuotients(ReadOnlySpan<double> numerators, ReadOnlySpan<double> denominators, Span<double> output)
    {
        var i = 0;
#if !NETFRAMEWORK
        if (System.Numerics.Vector.IsHardwareAccelerated)
        {
            var width = System.Numerics.Vector<double>.Count;
            System.Numerics.Vector<double> zero = System.Numerics.Vector<double>.Zero, hundred = new System.Numerics.Vector<double>(100.0);
            for (; i <= output.Length - width; i += width)
            {
                var numerator = hundred * new System.Numerics.Vector<double>(numerators.Slice(i, width));
                var denominator = new System.Numerics.Vector<double>(denominators.Slice(i, width));
                var none = System.Numerics.Vector.Equals(numerator, zero) | System.Numerics.Vector.Equals(denominator, zero);
                System.Numerics.Vector.ConditionalSelect(none, zero, numerator / denominator).CopyTo(output.Slice(i, width));
            }
        }
#endif
        for (; i < output.Length; i++) output[i] = Quotient(100 * numerators[i], denominators[i]);
    }

    // Each bar's directional index from its smoothed movements and range, written over the positives, and
    // the sum of its two indicators, written over the negatives: zero there is a bar with no direction.
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    private static void DirectionalIndexes(Span<double> positives, Span<double> negatives, ReadOnlySpan<double> ranges)
    {
        var i = 0;
#if !NETFRAMEWORK
        if (System.Numerics.Vector.IsHardwareAccelerated)
        {
            var width = System.Numerics.Vector<double>.Count;
            System.Numerics.Vector<double> zero = System.Numerics.Vector<double>.Zero, hundred = new System.Numerics.Vector<double>(100.0);
            for (; i <= ranges.Length - width; i += width)
            {
                var range = new System.Numerics.Vector<double>(ranges.Slice(i, width));
                var plus = hundred * new System.Numerics.Vector<double>(positives.Slice(i, width));
                var minus = hundred * new System.Numerics.Vector<double>(negatives.Slice(i, width));
                var flat = System.Numerics.Vector.Equals(range, zero);
                plus = System.Numerics.Vector.ConditionalSelect(System.Numerics.Vector.Equals(plus, zero) | flat, zero, plus / range);
                minus = System.Numerics.Vector.ConditionalSelect(System.Numerics.Vector.Equals(minus, zero) | flat, zero, minus / range);
                var total = plus + minus;
                System.Numerics.Vector.ConditionalSelect(System.Numerics.Vector.Equals(total, zero), zero, hundred * System.Numerics.Vector.Abs(plus - minus) / total)
                    .CopyTo(positives.Slice(i, width));
                total.CopyTo(negatives.Slice(i, width));
            }
        }
#endif
        for (; i < ranges.Length; i++)
        {
            double plus = Quotient(100 * positives[i], ranges[i]), minus = Quotient(100 * negatives[i], ranges[i]);
            var total = plus + minus;
            positives[i] = total != 0 ? 100 * Math.Abs(plus - minus) / total : 0; // NOSONAR an exact zero is the contract
            negatives[i] = total;
        }
    }

    // The general loop: every measure, any period, one bar at a time.
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    private static void DirectionalGeneral(FastColumns columns, Span<double> value, Span<double> defined,
        PriorSeededDirectionalMeasure owner)
    {
        var measure = owner.Measure;
        var period = owner.Period;
        long unstable = owner.UnstablePeriod;
        var needsPositive = measure is not (PriorDirectionalMeasure.NegativeMovement or PriorDirectionalMeasure.NegativeIndicator);
        var needsNegative = measure is not (PriorDirectionalMeasure.PositiveMovement or PriorDirectionalMeasure.PositiveIndicator);
        var needsRange = measure >= PriorDirectionalMeasure.PositiveIndicator;
        var movementOnly = measure <= PriorDirectionalMeasure.NegativeMovement;
        var positiveSide = measure is PriorDirectionalMeasure.PositiveMovement or PriorDirectionalMeasure.PositiveIndicator;
        if (needsRange) columns.RequireHighLowClose();
        ReadOnlySpan<double> high = columns.High, low = columns.Low;
        // The movement measures never read the close, so it is neither gathered nor validated for them.
        var close = needsRange ? columns.Close : default(ReadOnlySpan<double>);
        var n = high.Length;
        value = value.Slice(0, n);
        defined = defined.Slice(0, n);
        value.Clear();
        defined.Clear();
        var weight = (period - 1.0) / period;
        var firstDx = period + unstable;
        var seedIndex = 2L * period - 1;
        double positive = 0, negative = 0, range = 0, positiveSeed = 0, negativeSeed = 0, rangeSeed = 0;
        double seedDx = 0, average = 0, lastDx = 0;
        // The rating pairs an average with the one period - 1 bars earlier; NaN marks one not yet available.
        var history = measure == PriorDirectionalMeasure.Rating ? new double[period - 1] : null;
        var slot = 0;
        for (var i = 1; i < n; i++)
        {
            var up = high[i] - high[i - 1];
            var down = low[i - 1] - low[i];
            // Which way a bar moved is as good as random, so each movement is kept or zeroed without a branch.
            var plus = FastMath.KeepIf(up, needsPositive & up > 0 & up > down);
            var minus = FastMath.KeepIf(down, needsNegative & down > 0 & down > up);
            var trueRange = needsRange ? TrueRange(high[i], low[i], close[i - 1]) : 0;
            if (period == 1)
            {
                var movement = positiveSide ? plus : minus;
                value[i] = movementOnly ? movement : Quotient(movement, trueRange);
                defined[i] = 1;
                continue;
            }
            if (i < period)
            {
                positiveSeed += plus;
                negativeSeed += minus;
                rangeSeed += trueRange;
                if (i == period - 1)
                {
                    positive = positiveSeed;
                    negative = negativeSeed;
                    range = rangeSeed;
                }
            }
            else
            {
                positive = FastMath.Blend(weight, positive, plus);
                negative = FastMath.Blend(weight, negative, minus);
                range = FastMath.Blend(weight, range, trueRange);
            }
            if (movementOnly)
            {
                if (i >= period - 1 + unstable)
                {
                    value[i] = positiveSide ? positive : negative;
                    defined[i] = 1;
                }
                continue;
            }
            if (i < period) continue;
            // The index folds the bar's own range in a second time once it is past its first value. The range
            // cancels out of the index, so this only keeps the stage identical to the exact state.
            if (measure == PriorDirectionalMeasure.Index && i > firstDx)
                range = FastMath.Blend(weight, range, TrueRange(high[i], low[i], close[i]));
            if (measure <= PriorDirectionalMeasure.NegativeIndicator)
            {
                if (i >= firstDx)
                {
                    value[i] = Quotient(100 * (positiveSide ? positive : negative), range);
                    defined[i] = 1;
                }
                continue;
            }
            var plusIndicator = Quotient(100 * positive, range);
            var minusIndicator = Quotient(100 * negative, range);
            var total = plusIndicator + minusIndicator;
            var hasDirection = total != 0;
            var index = hasDirection ? 100 * Math.Abs(plusIndicator - minusIndicator) / total : 0;
            if (measure == PriorDirectionalMeasure.Index)
            {
                if (i < firstDx) continue;
                if (i == firstDx || hasDirection) lastDx = index;
                value[i] = lastDx;
                defined[i] = 1;
                continue;
            }
            if (i <= seedIndex)
            {
                seedDx += index;
                if (i == seedIndex) average = seedDx / period;
            }
            else if (hasDirection)
                average = (average * (period - 1) + index) / period;
            var available = i >= seedIndex + unstable;
            if (history is null)
            {
                if (available)
                {
                    value[i] = average;
                    defined[i] = 1;
                }
                continue;
            }
            // The slot walks round the history one bar at a time, which spares a remainder on every bar.
            var prior = i - period >= history.Length ? history[slot] : double.NaN;
            history[slot] = available ? average : double.NaN;
            if (++slot == history.Length) slot = 0;
            if (available && !double.IsNaN(prior))
            {
                value[i] = (average + prior) / 2;
                defined[i] = 1;
            }
        }
    }

    // A zero numerator or denominator gives zero, as the exact ratio does.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | FastMath.Hot)]
    private static double Quotient(double numerator, double denominator) =>
        numerator == 0 || denominator == 0 ? 0 : numerator / denominator;
}
