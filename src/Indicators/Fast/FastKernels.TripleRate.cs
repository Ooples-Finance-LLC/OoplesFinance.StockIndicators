namespace OoplesFinance.StockIndicators.Indicators.Fast;

internal static partial class FastKernels
{
    /// <summary>
    /// Percentage change of a triple exponential average, with the third average and an optional mean of the
    /// rate as further outputs. The three seeds differ only in how the stages start, so the startup bars follow
    /// the stage rules one bar at a time and the rest is three blends per bar.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    internal static void TripleRate(ReadOnlySpan<double> close, double[][] outputs, TripleExponentialRate owner)
    {
        var n = close.Length;
        for (var slot = 0; slot < 6; slot++) outputs[slot].AsSpan().Clear();
        var period = owner.Period;
        var seed = owner.Seed;
        var shared = seed == TripleRateSeed.SharedMean;
        var alpha = 2.0 / (period + 1.0);
        var weight = (period - 1.0) / (period + 1.0);
        var lookback = (long)period - 1 + owner.Suppression;
        // From this bar on, every stage is past its seed and the third average is published.
        var steady = shared ? period : Math.Max(2 * lookback + period, 3 * lookback);
        var startup = (int)Math.Min(steady, n);
        var tail = new RateTail(outputs, owner.SignalPeriod ?? 0, shared);
        Span<double> values = stackalloc double[3];
        Span<double> sums = stackalloc double[3];
        values.Clear();
        sums.Clear();
        for (var i = 0; i < startup; i++)
        {
            var input = close[i];
            if (shared)
            {
                sums[0] += input;
                if (i == period - 1)
                {
                    values.Fill(sums[0] / period);
                    tail.Seed(values[0]);
                }
                continue;
            }
            var complete = true;
            for (var j = 0; j < 3 && complete; j++)
            {
                var start = j * lookback;
                if (i < start)
                    complete = false;
                else if (seed == TripleRateSeed.CascadedFirstPrice)
                    values[j] = i == start ? input : FastMath.Blend(weight, values[j], alpha * input);
                else if (i < start + period)
                {
                    sums[j] += input;
                    if (i == start + period - 1) values[j] = sums[j] / period;
                    else complete = false;
                }
                else
                    values[j] = FastMath.Blend(weight, values[j], alpha * input);
                if (complete && i < start + lookback) complete = false;
                input = values[j];
            }
            if (complete) tail.Emit(i, input);
        }
        double first = values[0], second = values[1], third = values[2];
        for (var i = startup; i < n; i++)
        {
            first = FastMath.Blend(weight, first, alpha * close[i]);
            second = FastMath.Blend(weight, second, alpha * first);
            third = FastMath.Blend(weight, third, alpha * second);
            tail.Emit(i, third);
        }
    }

    // Publishes the third average, its percentage change and the rolling mean of that change.
    private ref struct RateTail
    {
        private readonly Span<double> _rate, _third, _signal, _rateDefined, _thirdDefined, _signalDefined;
        private readonly int _signalPeriod;
        private readonly bool _shared;
        private double _previous, _sum;
        private int _count, _missing;
        private bool _hasPrevious;

        internal RateTail(double[][] outputs, int signalPeriod, bool shared)
        {
            _rate = outputs[0];
            _third = outputs[1];
            _signal = outputs[2];
            _rateDefined = outputs[3];
            _thirdDefined = outputs[4];
            _signalDefined = outputs[5];
            _signalPeriod = signalPeriod;
            _shared = shared;
            _previous = _sum = 0;
            _count = _missing = 0;
            _hasPrevious = false;
        }

        // The shared seed is a previous value for the first rate, but is not itself published.
        internal void Seed(double value)
        {
            _previous = value;
            _hasPrevious = true;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | FastMath.Hot)]
        internal void Emit(int i, double value)
        {
            _third[i] = value;
            _thirdDefined[i] = 1;
            if (!_hasPrevious)
            {
                Seed(value);
                return;
            }
            double rate;
            var present = true;
            if (_previous != 0)
                rate = 100 * (value - _previous) / _previous;
            else if (!_shared)
                rate = 0;
            else if (value == 0)
            {
                // Zero over zero: the shared seed publishes no rate for this bar.
                rate = 0;
                present = false;
            }
            else
                rate = value > 0 ? double.PositiveInfinity : double.NegativeInfinity;
            _previous = value;
            if (present)
            {
                _rate[i] = rate;
                _rateDefined[i] = 1;
            }
            // An infinite rate fails the build when the outputs are checked, so the window never has to skip one.
            if (_signalPeriod == 0 || double.IsInfinity(rate)) return;
            if (_count == _signalPeriod)
            {
                var old = i - _signalPeriod;
                _sum -= _rate[old];
                if (_rateDefined[old] == 0) _missing--;
            }
            else
                _count++;
            _sum += rate;
            if (!present) _missing++;
            if (_count == _signalPeriod && _missing == 0)
            {
                _signal[i] = _sum / _signalPeriod;
                _signalDefined[i] = 1;
            }
        }
    }
}
