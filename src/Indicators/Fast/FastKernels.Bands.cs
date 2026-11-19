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
    internal static void AtrBands(FastColumns columns, double[][] outputs, WindowAtrBands owner)
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
        DefinedFrom(outputs[4], centerFirst);
        DefinedFrom(outputs[5], bandFirst);
        DefinedFrom(outputs[6], bandFirst);
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
}
