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
}
