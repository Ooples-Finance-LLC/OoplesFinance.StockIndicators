using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators.Fast;

internal static partial class FastKernels
{
    /// <summary>
    /// Wilder average. Seeded, it publishes the running mean of the first <paramref name="period"/> values
    /// and then smooths; unseeded, it starts from the first value.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    internal static void WilderAverage(ReadOnlySpan<double> input, Span<double> output, int period, bool seedWithAverage)
    {
        var n = input.Length;
        output = output.Slice(0, n);
        if (n == 0) return;
        double value;
        int start;
        if (seedWithAverage)
        {
            start = Math.Min(period, n);
            double sum = 0;
            value = 0;
            for (var i = 0; i < start; i++)
            {
                sum += input[i];
                value = sum / (i + 1);
                output[i] = value;
            }
        }
        else
        {
            start = 1;
            value = input[0];
            output[0] = value;
        }
        var scale = 1.0 / period;
        var weight = (period - 1.0) / period;
        for (var i = start; i < n; i++)
        {
            value = FastMath.Blend(weight, value, scale * input[i]);
            output[i] = value;
        }
    }

    /// <summary>
    /// Double or triple exponential average whose stages start at zero and are divided by the weight they
    /// have accumulated so far, until that correction is too small to matter.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    internal static void CompensatedExponential(ReadOnlySpan<double> input, Span<double> output, int period, int order)
    {
        var n = input.Length;
        output = output.Slice(0, n);
        var alpha = 2.0 / (period + 1.0);
        var decay = 1 - alpha;
        var triple = order == 3;
        double residual = 1, first = 0, second = 0, third = 0;
        for (var i = 0; i < n; i++)
        {
            residual = residual > 1e-10 ? decay * residual : 0;
            var compensation = residual > 1e-10 ? 1 / (1 - residual) : 1;
            first += alpha * (input[i] - first);
            var one = first * compensation;
            second += alpha * (one - second);
            var two = second * compensation;
            if (!triple)
            {
                output[i] = 2 * one - two;
                continue;
            }
            third += alpha * (two - third);
            output[i] = 3 * one - 3 * two + third * compensation;
        }
    }

    /// <summary>Sum, difference, product or quotient of two price fields; a zero divisor publishes nothing.</summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    internal static void CandleArithmetic(FastColumns columns, FastOutputs outputs, CandleArithmetic owner)
    {
        // A sum, a difference or a product is finite only if both of its terms are, so checking the result
        // checks them. A quotient is not: a zero divisor hides what it divides.
        var checkedByResult = owner.Operation != CandleArithmeticOperation.Divide;
        ReadOnlySpan<double> left = checkedByResult ? columns.Raw(InputOf(owner.Left)) : Field(columns, owner.Left);
        ReadOnlySpan<double> right = checkedByResult ? columns.Raw(InputOf(owner.Right)) : Field(columns, owner.Right);
        var n = left.Length;
        Span<double> value = outputs[0].AsSpan(0, n);
        switch (owner.Operation)
        {
            case CandleArithmeticOperation.Add:
                FastMath.Add(left, right, value);
                break;
            case CandleArithmeticOperation.Subtract:
                FastMath.Subtract(left, right, value);
                break;
            case CandleArithmeticOperation.Multiply:
                for (var i = 0; i < n; i++) value[i] = left[i] * right[i];
                break;
            default:
                var undefined = 0;
                for (var i = 0; i < n; i++)
                {
                    var divisor = right[i];
                    if (divisor == 0)
                    {
                        value[i] = 0;
                        undefined++;
                    }
                    else
                        value[i] = left[i] / divisor;
                }
                if (undefined == 0) break;
                // A zero divisor is rare enough that its flags are worked out only once one has been met.
                Span<double> defined = outputs[1].AsSpan(0, n);
                for (var i = 0; i < n; i++) defined[i] = right[i] == 0 ? 0 : 1;
                return;
        }
        outputs.Flag(1, 0);
        if (!checkedByResult) return;
        if (FastMath.AllFinite(value))
        {
            outputs.Verified(0);
            return;
        }
        _ = Field(columns, owner.Left);
        _ = Field(columns, owner.Right);
    }

    [System.Runtime.CompilerServices.MethodImpl(FastMath.Hot)]
    private static IndicatorInputFields InputOf(CandlePriceField field) => field switch
    {
        CandlePriceField.Open => IndicatorInputFields.Open,
        CandlePriceField.High => IndicatorInputFields.High,
        CandlePriceField.Low => IndicatorInputFields.Low,
        CandlePriceField.Close => IndicatorInputFields.Close,
        _ => IndicatorInputFields.Volume
    };

    [System.Runtime.CompilerServices.MethodImpl(FastMath.Hot)]
    private static double[] Field(FastColumns columns, CandlePriceField field) => field switch
    {
        CandlePriceField.Open => columns.Open,
        CandlePriceField.High => columns.High,
        CandlePriceField.Low => columns.Low,
        _ => columns.Close,
    };

    /// <summary>
    /// Bar index of the highest or lowest of the last <paramref name="period"/> values, over the available
    /// history while the window fills. Equal values resolve to the newest or the oldest of them.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    internal static void RollingExtremeIndex(ReadOnlySpan<double> input, Span<double> index, long period, bool highest,
        bool newestTie)
    {
        var n = input.Length;
        index = index.Slice(0, n);
        if (n == 0) return;
        if (period > RescanLimit)
        {
            ExtremeIndexByDeque(input, index, period, highest, newestTie);
            return;
        }
        var best = input[0];
        var at = 0;
        index[0] = 0;
        for (var i = 1; i < n; i++)
        {
            var value = input[i];
            if (Replaces(value, best, highest, newestTie))
            {
                best = value;
                at = i;
            }
            else if (at <= i - period)
            {
                at = (int)(i - period + 1);
                best = input[at];
                for (var j = at + 1; j <= i; j++)
                    if (Replaces(input[j], best, highest, newestTie))
                    {
                        best = input[j];
                        at = j;
                    }
            }
            index[i] = at;
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | FastMath.Hot)]
    private static bool Replaces(double value, double best, bool highest, bool newestTie) =>
        highest ? (newestTie ? value >= best : value > best) : (newestTie ? value <= best : value < best);

    [System.Runtime.CompilerServices.MethodImpl(FastMath.Hot)]
    private static void ExtremeIndexByDeque(ReadOnlySpan<double> input, Span<double> index, long period, bool highest,
        bool newestTie)
    {
        // Candidates in bar order, in a queue as long as the series: every bar enters once, so neither end
        // ever wraps and no index needs a remainder.
        var queue = System.Buffers.ArrayPool<int>.Shared.Rent(input.Length);
        int head = 0, tail = 0;
        for (var i = 0; i < input.Length; i++)
        {
            var value = input[i];
            // A candidate the new value replaces can never be the extreme again.
            while (tail > head && Replaces(value, input[queue[tail - 1]], highest, newestTie)) tail--;
            if (tail > head && queue[head] <= i - period) head++;
            queue[tail++] = i;
            index[i] = queue[head];
        }
        System.Buffers.ArrayPool<int>.Shared.Return(queue);
    }

    /// <summary>
    /// Aroon: how recently the window's high and low occurred, as percentages of the period, and their
    /// difference. Nothing is published until the window is full.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    internal static void Aroon(FastColumns columns, FastOutputs outputs, WindowAroon owner)
    {
        ReadOnlySpan<double> high = columns.High, low = columns.Low;
        var n = high.Length;
        var period = owner.Period;
        var size = (long)period + (owner.IncludeExtraBar ? 1 : 0);
        Span<double> up = outputs[0].AsSpan(0, n), down = outputs[1].AsSpan(0, n), oscillator = outputs[2].AsSpan(0, n);
        var first = (int)Math.Min(size - 1, n);
        for (var slot = 3; slot < 6; slot++) outputs.Flag(slot, first);
        if (size <= AroonFusedLimit && n > 0)
        {
            FusedAroon(high, low, up, down, oscillator, period, (int)size, first, !owner.OldestTie);
            return;
        }
        // The two outputs hold the extreme's bar index until the final pass turns it into a percentage.
        RollingExtremeIndex(high, up, size, true, !owner.OldestTie);
        RollingExtremeIndex(low, down, size, false, !owner.OldestTie);
        up.Slice(0, first).Clear();
        down.Slice(0, first).Clear();
        oscillator.Slice(0, first).Clear();
        if (period > AroonTableLimit)
        {
            for (var i = first; i < n; i++)
            {
                long highest = (long)up[i], lowest = (long)down[i];
                up[i] = WindowAroon.Percentage(period - (i - highest), period);
                down[i] = WindowAroon.Percentage(period - (i - lowest), period);
                oscillator[i] = WindowAroon.Percentage(highest - lowest, period);
            }
            return;
        }
        // An extreme is at most a window old, so every percentage is one of a few values: they are divided
        // once each instead of three times a bar. Entry k holds the percentage of k - period.
        var percentages = new double[2 * period + 3];
        for (var k = 0; k < percentages.Length; k++) percentages[k] = WindowAroon.Percentage(k - period - 1L, period);
        for (var i = first; i < n; i++)
        {
            int highest = (int)up[i], lowest = (int)down[i];
            up[i] = percentages[2 * period + 1 - (i - highest)];
            down[i] = percentages[2 * period + 1 - (i - lowest)];
            oscillator[i] = percentages[period + 1 + (highest - lowest)];
        }
    }

    private const int AroonTableLimit = 4096;

    // Up to this window a rescan when an extreme expires is cheaper than a deque, as in RollingExtremeIndex.
    private const int AroonFusedLimit = 32;

    // Short windows in one pass: both extremes are followed together and each bar's three percentages are
    // written as soon as they are known, so nothing is stored and read back.
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Hot)]
    private static void FusedAroon(ReadOnlySpan<double> high, ReadOnlySpan<double> low, Span<double> up, Span<double> down,
        Span<double> oscillator, int period, int size, int first, bool newestTie)
    {
        var n = high.Length;
        up.Slice(0, first).Clear();
        down.Slice(0, first).Clear();
        oscillator.Slice(0, first).Clear();
        // Entry k holds the percentage of k - period - 1.
        var percentages = new double[2 * period + 3];
        for (var k = 0; k < percentages.Length; k++) percentages[k] = WindowAroon.Percentage(k - period - 1L, period);
        double highest = high[0], lowest = low[0];
        int highestAt = 0, lowestAt = 0;
        for (var i = 0; i < n; i++)
        {
            double h = high[i], l = low[i];
            if (newestTie ? h >= highest : h > highest)
            {
                highest = h;
                highestAt = i;
            }
            else if (highestAt <= i - size)
            {
                highestAt = i - size + 1;
                highest = high[highestAt];
                for (var j = highestAt + 1; j <= i; j++)
                    if (newestTie ? high[j] >= highest : high[j] > highest)
                    {
                        highest = high[j];
                        highestAt = j;
                    }
            }
            if (newestTie ? l <= lowest : l < lowest)
            {
                lowest = l;
                lowestAt = i;
            }
            else if (lowestAt <= i - size)
            {
                lowestAt = i - size + 1;
                lowest = low[lowestAt];
                for (var j = lowestAt + 1; j <= i; j++)
                    if (newestTie ? low[j] <= lowest : low[j] < lowest)
                    {
                        lowest = low[j];
                        lowestAt = j;
                    }
            }
            if (i < first) continue;
            up[i] = percentages[2 * period + 1 - (i - highestAt)];
            down[i] = percentages[2 * period + 1 - (i - lowestAt)];
            oscillator[i] = percentages[period + 1 + (highestAt - lowestAt)];
        }
    }

    /// <summary>Half the sum of the window's lowest and highest value.</summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    internal static void RangeMidpoint(ReadOnlySpan<double> lows, ReadOnlySpan<double> highs, Span<double> output, int period)
    {
        var n = lows.Length;
        output = output.Slice(0, n);
        var highest = FastMath.Allocate(n);
        RollingExtreme(lows, output, period, false);
        RollingExtreme(highs, highest, period, true);
        FastMath.Add(output, highest, output);
        FastMath.Scale(output, 0.5, output);
    }

    /// <summary>Sum of the last <paramref name="period"/> values; zero until the window is full.</summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    internal static void RollingSum(ReadOnlySpan<double> input, Span<double> output, int period)
    {
        var n = input.Length;
        output = output.Slice(0, n);
        double sum = 0;
        for (var i = 0; i < n; i++)
        {
            if (i >= period && (i & RebuildMask) == 0)
            {
                sum = 0;
                for (var j = i - period + 1; j <= i; j++) sum += input[j];
            }
            else
                sum += i >= period ? input[i] - input[i - period] : input[i];
            output[i] = i < period - 1 ? 0 : sum;
        }
    }

    /// <summary>
    /// The Wilder recurrence run backwards: the running mean while it seeds, then the previous value moved
    /// away from the new one by a period-th of their distance.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    internal static void ReverseWilder(ReadOnlySpan<double> input, Span<double> output, int period)
    {
        var n = input.Length;
        output = output.Slice(0, n);
        if (n == 0) return;
        var value = input[0];
        output[0] = value;
        var seeded = Math.Min(period, n);
        for (var i = 1; i < seeded; i++)
        {
            // Written as a step from the previous mean, so equal values stay exactly equal.
            value += (input[i] - value) / (i + 1);
            output[i] = value;
        }
        var scale = 1.0 / period;
        for (var i = Math.Max(seeded, 1); i < n; i++)
        {
            value += (value - input[i]) * scale;
            output[i] = value;
        }
    }


    /// <summary>
    /// Regularized exponential average: the usual step toward the close plus a pull along the previous
    /// slope, with the period growing to its setting over the first bars. The first two bars are the close.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    internal static void RegularizedEma(ReadOnlySpan<double> close, Span<double> output, int period, double lambda)
    {
        var n = close.Length;
        output = output.Slice(0, n);
        double previous = 0, older = 0;
        for (var i = 0; i < n; i++)
        {
            var result = close[i];
            if (i >= 2)
            {
                double effective = Math.Min(i + 1, period), mass = effective + 1;
                result = (previous * (effective - 1) + 2 * result + lambda * (2 * previous - older) * mass)
                    / ((1 + lambda) * mass);
            }
            older = previous;
            previous = result;
            output[i] = result;
        }
    }

    /// <summary>
    /// Zero-lag exponential average: an average of the close pushed ahead by its change over half the
    /// period, with the period growing to its setting over the first bars.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    internal static void ZeroLagEma(ReadOnlySpan<double> close, Span<double> output, int period)
    {
        var n = close.Length;
        output = output.Slice(0, n);
        var lag = (period - 1) / 2;
        double previous = 0;
        for (var i = 0; i < n; i++)
        {
            double count = Math.Min(i + 1, period);
            previous = (4 * close[i] - 2 * close[Math.Max(0, i - lag)] + previous * (count - 1)) / (count + 1);
            output[i] = previous;
        }
    }

    /// <summary>
    /// On-balance volume: volume added on a rising close and removed on a falling one, starting from the
    /// first bar's volume or from zero, with an optional window mean that starts at the second bar.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    internal static void OnBalanceVolume(FastColumns columns, FastOutputs outputs, SeededOnBalanceVolume owner)
    {
        // A close that is not a number compares as neither above nor below, which would hide it, so the
        // closes are checked as they are read. A volume that is not finite stays in the total for good.
        ReadOnlySpan<double> close = columns.Raw(IndicatorInputFields.Close), volume = columns.Raw(IndicatorInputFields.Volume);
        var n = close.Length;
        long suspect = n > 0 ? FastMath.NonFiniteBits(close[0]) | FastMath.NonFiniteBits(volume[0]) : 0;
        Span<double> value = outputs[0].AsSpan(0, n);
        // Without an average, or before one exists, its series is zero throughout and its flag never turns on.
        var averaged = owner.AveragePeriod is int length && n > length;
        if (!averaged)
        {
            outputs.Flag(1, n);
            outputs.Flag(2, n);
        }
        if (n == 0) return;
        var total = owner.Seed == ObvSeed.FirstVolume ? volume[0] : 0;
        value[0] = total;
        // Bars before this one hold their signed volume and wait for the running total.
        var signed = 1;
#if !NETFRAMEWORK
        if (System.Numerics.Vector.IsHardwareAccelerated)
        {
            // Each bar's volume, signed by which way its close moved, a vector of bars at a time; the
            // running total then adds the same terms in the same order.
            var width = System.Numerics.Vector<double>.Count;
            System.Numerics.Vector<double> zero = System.Numerics.Vector<double>.Zero, one = System.Numerics.Vector<double>.One;
            var seen = System.Numerics.Vector<long>.Zero;
            for (; signed <= n - width; signed += width)
            {
                var now = new System.Numerics.Vector<double>(close.Slice(signed, width));
                var then = new System.Numerics.Vector<double>(close.Slice(signed - 1, width));
                var direction = System.Numerics.Vector.ConditionalSelect(System.Numerics.Vector.GreaterThan(now, then), one, zero)
                    - System.Numerics.Vector.ConditionalSelect(System.Numerics.Vector.LessThan(now, then), one, zero);
                seen |= System.Numerics.Vector.AsVectorInt64(now - now); // NOSONAR x - x is the finite test
                (direction * new System.Numerics.Vector<double>(volume.Slice(signed, width))).CopyTo(value.Slice(signed, width));
            }
            if (!System.Numerics.Vector.EqualsAll(seen, System.Numerics.Vector<long>.Zero)) suspect = 1;
        }
#endif
        for (var i = 1; i < signed; i++)
        {
            total += value[i];
            value[i] = total;
        }
        for (var i = signed; i < n; i++)
        {
            double current = close[i], previous = close[i - 1];
#if !NETFRAMEWORK
            // The direction of a bar is as good as random, so it is taken without a branch.
            bool up = current > previous, down = current < previous;
            total += (System.Runtime.CompilerServices.Unsafe.As<bool, byte>(ref up)
                - System.Runtime.CompilerServices.Unsafe.As<bool, byte>(ref down)) * volume[i];
#else
            if (current > previous) total += volume[i];
            else if (current < previous) total -= volume[i];
            suspect |= FastMath.NonFiniteBits(volume[i]);
#endif
            suspect |= FastMath.NonFiniteBits(current);
            value[i] = total;
        }
        if (suspect == 0 && FastMath.NonFiniteBits(total) == 0) outputs.Verified(0);
        else
        {
            _ = columns.Close;
            _ = columns.Volume;
        }
        if (!averaged || owner.AveragePeriod is not int period) return;
        Span<double> average = outputs[1].AsSpan(0, n);
        average.Clear();
        RebuiltMean(value.Slice(1), average.Slice(1), period);
        outputs.Flag(2, period);
    }


    /// <summary>
    /// Four-stage Laguerre filter started from zero: each stage follows the one before it with the same
    /// damping, and the output weights the stages 1, 2, 2, 1.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    internal static void LaguerreFilter(ReadOnlySpan<double> close, Span<double> output, double gamma)
    {
        var n = close.Length;
        output = output.Slice(0, n);
        double first = 0, second = 0, third = 0, fourth = 0;
        for (var i = 0; i < n; i++)
        {
            var price = close[i];
            var nextFirst = price + gamma * (first - price);
            var nextSecond = first + gamma * (second - nextFirst);
            var nextThird = second + gamma * (third - nextSecond);
            var nextFourth = third + gamma * (fourth - nextThird);
            first = nextFirst;
            second = nextSecond;
            third = nextThird;
            fourth = nextFourth;
            output[i] = (first + 2 * second + 2 * third + fourth) / 6;
        }
    }


    // One exponential stage that divides by the weight it has accumulated until the unaccumulated remainder
    // is negligible, and is a plain exponential average after that.
    private struct MassNormalized(double alpha)
    {
        private double _mean, _mass;
        private double _residual = 1;

        [System.Runtime.CompilerServices.MethodImpl(FastMath.Hot)]
        internal double Next(double input)
        {
            if (_residual == 0) return _mean += alpha * (input - _mean);
            _residual = _residual > 1e-10 ? (1 - alpha) * _residual : 0;
            if (_mass == 0)
            {
                _mass = alpha;
                return _mean = input;
            }
            var previousWeight = (1 - alpha) * _mass;
            var weight = previousWeight + alpha;
            var numerator = previousWeight * _mean + input * alpha;
            _mean = _residual == 0 ? numerator : numerator / weight;
            _mass = _residual == 0 ? 1 : weight;
            return _mean;
        }
    }

    /// <summary>
    /// Quadruple exponential average of four chained mass-normalized stages, combined 4, -6, 4, -1.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    internal static void QuadrupleEma(ReadOnlySpan<double> close, Span<double> output, MassNormalizedQuadrupleEma owner)
    {
        var n = close.Length;
        output = output.Slice(0, n);
        MassNormalized first = new(owner.Alpha1), second = new(owner.Alpha2), third = new(owner.Alpha3),
            fourth = new(owner.Alpha4);
        for (var i = 0; i < n; i++)
        {
            var one = first.Next(close[i]);
            var two = second.Next(one);
            var three = third.Next(two);
            output[i] = 4 * one - 6 * two + 4 * three - fourth.Next(three);
        }
    }


    /// <summary>
    /// McGinley dynamic: each step moves toward the price by the gap over factor * period, scaled by the
    /// fourth power of average over price, so it speeds up when the price falls below it. The delayed
    /// start restarts from a zero average and withholds the value for a period after that.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    internal static void QuarticDynamic(ReadOnlySpan<double> close, Span<double> value, Span<double> defined,
        QuarticDynamicAverage owner)
    {
        var n = close.Length;
        value = value.Slice(0, n);
        defined = defined.Slice(0, n);
        value.Clear();
        defined.Clear();
        if (n == 0) return;
        var immediate = owner.Startup == McGinleyStartup.Immediate;
        var scale = owner.Factor * owner.Period;
        var period = owner.Period;
        long start = 1;
        var previous = close[0];
        var first = 0;
        if (immediate)
        {
            value[0] = previous;
            defined[0] = 1;
            first = 1;
        }
        for (var i = first; i < n; i++)
        {
            var price = close[i];
            if (!immediate && previous == 0)
            {
                previous = price;
                start = (long)i + period;
                continue;
            }
            double next;
            if (previous == 0) next = price / scale;
            else if (price == 0) next = previous > 0 ? double.NegativeInfinity : double.PositiveInfinity;
            else
            {
                var ratio = previous / price;
                var square = ratio * ratio;
                next = previous + (price - previous) * (square * square) / scale;
            }
            if (double.IsInfinity(next) || double.IsNaN(next))
            {
                // Published without a flag, which is what the output check rejects.
                value[i] = next;
                continue;
            }
            previous = next;
            if (!immediate && i < start) continue;
            value[i] = next;
            defined[i] = 1;
        }
    }


    /// <summary>
    /// Deviation-scaled average: an exponential average whose step grows with how large the smoothed
    /// distance of the price from the average is against its recent root mean square.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    internal static void DeviationScaled(ReadOnlySpan<double> close, Span<double> output, int period, double scaleFactor)
    {
        var n = close.Length;
        output = output.Slice(0, n);
        if (n == 0) return;
        var a = Math.Exp(-1.414 * Math.PI / (.5 * period));
        var b = 2 * a * Math.Cos(1.414 * Math.PI / (.5 * period));
        var c = -a * a;
        var half = (1 - b - c) / 2;
        var squares = new double[period];
        var average = close[0];
        output[0] = average;
        double previousZero = 0, filter = 0, older = 0, sum = 0, largest = 0;
        // Consecutive bars whose filter is exactly zero: a window of them has a sum of exactly zero.
        int flat = 0, slot = 0, count = 0, slid = 0;
        for (var i = 1; i < n; i++)
        {
            var price = close[i];
            var zero = price - average;
            var next = half * (zero + previousZero) + b * filter + c * older;
            var square = next * next;
            if (count == period) sum -= squares[slot];
            else count++;
            squares[slot] = square;
            slot = slot + 1 == period ? 0 : slot + 1;
            sum += square;
            largest = Math.Max(largest, square);
            flat = square == 0 ? flat + 1 : 0;
            // The running sum is rebuilt at intervals and whenever it has fallen far enough below what
            // passed through it that the rounding it kept could matter.
            if (++slid > RebuildMask || !(sum > largest * (1.0 / 1024)))
            {
                sum = 0;
                for (var j = 0; j < count; j++) sum += squares[j];
                largest = sum;
                slid = 0;
            }
            var normalized = flat >= count || sum == 0 ? 0 : Math.Sqrt(square * period / sum);
            var gain = Math.Max(.1, Math.Min(1, scaleFactor * normalized * 5 / period));
            average = gain * price + (1 - gain) * average;
            previousZero = zero;
            older = filter;
            filter = next;
            output[i] = average;
        }
    }


    /// <summary>
    /// Price relative: the ratio of one price field to another, the mean of that ratio over a window, and
    /// the difference between the two fields' returns over a lag. Each value has its own defined flag.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    internal static void PriceRelative(FastColumns columns, double[][] output, PriceRelativeStrength owner)
    {
        var evaluation = Field(columns, owner.Evaluation);
        var basis = Field(columns, owner.Basis);
        var n = columns.Close.Length;
        for (var slot = 0; slot < 6; slot++) output[slot].AsSpan(0, n).Clear();
        var meanPeriod = owner.MeanPeriod ?? 0;
        var lag = owner.Period ?? 0;
        // Bars whose ratio overflows are skipped by the windows, so both windows keep their own rings
        // instead of indexing back into the columns.
        var ratios = new double[meanPeriod];
        var pastEvaluation = new double[lag];
        var pastBasis = new double[lag];
        int ratioSlot = 0, ratioCount = 0, missing = 0, pastSlot = 0, pastCount = 0, slid = 0;
        double sum = 0, largest = 0;
        for (var i = 0; i < n; i++)
        {
            var e = evaluation[i];
            var b = basis[i];
            var has = b != 0;
            var ratio = has ? e / b : double.NaN;
            if (has)
            {
                output[0][i] = ratio;
                output[3][i] = 1;
                if (double.IsInfinity(ratio)) continue;
            }
            if (meanPeriod != 0)
            {
                if (ratioCount == meanPeriod)
                {
                    var old = ratios[ratioSlot];
                    if (double.IsNaN(old)) missing--;
                    else
                    {
                        sum -= old;
                        largest = Math.Max(largest, Math.Abs(old));
                    }
                }
                else ratioCount++;
                ratios[ratioSlot] = ratio;
                ratioSlot = ratioSlot + 1 == meanPeriod ? 0 : ratioSlot + 1;
                if (has)
                {
                    sum += ratio;
                    largest = Math.Max(largest, Math.Abs(ratio));
                }
                else missing++;
                // Rebuilt at intervals, and whenever the sum has fallen far below what passed through it.
                if (++slid > RebuildMask || !(Math.Abs(sum) > largest * (1.0 / 1024)))
                {
                    sum = 0;
                    double carry = 0;
                    for (var j = 0; j < ratioCount; j++)
                    {
                        var term = ratios[j];
                        if (double.IsNaN(term)) continue;
                        var total = sum + term;
                        carry += Math.Abs(sum) >= Math.Abs(term) ? sum - total + term : term - total + sum;
                        sum = total;
                    }
                    sum += carry;
                    largest = Math.Abs(sum);
                    slid = 0;
                }
                if (ratioCount == meanPeriod && missing == 0)
                {
                    output[1][i] = meanPeriod == 1 ? ratio : sum / meanPeriod;
                    output[4][i] = 1;
                }
            }
            if (lag == 0) continue;
            if (pastCount == lag)
            {
                var oldEvaluation = pastEvaluation[pastSlot];
                var oldBasis = pastBasis[pastSlot];
                if (oldEvaluation != 0 && oldBasis != 0)
                {
                    output[2][i] = ReturnGap(e, b, oldEvaluation, oldBasis);
                    output[5][i] = 1;
                }
            }
            else pastCount++;
            pastEvaluation[pastSlot] = e;
            pastBasis[pastSlot] = b;
            pastSlot = pastSlot + 1 == lag ? 0 : pastSlot + 1;
        }
    }

    // e / oldE - b / oldB as one quotient, with the products' rounding carried, so the two returns
    // cancelling does not leave only their rounding behind.
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Hot)]
    private static double ReturnGap(double e, double b, double oldE, double oldB)
    {
        var left = e * oldB;
        var right = b * oldE;
        var divisor = oldE * oldB;
        var plain = (left - right) / divisor;
        if (double.IsInfinity(left) || double.IsInfinity(right) || double.IsInfinity(divisor) || divisor == 0
            || Math.Abs(left) < 1e-270 || Math.Abs(right) < 1e-270)
            return e / oldE - b / oldB;
        var residual = ProductResidual(e, oldB, left) - ProductResidual(b, oldE, right);
        var refined = (left - right + residual) / divisor;
        return double.IsNaN(refined) || double.IsInfinity(refined) ? plain : refined;
    }

    // What a * b lost to rounding, by splitting each factor into two halves whose products are exact.
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Hot)]
    private static double ProductResidual(double a, double b, double product)
    {
        const double Splitter = 134217729;
        var scaledA = a * Splitter;
        var highA = scaledA - (scaledA - a);
        var lowA = a - highA;
        var scaledB = b * Splitter;
        var highB = scaledB - (scaledB - b);
        var lowB = b - highB;
        return lowA * lowB - (product - highA * highB - lowA * highB - highA * lowB);
    }


    /// <summary>
    /// Fractal adaptive average: the mean of the bars so far until a window fills, then an exponential
    /// average whose step comes from the fractal dimension, the ranges of the window's two halves against
    /// the range of the whole window.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    internal static void FractalAdaptive(ReadOnlySpan<double> close, Span<double> output, int period)
    {
        var n = close.Length;
        output = output.Slice(0, n);
        if (n == 0) return;
        var half = period / 2;
        var rest = period - half;
        double sum = 0, average = 0;
        var filling = Math.Min(n, period - 1);
        for (var i = 0; i < filling; i++)
        {
            sum += close[i];
            average = i == 0 ? close[0] : sum / (i + 1);
            output[i] = average;
        }
        if (n < period) return;
        double[] wholeHigh = FastMath.Allocate(n), wholeLow = FastMath.Allocate(n), restHigh = FastMath.Allocate(n),
            restLow = FastMath.Allocate(n), halfHigh = FastMath.Allocate(n), halfLow = FastMath.Allocate(n);
        RollingExtreme(close, wholeHigh, period, true);
        RollingExtreme(close, wholeLow, period, false);
        RollingExtreme(close, restHigh, rest, true);
        RollingExtreme(close, restLow, rest, false);
        RollingExtreme(close, halfHigh, half, true);
        RollingExtreme(close, halfLow, half, false);
        var scale = (double)period / half;
        var inverseLog = 1 / Math.Log(2);
        for (var i = period - 1; i < n; i++)
        {
            var whole = wholeHigh[i] - wholeLow[i];
            var split = halfHigh[i - rest] - halfLow[i - rest] + (restHigh[i] - restLow[i]);
            // Flat halves leave a ratio of one unit against the whole range, or exactly one: a full step.
            double gain = 1;
            if (split != 0)
            {
                var dimension = Math.Log(split * scale / whole) * inverseLog;
                gain = Math.Max(.01, Math.Min(1, Math.Exp(-4.6 * (dimension - 1))));
            }
            average = gain * close[i] + (1 - gain) * average;
            output[i] = average;
        }
    }


    /// <summary>
    /// Pearson correlation of two price columns over the window; a window where either column is flat has
    /// no correlation. The five sums slide, measured from anchor prices so they do not lose digits to the
    /// price level, and are taken afresh on a schedule and whenever a variance has all but cancelled.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    internal static void Correlation(FastColumns columns, Span<double> value, Span<double> defined, WindowCorrelation owner)
    {
        ReadOnlySpan<double> left = Field(columns, owner.Left), right = Field(columns, owner.Right);
        var n = columns.Count;
        var period = owner.Period;
        value = value.Slice(0, n);
        defined = defined.Slice(0, n);
        if (n < period)
        {
            value.Clear();
            defined.Clear();
            return;
        }
        value.Slice(0, period - 1).Clear();
        defined.Slice(0, period - 1).Clear();
        int leftRun = 0, rightRun = 0;
        for (var i = 0; i < period - 1; i++)
        {
            leftRun = i > 0 && left[i] == left[i - 1] ? leftRun + 1 : 1; // NOSONAR an exact tie is the contract
            rightRun = i > 0 && right[i] == right[i - 1] ? rightRun + 1 : 1; // NOSONAR an exact tie is the contract
        }
        var block = Math.Max(64, period);
        // One division for the window instead of three a bar.
        var inverse = 1.0 / period;
        var flat = owner.FlatZero ? 1.0 : 0.0;
        var at = period - 1;
        // Whether the bar a block starts on already has its runs counted: it does when the block before it
        // stopped there to sum the window afresh.
        var counted = false;
        while (at < n)
        {
            // A block starts on a fresh sum of its first window, around that window's newest pair.
            if (!counted)
            {
                leftRun = at > 0 && left[at] == left[at - 1] ? leftRun + 1 : 1; // NOSONAR an exact tie is the contract
                rightRun = at > 0 && right[at] == right[at - 1] ? rightRun + 1 : 1; // NOSONAR an exact tie is the contract
            }
            counted = false;
            var from = at - period + 1;
            var sums = PairSums.Of(left.Slice(from, period), right.Slice(from, period));
            if (leftRun >= period || rightRun >= period)
            {
                value[at] = 0;
                defined[at] = flat;
            }
            else
            {
                double a = sums.Xx - sums.X * sums.X * inverse, b = sums.Yy - sums.Y * sums.Y * inverse;
                // Where a variance is a small remainder even of a fresh sum of squares, the window is summed
                // around its own means instead.
                value[at] = !(a > 1e-4 * sums.Xx) || !(b > 1e-4 * sums.Yy)
                    ? CenteredCorrelation(left.Slice(from, period), right.Slice(from, period))
                    : (sums.Xy - sums.X * sums.Y * inverse) / Math.Sqrt(a * b);
                defined[at] = 1;
            }
            at++;
            // The rest of the block slides those sums, held where the compiler can keep them in registers.
            double anchorX = sums.AnchorX, anchorY = sums.AnchorY, sumX = sums.X, sumY = sums.Y;
            double sumXx = sums.Xx, sumYy = sums.Yy, sumXy = sums.Xy;
            var end = (int)Math.Min((long)at + block - 1, n);
            for (; at < end; at++)
            {
                leftRun = left[at] == left[at - 1] ? leftRun + 1 : 1; // NOSONAR an exact tie is the contract
                rightRun = right[at] == right[at - 1] ? rightRun + 1 : 1; // NOSONAR an exact tie is the contract
                double x = left[at] - anchorX, y = right[at] - anchorY;
                double oldX = left[at - period] - anchorX, oldY = right[at - period] - anchorY;
                sumX += x - oldX;
                sumY += y - oldY;
                sumXx += x * x - oldX * oldX;
                sumYy += y * y - oldY * oldY;
                sumXy += x * y - oldX * oldY;
                if (leftRun >= period || rightRun >= period)
                {
                    value[at] = 0;
                    defined[at] = flat;
                    continue;
                }
                double a = sumXx - sumX * sumX * inverse, b = sumYy - sumY * sumY * inverse;
                // Where a variance is a small remainder of its sum of squares, the block stops and the next
                // one starts here, on a fresh sum.
                if (!(a > 1e-4 * sumXx) || !(b > 1e-4 * sumYy))
                {
                    counted = true;
                    break;
                }
                value[at] = (sumXy - sumX * sumY * inverse) / Math.Sqrt(a * b);
                defined[at] = 1;
            }
        }
    }

    // Sums of two columns over a window, each measured from that column's anchor price.
    private struct PairSums
    {
        internal double AnchorX, AnchorY, X, Y, Xx, Yy, Xy;

        // Sums the window around its newest pair, which becomes the anchor.
        [System.Runtime.CompilerServices.MethodImpl(FastMath.Hot)]
        internal static PairSums Of(ReadOnlySpan<double> left, ReadOnlySpan<double> right)
        {
            var sums = new PairSums { AnchorX = left[left.Length - 1], AnchorY = right[right.Length - 1] };
            for (var j = 0; j < left.Length; j++)
            {
                double x = left[j] - sums.AnchorX, y = right[j] - sums.AnchorY;
                sums.X += x;
                sums.Y += y;
                sums.Xx += x * x;
                sums.Yy += y * y;
                sums.Xy += x * y;
            }
            return sums;
        }
    }

    // The correlation of one window, summed around its own means.
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Hot)]
    private static double CenteredCorrelation(ReadOnlySpan<double> left, ReadOnlySpan<double> right)
    {
        var period = left.Length;
        double leftAnchor = left[period - 1], rightAnchor = right[period - 1], leftSum = 0, rightSum = 0;
        for (var j = 0; j < period; j++)
        {
            leftSum += left[j] - leftAnchor;
            rightSum += right[j] - rightAnchor;
        }
        double leftMean = leftSum / period, rightMean = rightSum / period, a = 0, b = 0, c = 0;
        for (var j = 0; j < period; j++)
        {
            var x = left[j] - leftAnchor - leftMean;
            var y = right[j] - rightAnchor - rightMean;
            a += x * x;
            b += y * y;
            c += x * y;
        }
        return c / Math.Sqrt(a * b);
    }

    /// <summary>
    /// Holt-Winter forecast: a level, a velocity and an acceleration, each corrected by a share of the
    /// last forecast's error, published as the forecast for the next bar.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    internal static void HoltWinter(ReadOnlySpan<double> close, Span<double> output, HoltWinterForecast owner)
    {
        var n = close.Length;
        output = output.Slice(0, n);
        if (n == 0) return;
        if (owner.Period == 1)
        {
            close.CopyTo(output);
            return;
        }
        double a = owner.LevelFactor, b = owner.TrendFactor, c = owner.AccelerationFactor;
        double level = close[0], velocity = 0, acceleration = 0;
        output[0] = level;
        for (var i = 1; i < n; i++)
        {
            var forecast = level + velocity + 0.5 * acceleration;
            var next = forecast + a * (close[i] - forecast);
            var trend = velocity + acceleration;
            var speed = trend + b * (next - level - trend);
            acceleration += c * (speed - velocity - acceleration);
            level = next;
            velocity = speed;
            output[i] = level + velocity + 0.5 * acceleration;
        }
    }
}
