namespace OoplesFinance.StockIndicators.Indicators.Fast;

internal static partial class FastKernels
{
    /// <summary>
    /// Arnaud Legoux average: a Gaussian-weighted mean of the window whose peak sits at
    /// <c>offset</c> of the way through it. Before the window is full it is either withheld or computed
    /// over the bars so far with weights for that many bars.
    /// </summary>
    internal static void ArnaudLegoux(ReadOnlySpan<double> close, Span<double> value, Span<double> defined,
        ArnaudLegouxWindow owner)
    {
        var n = close.Length;
        var period = owner.Period;
        value = value.Slice(0, n);
        var first = Math.Min(owner.FullWindowOnly ? period - 1 : 0, n);
        DefinedFrom(defined.Slice(0, n), first);
        value.Slice(0, first).Clear();
        var weights = new double[period];
        var filling = Math.Min(period - 1, n);
        for (var i = first; i < filling; i++)
        {
            var total = LegouxWeights(weights.AsSpan(0, i + 1), owner.Offset, owner.Sigma);
            value[i] = Dot(close.Slice(0, i + 1), weights) / total;
        }
        if (n < period) return;
        var sum = LegouxWeights(weights, owner.Offset, owner.Sigma);
        for (var i = period - 1; i < n; i++) value[i] = Dot(close.Slice(i - period + 1, period), weights) / sum;
    }

    // Weights relative to the one nearest the centre, which is exactly one; returns their total.
    private static double LegouxWeights(Span<double> weights, double offset, double sigma)
    {
        var n = weights.Length;
        var center = offset * (n - 1);
        var peak = center <= 0 ? 0 : center >= n - 1 ? n - 1 : (int)Math.Floor(center + 0.5);
        var scale = sigma * sigma / (2.0 * n * n);
        double total = 0;
        for (var i = 0; i < n; i++)
        {
            // (i - centre)^2 - (peak - centre)^2, factored so the nearest weight cannot round away from one.
            var exponent = (i - peak) * (i + peak - 2 * center) * scale;
            var weight = exponent >= 746 ? 0 : Math.Exp(-exponent);
            weights[i] = weight;
            total += weight;
        }
        return total;
    }

    private static double Dot(ReadOnlySpan<double> values, ReadOnlySpan<double> weights)
    {
        weights = weights.Slice(0, values.Length);
        double a = 0, b = 0, c = 0, d = 0;
        var i = 0;
        for (; i <= values.Length - 4; i += 4)
        {
            a += values[i] * weights[i];
            b += values[i + 1] * weights[i + 1];
            c += values[i + 2] * weights[i + 2];
            d += values[i + 3] * weights[i + 3];
        }
        for (; i < values.Length; i++) a += values[i] * weights[i];
        return a + b + (c + d);
    }

    /// <summary>
    /// Volatility of the window's log returns: their sample deviation, or the root of their mean square
    /// for the realized form, optionally annualized by 252. A return across a zero or a sign change is
    /// missing, and a window that holds one publishes nothing.
    /// </summary>
    internal static void LogVolatility(ReadOnlySpan<double> close, Span<double> value, Span<double> defined,
        WindowLogVolatility owner)
    {
        var n = close.Length;
        var period = owner.Period;
        var realized = owner.Realized;
        var annual = owner.Annualized ? 252.0 : 1.0;
        value = value.Slice(0, n);
        defined = defined.Slice(0, n);
        value.Clear();
        defined.Fill(1);
        var denominator = realized ? period : (double)period * (period - 1);
        // The last period returns; NaN marks a missing one.
        var ring = new double[period];
        int count = 0, slot = 0, missing = 0, pushed = 0;
        double sum = 0, squares = 0, previous = 0;
        var poisoned = false;
        for (var i = 0; i < n; i++)
        {
            var price = close[i];
            var prior = previous;
            previous = price;
            if (prior != 0)
            {
                var next = price != 0 && price > 0 == prior > 0 ? Math.Log(price / prior) : double.NaN;
                if (count == period)
                {
                    var old = ring[slot];
                    if (double.IsNaN(old))
                        missing--;
                    else
                    {
                        sum -= old;
                        squares -= old * old;
                    }
                }
                else
                    count++;
                ring[slot] = next;
                slot = slot + 1 == period ? 0 : slot + 1;
                if (double.IsNaN(next))
                {
                    missing++;
                    poisoned = true;
                }
                else
                {
                    sum += next;
                    squares += next * next;
                }
                if ((++pushed & RebuildMask) == 0)
                {
                    sum = squares = 0;
                    for (var j = 0; j < count; j++)
                    {
                        if (double.IsNaN(ring[j])) continue;
                        sum += ring[j];
                        squares += ring[j] * ring[j];
                    }
                }
            }
            else if (realized)
                continue;
            if (count < period) continue;
            if (missing > 0 || realized && poisoned)
            {
                defined[i] = 0;
                continue;
            }
            var numerator = (realized ? squares : period * squares - sum * sum) * annual;
            value[i] = numerator > 0 ? Math.Sqrt(numerator / denominator) : 0;
        }
    }

    /// <summary>
    /// A centre line with bands one multiple of the average true range above and below it, and the band
    /// width as a fraction of the centre.
    /// </summary>
    internal static void AtrBands(FastColumns columns, FastOutputs outputs, WindowAtrBands owner)
    {
        columns.RequireHighLowClose();
        ReadOnlySpan<double> close = columns.Close;
        var n = close.Length;
        int centerPeriod = owner.CenterPeriod, atrPeriod = owner.AtrPeriod;
        var mode = owner.CenterMode;
        var multiplier = owner.Multiplier;
        var includeWidth = owner.IncludeWidth;
        Span<double> center = outputs[0].AsSpan(0, n), upper = outputs[1].AsSpan(0, n), lower = outputs[2].AsSpan(0, n),
            width = outputs[3].AsSpan(0, n), widthDefined = outputs[7].AsSpan(0, n);
        var range = FastMath.Allocate(n);
        SeededAverageTrueRange(columns.High, columns.Low, close, range, atrPeriod, false);
        var centerFirst = mode == AtrBandCenterMode.Simple ? centerPeriod - 1L : Math.Max(centerPeriod, atrPeriod) - 1L;
        var bandFirst = Math.Max(centerFirst, atrPeriod);
        outputs.Flag(4, centerFirst);
        outputs.Flag(5, bandFirst);
        outputs.Flag(6, bandFirst);
        upper.Clear();
        lower.Clear();
        width.Clear();
        widthDefined.Clear();
        var alpha = 2.0 / (centerPeriod + 1.0);
        var weight = (centerPeriod - 1.0) / (centerPeriod + 1.0);
        double sum = 0, line = 0;
        for (var i = 0; i < n; i++)
        {
            var price = close[i];
            if (mode == AtrBandCenterMode.Simple)
            {
                sum += i >= centerPeriod ? price - close[i - centerPeriod] : price;
                if (i >= centerPeriod && (i & RebuildMask) == 0)
                {
                    sum = 0;
                    for (var j = i - centerPeriod + 1; j <= i; j++) sum += close[j];
                }
                line = sum / centerPeriod;
            }
            else if (mode == AtrBandCenterMode.MeanSeededExponential && i < centerPeriod)
            {
                sum += price;
                line = sum / centerPeriod;
            }
            else if (mode == AtrBandCenterMode.FirstSeededExponential && i == 0)
                line = price;
            else
                line = FastMath.Blend(weight, line, alpha * price);
            if (i < centerFirst)
            {
                center[i] = 0;
                continue;
            }
            center[i] = line;
            if (i < bandFirst) continue;
            var offset = range[i] * multiplier;
            double high = line + offset, low = line - offset;
            upper[i] = high;
            lower[i] = low;
            if (!includeWidth || line == 0) continue;
            width[i] = (high - low) / line;
            widthDefined[i] = 1;
        }
    }

    /// <summary>
    /// Trailing stop one multiple of the average true range from the close, the high/low or the midpoint.
    /// Each band only tightens until the previous close crosses it, and the stop is the lower band while the
    /// close holds above it and the upper band otherwise.
    /// </summary>
    internal static void AtrTrailingStop(FastColumns columns, double[][] outputs, SeededAtrTrailingStop owner)
    {
        columns.RequireHighLowClose();
        ReadOnlySpan<double> high = columns.High, low = columns.Low, close = columns.Close;
        var n = close.Length;
        var period = owner.Period;
        var basis = owner.Basis;
        var multiplier = owner.Multiplier;
        for (var slot = 0; slot < 6; slot++) outputs[slot].AsSpan().Clear();
        if (n <= period) return;
        var range = FastMath.Allocate(n);
        SeededAverageTrueRange(high, low, close, range, period, false);
        Span<double> stop = outputs[0], upperOut = outputs[1], lowerOut = outputs[2], upperFlag = outputs[4],
            lowerFlag = outputs[5];
        outputs[3].AsSpan(period, n - period).Fill(1);
        double upperBand = 0, lowerBand = 0;
        var bullish = false;
        for (var i = period; i < n; i++)
        {
            double price = close[i], previous = close[i - 1];
            var midpoint = (high[i] + low[i]) * 0.5;
            var upperBasis = basis == AtrTrailBasis.Close ? price : basis == AtrTrailBasis.HighLow ? high[i] : midpoint;
            var lowerBasis = basis == AtrTrailBasis.Close ? price : basis == AtrTrailBasis.HighLow ? low[i] : midpoint;
            var width = range[i] * multiplier;
            double upper = upperBasis + width, lower = lowerBasis - width;
            if (i == period)
            {
                bullish = price >= (basis == AtrTrailBasis.Midpoint ? midpoint : previous);
                upperBand = upper;
                lowerBand = lower;
            }
            if (upper < upperBand || previous > upperBand) upperBand = upper;
            if (lower > lowerBand || previous < lowerBand) lowerBand = lower;
            bullish = price > (bullish ? lowerBand : upperBand);
            if (bullish)
            {
                stop[i] = lowerOut[i] = lowerBand;
                lowerFlag[i] = 1;
            }
            else
            {
                stop[i] = upperOut[i] = upperBand;
                upperFlag[i] = 1;
            }
        }
    }

    /// <summary>
    /// Mean of the window weighted by lag, newest first, over the bars so far while the window fills.
    /// Weights that sum to exactly zero fall back to the plain count as the divisor.
    /// </summary>
    internal static void LagWeightedWindow(ReadOnlySpan<double> input, Span<double> output, ReadOnlySpan<double> lagWeights)
    {
        var n = input.Length;
        var period = lagWeights.Length;
        output = output.Slice(0, n);
        // Oldest first, so a window and its weights run the same way.
        var weights = new double[period];
        for (var j = 0; j < period; j++) weights[j] = lagWeights[period - 1 - j];
        double total = 0;
        var filling = Math.Min(period - 1, n);
        for (var i = 0; i < filling; i++)
        {
            total += lagWeights[i];
            var sum = Dot(input.Slice(0, i + 1), weights.AsSpan(period - 1 - i));
            output[i] = total == 0 ? sum / (i + 1) : sum / total;
        }
        if (n < period) return;
        total += lagWeights[period - 1];
        var divisor = total == 0 ? period : total;
        for (var i = period - 1; i < n; i++) output[i] = Dot(input.Slice(i - period + 1, period), weights) / divisor;
    }

    internal static double[] SineWeights(int period)
    {
        var weights = new double[period];
        for (var lag = 0; lag < period; lag++) weights[lag] = Math.Sin((lag + 1d) * Math.PI / (period + 1d));
        return weights;
    }

    internal static double[] GaussianWeights(int period, double sigma)
    {
        var weights = new double[period];
        var center = period / 2;
        for (var lag = 0; lag < period; lag++)
        {
            var x = (lag - center) / (double)center / sigma;
            weights[lag] = Math.Exp(-.5 * x * x);
        }
        return weights;
    }

    /// <summary>Mean absolute distance of the window's values from their mean, over the bars so far at first.</summary>
    internal static void MeanAbsoluteDeviation(ReadOnlySpan<double> input, Span<double> output, int period)
    {
        var n = input.Length;
        output = output.Slice(0, n);
        double sum = 0;
        for (var i = 0; i < n; i++)
        {
            var start = Math.Max(0, i - period + 1);
            sum = WindowSum(input, sum, i, period);
            var count = i - start + 1;
            var mean = sum / count;
            double a = 0, b = 0;
            var j = start;
            for (; j < i; j += 2)
            {
                a += Math.Abs(input[j] - mean);
                b += Math.Abs(input[j + 1] - mean);
            }
            if (j == i) a += Math.Abs(input[j] - mean);
            output[i] = (a + b) / count;
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static double WindowSum(ReadOnlySpan<double> input, double sum, int i, int period)
    {
        if (i < period) return sum + input[i];
        // A window of one is the value itself; sliding would leave the rounding of the bars before it.
        if (period == 1) return input[i];
        if ((i & RebuildMask) != 0) return sum + (input[i] - input[i - period]);
        sum = 0;
        for (var j = i - period + 1; j <= i; j++) sum += input[j];
        return sum;
    }

    /// <summary>
    /// The window mean with its mean absolute error, mean squared error and mean signed relative error.
    /// A zero in the window leaves the relative error undefined.
    /// </summary>
    internal static void MeanDiagnostics(ReadOnlySpan<double> input, FastOutputs outputs, int period)
    {
        var n = input.Length;
        Span<double> means = outputs[0].AsSpan(0, n), absolutes = outputs[1].AsSpan(0, n), squares = outputs[2].AsSpan(0, n),
            relatives = outputs[3].AsSpan(0, n), relativeDefined = outputs[7].AsSpan(0, n);
        for (var slot = 4; slot < 7; slot++) outputs.Flag(slot, period - 1L);
        relatives.Clear();
        relativeDefined.Clear();
        double sum = 0;
        // Zeros among the last period values.
        var zeros = 0;
        for (var i = 0; i < n; i++)
        {
            sum = WindowSum(input, sum, i, period);
            if (input[i] == 0) zeros++;
            if (i >= period && input[i - period] == 0) zeros--;
            if (i < period - 1)
            {
                means[i] = absolutes[i] = squares[i] = 0;
                continue;
            }
            var mean = sum / period;
            double absolute = 0, squared = 0, relative = 0;
            for (var j = i - period + 1; j <= i; j++)
            {
                var value = input[j];
                var distance = Math.Abs(value - mean);
                absolute += distance;
                squared += distance * distance;
                relative += distance / value;
            }
            means[i] = mean;
            absolutes[i] = absolute / period;
            squares[i] = squared / period;
            if (zeros != 0) continue;
            relatives[i] = relative / period;
            relativeDefined[i] = 1;
        }
    }
}
