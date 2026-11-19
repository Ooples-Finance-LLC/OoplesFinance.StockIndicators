namespace OoplesFinance.StockIndicators.Indicators.Fast;

internal static partial class FastKernels
{
    /// <summary>
    /// Price momentum oscillator: the one-bar percentage change smoothed twice with mean-seeded
    /// averages, the first scaled by ten, and a signal average of the result. A zero close ends the series,
    /// because the change after it has no base.
    /// </summary>
    internal static void PriceMomentum(ReadOnlySpan<double> close, double[][] outputs, SeededPriceMomentum owner)
    {
        var n = close.Length;
        int period = owner.Period, smooth = owner.SmoothPeriod, signalPeriod = owner.SignalPeriod;
        Span<double> momentum = outputs[0].AsSpan(0, n), signal = outputs[1].AsSpan(0, n),
            momentumDefined = outputs[2].AsSpan(0, n), signalDefined = outputs[3].AsSpan(0, n);
        momentum.Clear();
        signal.Clear();
        momentumDefined.Clear();
        signalDefined.Clear();
        long secondStart = (long)period + smooth - 1, signalStart = secondStart + signalPeriod - 1;
        double first = 0, second = 0, line = 0, sum = 0, secondSum = 0, signalSum = 0;
        double firstWeight = period - 2, secondWeight = smooth - 2, signalWeight = signalPeriod - 1;
        for (var i = 1; i < n; i++)
        {
            var previous = close[i - 1];
            if (previous == 0) return;
            var change = (close[i] - previous) * 100 / previous;
            if (i <= period)
            {
                sum += change;
                if (i < period) continue;
                first = sum / period;
            }
            else
                first = (first * firstWeight + 2 * change) / period;
            var scaled = first * 10;
            if (i <= secondStart)
            {
                secondSum += scaled;
                if (i < secondStart) continue;
                second = secondSum / smooth;
            }
            else
                second = (second * secondWeight + 2 * scaled) / smooth;
            momentum[i] = second;
            momentumDefined[i] = 1;
            if (i <= signalStart)
            {
                signalSum += second;
                if (i < signalStart) continue;
                line = signalSum / signalPeriod;
            }
            else
                line = (line * signalWeight + 2 * second) / (signalPeriod + 1.0);
            signal[i] = line;
            signalDefined[i] = 1;
        }
    }

    /// <summary>
    /// Bands a multiple of the window's population deviation above and below a classic average of the close.
    /// </summary>
    internal static void ClassicBands(ReadOnlySpan<double> close, double[][] outputs, ClassicDeviationBands owner)
    {
        var n = close.Length;
        var period = owner.Period;
        Span<double> upper = outputs[0].AsSpan(0, n), middle = outputs[1].AsSpan(0, n), lower = outputs[2].AsSpan(0, n);
        var simple = owner.Method == ClassicAverageMethod.Sma;
        var first = simple ? period - 1L : Math.Max(period - 1L, owner.Average.First);
        for (var slot = 3; slot < 6; slot++) DefinedFrom(outputs[slot], first);
        var deviation = FastMath.Allocate(n);
        Dispersion(close, deviation, new WindowDispersion(period, WindowDispersionOutput.StandardDeviation, false, 1));
        if (simple) Sma(close, middle, period);
        else ClassicAverage(close, middle, owner.Average, owner.Average.First);
        var start = (int)Math.Min(first, n);
        upper.Slice(0, start).Clear();
        middle.Slice(0, start).Clear();
        lower.Slice(0, start).Clear();
        double upperFactor = owner.UpperFactor, lowerFactor = owner.LowerFactor;
        for (var i = start; i < n; i++)
        {
            double center = middle[i], spread = deviation[i];
            upper[i] = center + spread * upperFactor;
            lower[i] = center - spread * lowerFactor;
        }
    }

    /// <summary>
    /// Close minus the close <c>period</c> bars back, as a difference, a fraction or percentage of the old
    /// close, a ratio, or only its rising or falling part. A zero old close publishes zero for the quotients.
    /// </summary>
    internal static void LaggedChange(ReadOnlySpan<double> close, Span<double> output, LaggedPriceChange owner)
    {
        var n = close.Length;
        output = output.Slice(0, n);
        var period = owner.Period;
        var start = Math.Min(period, n);
        output.Slice(0, start).Clear();
        if (start == n) return;
        ReadOnlySpan<double> current = close.Slice(start), previous = close.Slice(0, n - start);
        var tail = output.Slice(start);
        switch (owner.Kind)
        {
            case PriceChangeKind.Difference:
                FastMath.Subtract(current, previous, tail);
                break;
            case PriceChangeKind.Gain:
                for (var i = 0; i < tail.Length; i++) tail[i] = current[i] > previous[i] ? current[i] - previous[i] : 0;
                break;
            case PriceChangeKind.Loss:
                for (var i = 0; i < tail.Length; i++) tail[i] = current[i] < previous[i] ? previous[i] - current[i] : 0;
                break;
            case PriceChangeKind.Ratio:
                for (var i = 0; i < tail.Length; i++) tail[i] = previous[i] == 0 ? 0 : current[i] / previous[i];
                break;
            case PriceChangeKind.RatioPercent:
                for (var i = 0; i < tail.Length; i++) tail[i] = previous[i] == 0 ? 0 : current[i] * 100 / previous[i];
                break;
            case PriceChangeKind.Percent:
                for (var i = 0; i < tail.Length; i++)
                    tail[i] = previous[i] == 0 ? 0 : (current[i] - previous[i]) * 100 / previous[i];
                break;
            default:
                for (var i = 0; i < tail.Length; i++)
                    tail[i] = previous[i] == 0 ? 0 : (current[i] - previous[i]) / previous[i];
                break;
        }
    }

    /// <summary>
    /// Acceleration bands: window means of the high widened, and the low narrowed, by four times the bar's
    /// range over its high-low sum, around the window mean of the close.
    /// </summary>
    internal static void AccelerationBands(FastColumns columns, double[][] outputs, RangeAccelerationBands owner)
    {
        columns.RequireHighLowClose();
        ReadOnlySpan<double> high = columns.High, low = columns.Low;
        var n = high.Length;
        var period = owner.Period;
        double[] raised = FastMath.Allocate(n), lowered = FastMath.Allocate(n);
        for (var i = 0; i < n; i++)
        {
            double h = high[i], l = low[i], sum = h + l;
            if (sum == 0)
            {
                raised[i] = h;
                lowered[i] = l;
                continue;
            }
            var spread = 4 * (h - l);
            raised[i] = h * (sum + spread) / sum;
            lowered[i] = l * (sum - spread) / sum;
        }
        RebuiltMean(raised, outputs[0].AsSpan(0, n), period);
        RebuiltMean(columns.Close, outputs[1].AsSpan(0, n), period);
        RebuiltMean(lowered, outputs[2].AsSpan(0, n), period);
        for (var slot = 3; slot < 6; slot++) DefinedFrom(outputs[slot], period - 1L);
    }

    // Rolling mean whose sum is rebuilt from the window at intervals, so it cannot drift on a long series.
    private static void RebuiltMean(ReadOnlySpan<double> input, Span<double> output, int period)
    {
        var n = input.Length;
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
            output[i] = i < period - 1 ? 0 : sum / period;
        }
    }

    /// <summary>
    /// True strength index: the one-bar change and its magnitude, each smoothed twice with mean-seeded
    /// exponential averages, as a percentage ratio, with a mean-seeded signal average. A zero denominator
    /// publishes nothing and ends the signal.
    /// </summary>
    internal static void TrueStrength(ReadOnlySpan<double> close, double[][] outputs, SeededTrueStrength owner)
    {
        var n = close.Length;
        int period = owner.Period, smooth = owner.SmoothPeriod, signalPeriod = owner.SignalPeriod;
        Span<double> strength = outputs[0].AsSpan(0, n), signal = outputs[1].AsSpan(0, n),
            strengthDefined = outputs[2].AsSpan(0, n), signalDefined = outputs[3].AsSpan(0, n);
        strength.Clear();
        signal.Clear();
        strengthDefined.Clear();
        signalDefined.Clear();
        long secondStart = (long)period + smooth - 1, seedIndex = secondStart + signalPeriod - 1;
        double signed = 0, absolute = 0, secondSigned = 0, secondAbsolute = 0, sumSigned = 0, sumAbsolute = 0;
        double seedSignal = 0, line = 0;
        bool signalMissing = false, hasLine = false;
        for (var i = 1; i < n; i++)
        {
            var change = close[i] - close[i - 1];
            var magnitude = Math.Abs(change);
            if (i <= period)
            {
                sumSigned += change;
                sumAbsolute += magnitude;
                if (i == period)
                {
                    // The first stage's seed is also the first term of the second stage's seed.
                    sumSigned = signed = sumSigned / period;
                    sumAbsolute = absolute = sumAbsolute / period;
                }
                continue;
            }
            signed = (signed * (period - 1) + 2 * change) / (period + 1.0);
            absolute = (absolute * (period - 1) + 2 * magnitude) / (period + 1.0);
            if (i <= secondStart)
            {
                sumSigned += signed;
                sumAbsolute += absolute;
                if (i < secondStart) continue;
                secondSigned = sumSigned / smooth;
                secondAbsolute = sumAbsolute / smooth;
            }
            else
            {
                secondSigned = (secondSigned * (smooth - 1) + 2 * signed) / (smooth + 1.0);
                secondAbsolute = (secondAbsolute * (smooth - 1) + 2 * absolute) / (smooth + 1.0);
            }
            var present = secondAbsolute != 0;
            var value = present ? 100 * secondSigned / secondAbsolute : 0;
            if (present)
            {
                strength[i] = value;
                strengthDefined[i] = 1;
            }
            if (i == secondStart)
            {
                signalMissing = !present;
                seedSignal = value;
                continue;
            }
            if (signalPeriod <= 1) continue;
            if (i <= seedIndex)
            {
                if (present) seedSignal += value;
                else signalMissing = true;
                if (i == seedIndex && !signalMissing)
                {
                    line = seedSignal / signalPeriod;
                    hasLine = true;
                }
            }
            else if (hasLine && present)
                line = (line * (signalPeriod - 1) + 2 * value) / (signalPeriod + 1.0);
            else
                hasLine = false;
            if (!hasLine) continue;
            signal[i] = line;
            signalDefined[i] = 1;
        }
    }

    /// <summary>
    /// Force index: the one-bar change times the volume, as a mean-seeded exponential average.
    /// </summary>
    internal static void ForceIndex(FastColumns columns, Span<double> value, Span<double> defined, int period)
    {
        ReadOnlySpan<double> close = columns.Close, volume = columns.Volume;
        var n = close.Length;
        value = value.Slice(0, n);
        DefinedFrom(defined.Slice(0, n), period);
        var seeded = (int)Math.Min((long)period + 1, n);
        value.Slice(0, Math.Min(period, n)).Clear();
        double sum = 0;
        for (var i = 1; i < seeded; i++) sum += (close[i] - close[i - 1]) * volume[i];
        if (n <= period) return;
        var force = sum / period;
        value[period] = force;
        var alpha = 2.0 / (period + 1.0);
        var weight = (period - 1.0) / (period + 1.0);
        for (var i = period + 1; i < n; i++)
        {
            force = FastMath.Blend(weight, force, alpha * ((close[i] - close[i - 1]) * volume[i]));
            value[i] = force;
        }
    }

    /// <summary>
    /// Deviation bands around the window mean with the close's position between them, its z-score and the
    /// band width over the mean. A flat window has no position or z-score, and a zero mean has no width.
    /// </summary>
    internal static void DeviationBands(ReadOnlySpan<double> close, double[][] outputs, WindowDeviationBands owner)
    {
        var n = close.Length;
        var period = owner.Period;
        var factor = owner.Factor;
        var widthScale = owner.WidthAsPercent ? 200.0 : 2.0;
        for (var slot = 0; slot < 12; slot++) outputs[slot].AsSpan().Clear();
        if (n < period) return;
        Span<double> middle = outputs[0].AsSpan(0, n), upper = outputs[1].AsSpan(0, n), lower = outputs[2].AsSpan(0, n),
            position = outputs[3].AsSpan(0, n), score = outputs[4].AsSpan(0, n), width = outputs[5].AsSpan(0, n),
            positionDefined = outputs[9].AsSpan(0, n), scoreDefined = outputs[10].AsSpan(0, n),
            widthDefined = outputs[11].AsSpan(0, n);
        var deviation = FastMath.Allocate(n);
        Dispersion(close, deviation, new WindowDispersion(period, WindowDispersionOutput.StandardDeviation, false, 1));
        Dispersion(close, score, new WindowDispersion(period, WindowDispersionOutput.ZScore, false, 1));
        RebuiltMean(close, middle, period);
        var first = period - 1;
        score.Slice(0, first).Clear();
        for (var slot = 6; slot < 9; slot++) outputs[slot].AsSpan(first, n - first).Fill(1);
        for (var i = first; i < n; i++)
        {
            double center = middle[i], spread = deviation[i], half = factor * spread;
            upper[i] = center + half;
            lower[i] = center - half;
            if (half != 0)
            {
                position[i] = (close[i] - center + half) / (2 * half);
                positionDefined[i] = 1;
            }
            if (spread != 0) scoreDefined[i] = 1;
            else score[i] = 0;
            if (center == 0) continue;
            width[i] = widthScale * half / center;
            widthDefined[i] = 1;
        }
    }


    /// <summary>
    /// KDJ: the close's position in the high/low window as a percentage (zero for a flat window), K as a
    /// simple or Wilder average of that, D as the same average of K, and J as a weighted difference of the two.
    /// </summary>
    internal static void StochasticKdj(FastColumns columns, double[][] outputs, WindowStochasticKdj owner)
    {
        columns.RequireHighLowClose();
        ReadOnlySpan<double> high = columns.High, low = columns.Low, close = columns.Close;
        var n = close.Length;
        for (var slot = 0; slot < 6; slot++) outputs[slot].AsSpan(0, n).Clear();
        var offset = owner.Period - 1;
        if (offset >= n) return;
        var highest = FastMath.Allocate(n);
        var lowest = FastMath.Allocate(n);
        RollingExtreme(high, highest, owner.Period, true);
        RollingExtreme(low, lowest, owner.Period, false);
        // The raw position reuses the highest buffer.
        var raw = highest.AsSpan(offset, n - offset);
        for (var i = offset; i < n; i++)
        {
            var top = highest[i];
            var bottom = lowest[i];
            raw[i - offset] = top == bottom ? 0 : 100 * (close[i] - bottom) / (top - bottom);
        }
        var wilder = owner.WilderSmoothing;
        var k = outputs[0].AsSpan(offset, n - offset);
        var kFirst = KdjAverage(raw, k, owner.KPeriod, wilder);
        if (kFirst >= k.Length) return;
        outputs[3].AsSpan(offset + kFirst, n - offset - kFirst).Fill(1);
        var kStart = offset + kFirst;
        var d = outputs[1].AsSpan(kStart, n - kStart);
        var dFirst = KdjAverage(outputs[0].AsSpan(kStart, n - kStart), d, owner.DPeriod, wilder);
        var start = kStart + dFirst;
        if (start >= n) return;
        outputs[4].AsSpan(start, n - start).Fill(1);
        outputs[5].AsSpan(start, n - start).Fill(1);
        double kFactor = owner.KFactor, dFactor = owner.DFactor;
        Span<double> kAll = outputs[0], dAll = outputs[1], j = outputs[2];
        for (var i = start; i < n; i++) j[i] = kAll[i] * kFactor - dAll[i] * dFactor;
    }

    // Simple average defined once the window fills, or Wilder's average seeded with the first value.
    // Returns the index of the first defined value; earlier outputs are zero.
    private static int KdjAverage(ReadOnlySpan<double> input, Span<double> output, int period, bool wilder)
    {
        if (period == 1)
        {
            input.CopyTo(output);
            return 0;
        }
        if (!wilder)
        {
            RebuiltMean(input, output, period);
            return period - 1;
        }
        if (input.Length == 0) return 0;
        var last = input[0];
        output[0] = last;
        double weight = period - 1, inverse = period;
        for (var i = 1; i < input.Length; i++)
        {
            last = (last * weight + input[i]) / inverse;
            output[i] = last;
        }
        return 0;
    }


    /// <summary>
    /// Least-squares line through the window (the bars so far while it fills): slope, intercept, the
    /// window's standard deviation, r-squared and the line's value at the newest bar. Each window is
    /// measured from its own newest price, so nothing drifts and a flat window is exactly flat.
    /// </summary>
    internal static void RegressionStatistics(ReadOnlySpan<double> close, double[][] outputs, int period)
    {
        var n = close.Length;
        for (var slot = 0; slot < 10; slot++) outputs[slot].AsSpan(0, n).Clear();
        outputs[5].AsSpan(0, n).Fill(1);
        if (n < 2) return;
        for (var slot = 6; slot < 10; slot++)
            if (slot != 8) outputs[slot].AsSpan(1, n - 1).Fill(1);
        Span<double> slopes = outputs[0], intercepts = outputs[1], deviations = outputs[2], fit = outputs[3],
            line = outputs[4], fitDefined = outputs[8];
        for (var i = 1; i < n; i++)
        {
            var count = Math.Min(period, i + 1);
            var window = close.Slice(i - count + 1, count);
            var anchor = window[count - 1];
            double sum = 0;
            for (var j = 0; j < count; j++) sum += window[j] - anchor;
            var mean = sum / count;
            var center = (count + 1) * .5;
            // The sum of squared centred positions 1..count.
            var xx = count * ((double)count * count - 1) / 12;
            double yy = 0, xy = 0;
            for (var j = 0; j < count; j++)
            {
                var y = window[j] - anchor - mean;
                yy += y * y;
                xy += (j + 1 - center) * y;
            }
            var slope = xy / xx;
            var level = anchor + mean;
            slopes[i] = slope;
            intercepts[i] = level - slope * center;
            deviations[i] = Math.Sqrt(yy / count);
            line[i] = level + slope * (count - center);
            if (yy == 0) continue;
            fit[i] = xy / xx * (xy / yy);
            fitDefined[i] = 1;
        }
    }
}
