namespace OoplesFinance.StockIndicators.Indicators.Fast;

internal static partial class FastKernels
{
    /// <summary>
    /// The catalog exponential average: the running mean until <paramref name="length"/> values have been
    /// seen, published on every bar, then the usual recurrence.
    /// </summary>
    internal static void RunningSeedEma(ReadOnlySpan<double> input, Span<double> output, int length)
    {
        var n = input.Length;
        output = output.Slice(0, n);
        length = Math.Max(1, length);
        var seeded = Math.Min(length, n);
        double sum = 0, value = 0;
        for (var i = 0; i < seeded; i++)
        {
            sum += input[i];
            value = sum / (i + 1);
            output[i] = value;
        }
        var alpha = 2.0 / (length + 1.0);
        var weight = (length - 1.0) / (length + 1.0);
        for (var i = seeded; i < n; i++)
        {
            value = FastMath.Blend(weight, value, alpha * input[i]);
            output[i] = value;
        }
    }

    /// <summary>
    /// The catalog weighted average: newest value heaviest, divided by the full weight total from the first
    /// bar, so the startup values rise toward the average instead of being withheld.
    /// </summary>
    internal static void FullDivisorWma(ReadOnlySpan<double> input, Span<double> output, int length)
    {
        var n = input.Length;
        output = output.Slice(0, n);
        length = Math.Max(1, length);
        var divisor = length * (length + 1.0) / 2;
        double numerator = 0, sum = 0;
        for (var i = 0; i < n; i++)
        {
            if (i >= length && (i & RebuildMask) == 0)
            {
                numerator = sum = 0;
                for (var k = 0; k < length; k++)
                {
                    var value = input[i - k];
                    sum += value;
                    numerator += (length - k) * value;
                }
            }
            else
            {
                numerator += length * input[i] - sum;
                sum += i >= length ? input[i] - input[i - length] : input[i];
            }
            output[i] = numerator / divisor;
        }
    }

    /// <summary>Mean of the high and low, optionally with the close once or twice, or with the open.</summary>
    internal static void PricePoint(FastColumns columns, Span<double> output, int closes, bool open)
    {
        if (closes > 0 && !open) columns.RequireHighLowClose();
        ReadOnlySpan<double> high = columns.High, low = columns.Low;
        output = output.Slice(0, high.Length);
        FastMath.Add(high, low, output);
        var count = 2;
        if (open)
        {
            FastMath.Add(output, columns.Open, output);
            count++;
        }
        for (var i = 0; i < closes; i++)
        {
            FastMath.Add(output, columns.Close, output);
            count++;
        }
        FastMath.Scale(output, 1.0 / count, output);
    }

    /// <summary>True range per bar; the first bar has no previous close and publishes its own range.</summary>
    internal static void TrueRangeSeries(ReadOnlySpan<double> high, ReadOnlySpan<double> low,
        ReadOnlySpan<double> close, Span<double> output)
    {
        var n = close.Length;
        if (n == 0) return;
        output[0] = high[0] - low[0];
        for (var i = 1; i < n; i++) output[i] = TrueRange(high[i], low[i], close[i - 1]);
    }

    /// <summary>
    /// Williams %R over the available high/low window: -100 for a flat window, zero when the close is the high.
    /// </summary>
    internal static void WilliamsPercent(FastColumns columns, Span<double> output, int length)
    {
        columns.RequireHighLowClose();
        ReadOnlySpan<double> close = columns.Close;
        var n = close.Length;
        output = output.Slice(0, n);
        var lowest = FastMath.Allocate(n);
        RollingExtreme(columns.High, output, Math.Max(1, length), true);
        RollingExtreme(columns.Low, lowest, Math.Max(1, length), false);
        for (var i = 0; i < n; i++)
        {
            double upper = output[i], lower = lowest[i];
            output[i] = upper == lower ? -100 : close[i] == upper ? 0 : 100 * (close[i] - upper) / (upper - lower);
        }
    }


    /// <summary>
    /// Accumulation/distribution line: the running total of volume weighted by where the close sits in the
    /// bar's range, with the catalog exponential average of the line as its signal.
    /// </summary>
    internal static void AccumulationLine(FastColumns columns, Span<double> line, Span<double> signal, int length)
    {
        columns.RequireHighLowClose();
        ReadOnlySpan<double> high = columns.High, low = columns.Low, close = columns.Close, volume = columns.Volume;
        var n = close.Length;
        line = line.Slice(0, n);
        double total = 0;
        for (var i = 0; i < n; i++)
        {
            double h = high[i], l = low[i], c = close[i];
            if (h != l) total += (c - l - (h - c)) * volume[i] / (h - l);
            line[i] = total;
        }
        RunningSeedEma(line, signal, length);
    }

    /// <summary>
    /// Balance of power: the bar's close-open move over its range, zero for a bar with no range, with the
    /// catalog exponential average of it as its signal.
    /// </summary>
    internal static void BalanceOfPower(FastColumns columns, Span<double> value, Span<double> signal, int length)
    {
        columns.RequireHighLowClose();
        ReadOnlySpan<double> open = columns.Open, high = columns.High, low = columns.Low, close = columns.Close;
        var n = close.Length;
        value = value.Slice(0, n);
        for (var i = 0; i < n; i++)
        {
            double h = high[i], l = low[i];
            value[i] = h == l ? 0 : (close[i] - open[i]) / (h - l);
        }
        // An average over one bar is the value itself.
        if (length <= 1) value.CopyTo(signal);
        else RunningSeedEma(value, signal, length);
    }
}
