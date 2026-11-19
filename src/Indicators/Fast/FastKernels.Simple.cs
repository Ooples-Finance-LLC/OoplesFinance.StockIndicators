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
}
