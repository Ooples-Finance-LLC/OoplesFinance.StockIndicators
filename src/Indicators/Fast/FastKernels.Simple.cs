namespace OoplesFinance.StockIndicators.Indicators.Fast;

internal static partial class FastKernels
{
    /// <summary>
    /// Wilder average. Seeded, it publishes the running mean of the first <paramref name="period"/> values
    /// and then smooths; unseeded, it starts from the first value.
    /// </summary>
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
    internal static void CandleArithmetic(FastColumns columns, Span<double> value, Span<double> defined, CandleArithmetic owner)
    {
        ReadOnlySpan<double> left = Field(columns, owner.Left), right = Field(columns, owner.Right);
        var n = left.Length;
        value = value.Slice(0, n);
        defined = defined.Slice(0, n);
        defined.Fill(1);
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
                for (var i = 0; i < n; i++)
                {
                    var divisor = right[i];
                    if (divisor == 0)
                    {
                        value[i] = 0;
                        defined[i] = 0;
                    }
                    else
                        value[i] = left[i] / divisor;
                }
                break;
        }
    }

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

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static bool Replaces(double value, double best, bool highest, bool newestTie) =>
        highest ? (newestTie ? value >= best : value > best) : (newestTie ? value <= best : value < best);

    private static void ExtremeIndexByDeque(ReadOnlySpan<double> input, Span<double> index, long period, bool highest,
        bool newestTie)
    {
        var ring = new int[(int)Math.Min(period, input.Length)];
        int head = 0, count = 0;
        for (var i = 0; i < input.Length; i++)
        {
            var value = input[i];
            // A candidate the new value replaces can never be the extreme again.
            while (count > 0 && Replaces(value, input[ring[(head + count - 1) % ring.Length]], highest, newestTie)) count--;
            if (count > 0 && ring[head] <= i - period)
            {
                head = (head + 1) % ring.Length;
                count--;
            }
            ring[(head + count) % ring.Length] = i;
            count++;
            index[i] = ring[head];
        }
    }

    /// <summary>
    /// Aroon: how recently the window's high and low occurred, as percentages of the period, and their
    /// difference. Nothing is published until the window is full.
    /// </summary>
    internal static void Aroon(FastColumns columns, double[][] outputs, WindowAroon owner)
    {
        ReadOnlySpan<double> high = columns.High, low = columns.Low;
        var n = high.Length;
        var period = owner.Period;
        var size = (long)period + (owner.IncludeExtraBar ? 1 : 0);
        Span<double> up = outputs[0].AsSpan(0, n), down = outputs[1].AsSpan(0, n), oscillator = outputs[2].AsSpan(0, n);
        var first = (int)Math.Min(size - 1, n);
        for (var slot = 3; slot < 6; slot++) DefinedFrom(outputs[slot], first);
        // The two outputs hold the extreme's bar index until the final pass turns it into a percentage.
        RollingExtremeIndex(high, up, size, true, !owner.OldestTie);
        RollingExtremeIndex(low, down, size, false, !owner.OldestTie);
        up.Slice(0, first).Clear();
        down.Slice(0, first).Clear();
        oscillator.Slice(0, first).Clear();
        for (var i = first; i < n; i++)
        {
            long highest = (long)up[i], lowest = (long)down[i];
            up[i] = WindowAroon.Percentage(period - (i - highest), period);
            down[i] = WindowAroon.Percentage(period - (i - lowest), period);
            oscillator[i] = WindowAroon.Percentage(highest - lowest, period);
        }
    }

    /// <summary>Half the sum of the window's lowest and highest value.</summary>
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
    internal static void OnBalanceVolume(FastColumns columns, double[][] outputs, SeededOnBalanceVolume owner)
    {
        ReadOnlySpan<double> close = columns.Close, volume = columns.Volume;
        var n = close.Length;
        Span<double> value = outputs[0].AsSpan(0, n), average = outputs[1].AsSpan(0, n), defined = outputs[2].AsSpan(0, n);
        average.Clear();
        defined.Clear();
        if (n == 0) return;
        var total = owner.Seed == ObvSeed.FirstVolume ? volume[0] : 0;
        value[0] = total;
        for (var i = 1; i < n; i++)
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
#endif
            value[i] = total;
        }
        if (owner.AveragePeriod is not int period || n <= period) return;
        RebuiltMean(value.Slice(1), average.Slice(1), period);
        defined.Slice(period).Fill(1);
    }


    /// <summary>
    /// Four-stage Laguerre filter started from zero: each stage follows the one before it with the same
    /// damping, and the output weights the stages 1, 2, 2, 1.
    /// </summary>
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
}
