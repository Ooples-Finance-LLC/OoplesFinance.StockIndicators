namespace OoplesFinance.StockIndicators.Indicators.Fast;

internal static partial class FastKernels
{
    /// <summary>
    /// Percentage change of a triple exponential average, with the third average and an optional mean of the
    /// rate as further outputs. The three seeds differ only in how the stages start, so the startup bars follow
    /// the stage rules one bar at a time and the rest is three blends per bar.
    /// </summary>
    /// <remarks>
    /// Each of the three series is published from one bar on, on every bar after it, unless a shared seed
    /// meets a zero over zero. So the series are filled without their flags, which are then declared as
    /// columns that turn on at a bar, and a series with no signal asked for is never filled at all. The rare
    /// zero over zero takes the pass again with every flag written out.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    internal static void TripleRate(ReadOnlySpan<double> close, FastOutputs outputs, TripleExponentialRate owner)
    {
        var n = close.Length;
        var signalled = (owner.SignalPeriod ?? 0) != 0;
        var tail = new RateTail(outputs[0], outputs[1], signalled ? outputs[2] : default, default, default, default,
            owner.SignalPeriod ?? 0, owner.Seed == TripleRateSeed.SharedMean, false);
        TripleRate(close, owner, ref tail);
        if (tail.Irregular)
        {
            double[][] all = outputs;
            for (var slot = 0; slot < 6; slot++) all[slot].AsSpan().Clear();
            tail = new RateTail(all[0], all[1], all[2], all[3], all[4], all[5],
                owner.SignalPeriod ?? 0, owner.Seed == TripleRateSeed.SharedMean, true);
            TripleRate(close, owner, ref tail);
            return;
        }
        // Nothing before a series' first bar was written, so that much is zeroed and the flag turns on there.
        int rateFrom = tail.FirstRate < 0 ? n : tail.FirstRate, thirdFrom = tail.FirstThird < 0 ? n : tail.FirstThird,
            signalFrom = tail.FirstSignal < 0 ? n : tail.FirstSignal;
        outputs[0].AsSpan(0, rateFrom).Clear();
        outputs[1].AsSpan(0, thirdFrom).Clear();
        if (signalled) outputs[2].AsSpan(0, signalFrom).Clear();
        else outputs.Flag(2, n);
        outputs.Flag(3, rateFrom);
        outputs.Flag(4, thirdFrom);
        outputs.Flag(5, signalFrom);
    }

    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    private static void TripleRate(ReadOnlySpan<double> close, TripleExponentialRate owner, ref RateTail tail)
    {
        var n = close.Length;
        var period = owner.Period;
        var seed = owner.Seed;
        var shared = seed == TripleRateSeed.SharedMean;
        var alpha = 2.0 / (period + 1.0);
        var weight = (period - 1.0) / (period + 1.0);
        var lookback = (long)period - 1 + owner.Suppression;
        // From this bar on, every stage is past its seed and the third average is published.
        var steady = shared ? period : Math.Max(2 * lookback + period, 3 * lookback);
        var startup = (int)Math.Min(steady, n);
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
        private readonly bool _shared, _flagged;
        private double _previous, _sum;
        private int _count, _missing;
        private bool _hasPrevious;

        /// <summary>The first bar each series was published on, or -1; kept when no flags are written.</summary>
        internal int FirstRate, FirstThird, FirstSignal;

        /// <summary>A bar published no rate after the first one did, which flags that turn on once cannot say.</summary>
        internal bool Irregular;

        [System.Runtime.CompilerServices.MethodImpl(FastMath.Hot)]
        internal RateTail(Span<double> rate, Span<double> third, Span<double> signal, Span<double> rateDefined,
            Span<double> thirdDefined, Span<double> signalDefined, int signalPeriod, bool shared, bool flagged)
        {
            _rate = rate;
            _third = third;
            _signal = signal;
            _rateDefined = rateDefined;
            _thirdDefined = thirdDefined;
            _signalDefined = signalDefined;
            _signalPeriod = signalPeriod;
            _shared = shared;
            _flagged = flagged;
            _previous = _sum = 0;
            _count = _missing = 0;
            _hasPrevious = false;
            FirstRate = FirstThird = FirstSignal = -1;
            Irregular = false;
        }

        // The shared seed is a previous value for the first rate, but is not itself published.
        [System.Runtime.CompilerServices.MethodImpl(FastMath.Hot)]
        internal void Seed(double value)
        {
            _previous = value;
            _hasPrevious = true;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | FastMath.Hot)]
        internal void Emit(int i, double value)
        {
            _third[i] = value;
            if (_flagged) _thirdDefined[i] = 1;
            else if (FirstThird < 0) FirstThird = i;
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
            if (!_flagged)
            {
                // Without flags a bar with no rate cannot be told from one with a rate of zero.
                if (!present) Irregular = true;
                else if (FirstRate < 0) FirstRate = i;
                _rate[i] = rate;
            }
            else if (present)
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
                if (_flagged && _rateDefined[old] == 0) _missing--;
            }
            else
                _count++;
            _sum += rate;
            if (!present) _missing++;
            if (_count == _signalPeriod && _missing == 0)
            {
                _signal[i] = _sum / _signalPeriod;
                if (_flagged) _signalDefined[i] = 1;
                else if (FirstSignal < 0) FirstSignal = i;
            }
        }
    }
}
