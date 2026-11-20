namespace OoplesFinance.StockIndicators.Indicators.Fast;

internal static partial class FastKernels
{
    // Sliding sums are rebuilt from their window this often, which bounds the rounding they can carry.
    private const int RebuildMask = 255;

    /// <summary>
    /// Kaufman adaptive average. The efficiency ratio is the net move over the window divided by the sum of
    /// the bar-to-bar moves, and it sets the smoothing rate between the slow and fast constants.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Hot)]
    internal static void AdaptiveAverage(ReadOnlySpan<double> close, FastOutputs outputs, SeededAdaptiveAverage owner)
    {
        var n = close.Length;
        var period = owner.Period;
        Span<double> average = outputs[0].AsSpan(0, n), efficiency = outputs[1].AsSpan(0, n);
        outputs.Flag(2, owner.FirstOutputIndex);
        outputs.Flag(3, period);
        var first = (int)Math.Min(owner.FirstOutputIndex, n);
        var fast = 2.0 / ((owner.ClampFastPeriod ? Math.Min(period, owner.FastPeriod) : owner.FastPeriod) + 1.0);
        var slow = 2.0 / (owner.SlowPeriod + 1.0);
        var span = fast - slow;
        var flatEfficiency = owner.FastFlat ? 1.0 : 0.0;
        var resetFlat = owner.ResetFlat;
        double volatility = 0, value = 0;
        // Consecutive equal closes ending here. A window of equal closes has exactly zero volatility, which a
        // slid sum cannot be trusted to reproduce.
        var run = 1;
        for (var i = 0; i < n; i++)
        {
            var price = close[i];
            if (i > 0)
            {
                var change = Math.Abs(price - close[i - 1]);
                run = change == 0 ? run + 1 : 1;
                if (i > period) volatility -= Math.Abs(close[i - period] - close[i - period - 1]);
                volatility += change;
            }
            if (i < period)
            {
                value = price;
                efficiency[i] = 0;
            }
            else
            {
                var flat = run > period;
                if (flat)
                    volatility = 0;
                else if ((i & RebuildMask) == 0 || !(volatility > 0))
                {
                    volatility = 0;
                    for (var j = i - period + 1; j <= i; j++) volatility += Math.Abs(close[j] - close[j - 1]);
                }
                var ratio = flat ? flatEfficiency : Math.Abs(price - close[i - period]) / volatility;
                var rate = ratio * span + slow;
                value = resetFlat && flat ? price : value + rate * rate * (price - value);
                efficiency[i] = ratio;
            }
            average[i] = i >= first ? value : 0;
        }
    }

    /// <summary>
    /// Least-squares line through the last <paramref name="owner"/>.Period closes, over the available history
    /// while the window fills. The sums are kept relative to an anchor price, so the slope does not lose
    /// digits to the price level.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Hot)]
    internal static void LinearRegression(ReadOnlySpan<double> close, Span<double> output, WindowLinearRegression owner)
    {
        var n = close.Length;
        output = output.Slice(0, n);
        var period = owner.Period;
        var selected = owner.Output;
        var level = selected is not (WindowRegressionOutput.Slope or WindowRegressionOutput.Angle);
        if (n == 0) return;
        if (period == 1)
        {
            if (level) close.CopyTo(output);
            else output.Clear();
            return;
        }
        double anchor = close[0], sum = 0, weighted = 0;
        output[0] = level ? close[0] : 0;
        for (var i = 1; i < n; i++)
        {
            int count;
            if (i < period)
            {
                count = i + 1;
                var value = close[i] - anchor;
                weighted += i * value;
                sum += value;
            }
            else if ((i & RebuildMask) == 0)
            {
                count = period;
                anchor = close[i];
                sum = weighted = 0;
                var window = close.Slice(i - period + 1, period);
                for (var j = 0; j < window.Length; j++)
                {
                    var value = window[j] - anchor;
                    sum += value;
                    weighted += j * value;
                }
            }
            else
            {
                count = period;
                var value = close[i] - anchor;
                var expired = close[i - period] - anchor;
                weighted += (period - 1) * value - (sum - expired);
                sum += value - expired;
            }
            var center = (count - 1) / 2.0;
            var slope = (weighted - center * sum) / (count * ((double)count * count - 1) / 12);
            var mean = sum / count;
            output[i] = selected switch
            {
                WindowRegressionOutput.Slope => slope,
                WindowRegressionOutput.Angle => Math.Atan(slope) * (180 / Math.PI),
                WindowRegressionOutput.Intercept => anchor + (mean - slope * center),
                WindowRegressionOutput.Forecast => anchor + (mean + slope * (center + 1)),
                _ => anchor + (mean + slope * center),
            };
        }
    }


    /// <summary>
    /// Regression channels: the closes are cut into whole blocks counted back from the last bar, each
    /// fitted with its own least-squares line and a channel a multiple of the block's deviation either side.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Hot)]
    internal static void RegressionChannels(ReadOnlySpan<double> close, FastOutputs outputs, int? period, double deviations)
    {
        var n = close.Length;
        var length = period ?? n;
        if (length < 2) throw new ArgumentOutOfRangeException(nameof(period), "A whole-input fit requires at least two bars.");
        for (var slot = 0; slot < 7; slot++) outputs[slot].AsSpan(0, n).Clear();
        if (n < length) return;
        var leading = n % length;
        Span<double> center = outputs[0].AsSpan(0, n), upper = outputs[1].AsSpan(0, n), lower = outputs[2].AsSpan(0, n);
        var breaks = outputs[3].AsSpan(0, n);
        for (var slot = 4; slot < 7; slot++) outputs.Flag(slot, leading);
        var middle = 0.5 * (length - 1);
        // The sum of squared offsets from the middle of 0..length-1.
        var spread = length * ((double)length * length - 1) / 12;
        for (var start = leading; start < n; start += length)
        {
            var block = close.Slice(start, length);
            var anchor = block[0];
            double sum = 0;
            for (var j = 0; j < length; j++) sum += block[j] - anchor;
            var mean = sum / length;
            double squares = 0, cross = 0;
            for (var j = 0; j < length; j++)
            {
                var y = block[j] - anchor - mean;
                squares += y * y;
                cross += (j - middle) * y;
            }
            var slope = cross / spread;
            var width = Math.Sqrt(squares / length) * deviations;
            var level = anchor + mean;
            breaks[start] = 1;
            for (var j = 0; j < length; j++)
            {
                var value = level + slope * (j - middle);
                center[start + j] = value;
                upper[start + j] = value + width;
                lower[start + j] = value - width;
            }
        }
    }
}
