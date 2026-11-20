using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators.Fast;

internal static partial class FastKernels
{
    // Exponential average seeded with the mean of its first period inputs; not ready until then.
    private struct MeanSeededAverage(int period)
    {
        private int _count;
        private double _sum;

        internal double Value;

        [System.Runtime.CompilerServices.MethodImpl(FastMath.Hot)]
        internal bool Add(double input)
        {
            if (_count < period)
            {
                _sum += input;
                if (++_count < period) return false;
                Value = _sum / period;
                return true;
            }
            Value = (Value * (period - 1) + 2 * input) / (period + 1.0);
            return true;
        }
    }

    /// <summary>
    /// Klinger volume oscillator: the difference of two mean-seeded averages of a volume force whose sign
    /// follows the high+low+close sum and whose size follows the bar's range against the range accumulated
    /// since the trend last turned, with a signal average.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    internal static void KlingerVolume(FastColumns columns, double[][] outputs, KlingerVolumeWithSignal owner)
    {
        columns.RequireHighLowClose();
        ReadOnlySpan<double> high = columns.High, low = columns.Low, close = columns.Close, volume = columns.Volume;
        var n = close.Length;
        Span<double> oscillator = outputs[0].AsSpan(0, n), signalOut = outputs[1].AsSpan(0, n),
            oscillatorDefined = outputs[2].AsSpan(0, n), signalDefined = outputs[3].AsSpan(0, n);
        oscillator.Clear();
        signalOut.Clear();
        oscillatorDefined.Clear();
        signalDefined.Clear();
        MeanSeededAverage fast = new(owner.FastPeriod), slow = new(owner.SlowPeriod), signal = new(owner.SignalPeriod);
        double previousRange = 0, cumulative = 0, force = 0;
        var previousDirection = 0;
        for (var i = 0; i < n; i++)
        {
            double h = high[i], l = low[i];
            var direction = i == 0
                ? BasisRises(h, l, close[i], 0, 0, 0)
                : BasisRises(h, l, close[i], high[i - 1], low[i - 1], close[i - 1]);
            var range = h - l;
            if (i >= 2)
            {
                // The range accumulated before this bar; the force is its share of the new total.
                var before = direction != previousDirection ? previousRange : cumulative;
                cumulative = before + range;
                var size = volume[i];
                if (before == 0 || size == 0) force = 0;
                else if (range == 0) force = size * (200 * direction);
                else if (cumulative != 0) force = Math.Abs(before) * size * (200 * direction) / Math.Abs(cumulative);
                var fastReady = fast.Add(force);
                if (slow.Add(force) && fastReady)
                {
                    var value = fast.Value - slow.Value;
                    oscillator[i] = value;
                    oscillatorDefined[i] = 1;
                    if (signal.Add(value))
                    {
                        signalOut[i] = signal.Value;
                        signalDefined[i] = 1;
                    }
                }
            }
            previousRange = range;
            previousDirection = direction;
        }
    }

    // 1 when this bar's high+low+close exceeds the previous bar's, otherwise -1. The sign decides a whole
    // bar's volume, so a difference too small for the rounded sums to settle is taken exactly.
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Hot)]
    private static int BasisRises(double high, double low, double close, double previousHigh, double previousLow,
        double previousClose)
    {
        var difference = high + low + close - (previousHigh + previousLow + previousClose);
        var magnitude = Math.Abs(high) + Math.Abs(low) + Math.Abs(close) + Math.Abs(previousHigh) + Math.Abs(previousLow)
            + Math.Abs(previousClose);
        if (Math.Abs(difference) > 4e-15 * magnitude) return difference > 0 ? 1 : -1;
        var exact = JurikDyadic.FromDouble(high);
        exact.Add(low);
        exact.Add(close);
        exact.Add(-previousHigh);
        exact.Add(-previousLow);
        exact.Add(-previousClose);
        return exact.Sign > 0 ? 1 : -1;
    }


    /// <summary>
    /// Ulcer index: the root mean square of each window value's percentage drop from the highest value
    /// before it in the window. Published once the window is full and its oldest value is positive.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    internal static void UlcerIndex(ReadOnlySpan<double> close, Span<double> value, Span<double> defined, int period)
    {
        var n = close.Length;
        value = value.Slice(0, n);
        defined = defined.Slice(0, n);
        value.Clear();
        defined.Clear();
        for (var i = period - 1; i < n; i++)
        {
            var window = close.Slice(i - period + 1, period);
            if (!(window[0] > 0)) continue;
            double peak = 0, squares = 0;
            for (var j = 0; j < window.Length; j++)
            {
                var price = window[j];
                if (price >= peak)
                {
                    peak = price;
                    continue;
                }
                var drop = (price - peak) * 100 / peak;
                squares += drop * drop;
            }
            value[i] = Math.Sqrt(squares / period);
            defined[i] = 1;
        }
    }

    /// <summary>
    /// Stochastic of the relative strength index: where the index sits in its own recent range, zero when
    /// that range is flat, with a classic average of it as the signal. Both wait for the signal.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    internal static void StochasticStrength(ReadOnlySpan<double> close, double[][] outputs, ClassicStochasticRsi owner)
    {
        var n = close.Length;
        for (var slot = 0; slot < 4; slot++) outputs[slot].AsSpan().Clear();
        double[] strength = FastMath.Allocate(n), present = FastMath.Allocate(n);
        WilderStrength(close, strength, present, owner.Rsi.Period, owner.Rsi.Convention, owner.Rsi.UnstablePeriods);
        // Only the bars that have an index feed the range, so they are packed together first.
        var bars = new int[n];
        var count = 0;
        for (var i = 0; i < n; i++)
        {
            if (present[i] == 0) continue;
            strength[count] = strength[i];
            bars[count++] = i;
        }
        var period = owner.StochasticPeriod;
        var length = count - (period - 1);
        var first = owner.SignalAverage.First;
        if (length <= first) return;
        var packed = strength.AsSpan(0, count);
        double[] highest = FastMath.Allocate(count), lowest = FastMath.Allocate(count);
        RollingExtreme(packed, highest, period, true);
        RollingExtreme(packed, lowest, period, false);
        double[] raw = FastMath.Allocate(length), average = FastMath.Allocate(length);
        for (var j = 0; j < length; j++)
        {
            var at = j + period - 1;
            double high = highest[at], low = lowest[at];
            raw[j] = high == low ? 0 : 100 * (packed[at] - low) / (high - low); // NOSONAR an exact tie is the contract
        }
        ClassicAverage(raw, average, owner.SignalAverage, first);
        Span<double> k = outputs[0], d = outputs[1], kDefined = outputs[2], dDefined = outputs[3];
        for (var j = (int)first; j < length; j++)
        {
            var bar = bars[j + period - 1];
            k[bar] = raw[j];
            d[bar] = average[j];
            kDefined[bar] = dDefined[bar] = 1;
        }
    }


    /// <summary>
    /// Ultimate oscillator: buying pressure over true range summed across three windows, weighted 4, 2, 1.
    /// A window with no range at all either withholds the value or contributes nothing, as configured.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    internal static void UltimateOscillator(FastColumns columns, Span<double> value, Span<double> defined,
        WindowUltimateOscillator owner)
    {
        columns.RequireHighLowClose();
        ReadOnlySpan<double> high = columns.High, low = columns.Low, close = columns.Close;
        var n = close.Length;
        value = value.Slice(0, n);
        defined = defined.Slice(0, n);
        value.Clear();
        defined.Clear();
        var zero = owner.ZeroRangeContribution;
        int shortPeriod = owner.ShortPeriod, middlePeriod = owner.MiddlePeriod, longPeriod = owner.LongPeriod;
        if (n <= longPeriod) return;
        // Slot j holds the interval that ends at bar j + 1; the first bar only supplies a previous close.
        double[] pressures = FastMath.Allocate(n - 1), ranges = FastMath.Allocate(n - 1);
        double shortPressure = 0, shortRange = 0, middlePressure = 0, middleRange = 0, longPressure = 0, longRange = 0;
        // Consecutive intervals with no range: a window of them sums to exactly zero.
        var flat = 0;
        for (var j = 0; j < pressures.Length; j++)
        {
            double previous = close[j], floor = Math.Min(low[j + 1], previous);
            var pressure = close[j + 1] - floor;
            var range = zero ? TrueRange(high[j + 1], low[j + 1], previous) : Math.Max(high[j + 1], previous) - floor;
            pressures[j] = pressure;
            ranges[j] = range;
            flat = range == 0 ? flat + 1 : 0;
            if ((j & RebuildMask) == 0 && j >= longPeriod)
            {
                shortPressure = Total(pressures, j, shortPeriod);
                shortRange = Total(ranges, j, shortPeriod);
                middlePressure = Total(pressures, j, middlePeriod);
                middleRange = Total(ranges, j, middlePeriod);
                longPressure = Total(pressures, j, longPeriod);
                longRange = Total(ranges, j, longPeriod);
            }
            else
            {
                shortPressure += j >= shortPeriod ? pressure - pressures[j - shortPeriod] : pressure;
                shortRange += j >= shortPeriod ? range - ranges[j - shortPeriod] : range;
                middlePressure += j >= middlePeriod ? pressure - pressures[j - middlePeriod] : pressure;
                middleRange += j >= middlePeriod ? range - ranges[j - middlePeriod] : range;
                longPressure += j >= longPeriod ? pressure - pressures[j - longPeriod] : pressure;
                longRange += j >= longPeriod ? range - ranges[j - longPeriod] : range;
            }
            if (j < longPeriod - 1) continue;
            if (flat >= shortPeriod && !zero) continue;
            var total = (flat >= shortPeriod ? 0 : 4 * shortPressure / shortRange)
                + (flat >= middlePeriod ? 0 : 2 * middlePressure / middleRange)
                + (flat >= longPeriod ? 0 : longPressure / longRange);
            value[j + 1] = 100 * total / 7;
            defined[j + 1] = 1;
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(FastMath.Hot)]
    private static double Total(double[] values, int last, int period)
    {
        double sum = 0;
        for (var j = last - period + 1; j <= last; j++) sum += values[j];
        return sum;
    }


    /// <summary>
    /// Directional index whose movement and range totals start as plain sums over the first period: the
    /// two directional indicators, their index, its mean-seeded Wilder average, and that average's mean
    /// with its value one period back.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    internal static void SumSeededDirectional(FastColumns columns, double[][] outputs, int period)
    {
        columns.RequireHighLowClose();
        ReadOnlySpan<double> high = columns.High, low = columns.Low, close = columns.Close;
        var n = close.Length;
        for (var slot = 0; slot < 10; slot++) outputs[slot].AsSpan().Clear();
        Span<double> positiveOut = outputs[0], negativeOut = outputs[1], indexOut = outputs[2], averageOut = outputs[3],
            ratingOut = outputs[4], averageDefined = outputs[8];
        double positive = 0, negative = 0, range = 0, seed = 0, average = 0;
        var hasAverage = false;
        var keep = (period - 1.0) / period;
        long seedIndex = 2L * period - 1;
        for (var i = 1; i < n; i++)
        {
            double up = high[i] - high[i - 1], down = low[i - 1] - low[i];
            // Which move dominates decides a whole bar, so a near tie is settled exactly.
            var dominance = up > 0 || down > 0 ? SumSign(high[i], low[i], high[i - 1], low[i - 1]) : 0;
            double plus = up > 0 && dominance > 0 ? up : 0, minus = down > 0 && dominance < 0 ? down : 0;
            var trueRange = TrueRange(high[i], low[i], close[i - 1]);
            if (i <= period)
            {
                positive += plus;
                negative += minus;
                range += trueRange;
                if (i < period) continue;
            }
            else
            {
                positive = FastMath.Blend(keep, positive, plus);
                negative = FastMath.Blend(keep, negative, minus);
                range = FastMath.Blend(keep, range, trueRange);
            }
            if (range == 0) continue;
            double plusIndicator = 100 * positive / range, minusIndicator = 100 * negative / range;
            var total = plusIndicator + minusIndicator;
            var index = total == 0 ? 0 : 100 * Math.Abs(plusIndicator - minusIndicator) / total;
            positiveOut[i] = plusIndicator;
            negativeOut[i] = minusIndicator;
            indexOut[i] = index;
            outputs[5][i] = outputs[6][i] = outputs[7][i] = 1;
            if (i <= seedIndex)
            {
                seed += index;
                if (i == seedIndex)
                {
                    average = seed / period;
                    hasAverage = true;
                }
            }
            else if (hasAverage)
                average = (average * (period - 1) + index) / period;
            if (!hasAverage) continue;
            averageOut[i] = average;
            averageDefined[i] = 1;
            if (averageDefined[i - period] == 0) continue;
            ratingOut[i] = (averageOut[i - period] + average) / 2;
            outputs[9][i] = 1;
        }
    }

    // Sign of (a + b) - (c + d). The rounded value decides it unless it is too close to zero to trust.
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Hot)]
    private static int SumSign(double a, double b, double c, double d)
    {
        var difference = a - c + (b - d);
        var magnitude = Math.Abs(a) + Math.Abs(b) + Math.Abs(c) + Math.Abs(d);
        if (Math.Abs(difference) > 4e-15 * magnitude) return difference > 0 ? 1 : -1;
        var exact = JurikDyadic.FromDouble(a);
        exact.Add(b);
        exact.Add(-c);
        exact.Add(-d);
        return exact.Sign;
    }
}
