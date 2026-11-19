namespace OoplesFinance.StockIndicators.Indicators.Fast;

internal static partial class FastKernels
{
    // a * b + c * d with the first product's rounding carried, so two products that cancel leave their
    // true difference and a result of zero means the exact value is zero.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static double ProductSum(double a, double b, double c, double d)
    {
#if !NETFRAMEWORK
        if (System.Runtime.Intrinsics.X86.Fma.IsSupported || System.Runtime.Intrinsics.Arm.AdvSimd.Arm64.IsSupported)
        {
            var second = c * d;
            var lost = Math.FusedMultiplyAdd(c, d, -second);
            return Math.FusedMultiplyAdd(a, b, second) + lost;
        }
#endif
        return a * b + c * d;
    }

    /// <summary>
    /// The homodyne discriminator behind the adaptive averages and cycle readings: a smoothed price, its
    /// detrended, in-phase and quadrature parts, and from their smoothed products the dominant cycle
    /// period and phase.
    /// </summary>
    private struct HilbertCore
    {
        private readonly int _start;
        private readonly bool _retainPeriod, _retainOlder;
        private double _price1, _price2, _price3;
        private double _smooth1, _smooth2, _smooth3, _smooth4, _smooth5, _smooth6;
        private double _detrended1, _detrended2, _detrended3, _detrended4, _detrended5, _detrended6;
        private double _quadrature1, _quadrature2, _quadrature3, _quadrature4, _quadrature5, _quadrature6;
        private double _inphase1, _inphase2, _inphase3, _inphase4, _inphase5, _inphase6;
        private double _i2, _q2, _real, _imaginary, _olderPeriod, _olderPhase;
        private long _index;

        internal double InPhase, Quadrature, SmoothPrice, Period, Phase;

        internal HilbertCore(int start, bool retainPeriod, bool retainOlder) : this()
        {
            _start = start;
            _retainPeriod = retainPeriod;
            _retainOlder = retainOlder;
            _index = -1;
        }

        // The four-tap filter over the value and the ones two, four and six bars back. Equal values
        // cancel in the differences, so a flat input gives exactly zero.
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static double Fir(double value, double lag2, double lag4, double lag6, double correction) =>
            ProductSum(value - lag6, .0962, lag2 - lag4, .5769) * correction;

        internal void Update(double price)
        {
            _index++;
            if (_index >= _start)
            {
                var correction = .075 * Period + .54;
                SmoothPrice = (4 * price + 3 * _price1 + 2 * _price2 + _price3) / 10;
                var detrended = Fir(SmoothPrice, _smooth2, _smooth4, _smooth6, correction);
                Quadrature = Fir(detrended, _detrended2, _detrended4, _detrended6, correction);
                InPhase = _detrended3;
                var ji = Fir(InPhase, _inphase2, _inphase4, _inphase6, correction);
                var jq = Fir(Quadrature, _quadrature2, _quadrature4, _quadrature6, correction);
                var i2 = ProductSum(InPhase - jq, .2, _i2, .8);
                var q2 = ProductSum(Quadrature + ji, .2, _q2, .8);
                var real = ProductSum(ProductSum(i2, _i2, q2, _q2), .2, _real, .8);
                var imaginary = ProductSum(ProductSum(i2, _q2, -q2, _i2), .2, _imaginary, .8);
                var measured = imaginary != 0 && real != 0 ? 2 * Math.PI / Math.Atan(imaginary / real)
                    : _retainPeriod ? Period
                    : _retainOlder ? _olderPeriod
                    : 0;
                measured = Math.Min(measured, 1.5 * Period);
                measured = Math.Max(measured, .67 * Period);
                measured = Math.Max(6, Math.Min(50, measured));
                var period = .2 * measured + .8 * Period;
                var phase = InPhase != 0 ? Math.Atan(Quadrature / InPhase) * 180 / Math.PI
                    : _retainOlder ? _olderPhase
                    : 0;
                _smooth6 = _smooth5; _smooth5 = _smooth4; _smooth4 = _smooth3; _smooth3 = _smooth2; _smooth2 = _smooth1; _smooth1 = SmoothPrice;
                _detrended6 = _detrended5; _detrended5 = _detrended4; _detrended4 = _detrended3; _detrended3 = _detrended2; _detrended2 = _detrended1; _detrended1 = detrended;
                _quadrature6 = _quadrature5; _quadrature5 = _quadrature4; _quadrature4 = _quadrature3; _quadrature3 = _quadrature2; _quadrature2 = _quadrature1; _quadrature1 = Quadrature;
                _inphase6 = _inphase5; _inphase5 = _inphase4; _inphase4 = _inphase3; _inphase3 = _inphase2; _inphase2 = _inphase1; _inphase1 = InPhase;
                _i2 = i2;
                _q2 = q2;
                _real = real;
                _imaginary = imaginary;
                _olderPeriod = Period;
                Period = period;
                _olderPhase = Phase;
                Phase = phase;
            }
            _price3 = _price2;
            _price2 = _price1;
            _price1 = price;
        }
    }


    /// <summary>
    /// MESA adaptive average and its following average: exponential averages whose step is the fast limit
    /// divided by how far the cycle phase fell on the bar, floored at the slow limit. The seeded form
    /// starts from the mean of the first bars; the delayed form starts from zero after a longer run-in.
    /// </summary>
    internal static void PhaseAdaptive(FastColumns columns, double[][] outputs, double fastLimit, double slowLimit,
        bool delayed, bool retainOlder, bool useMidpoint, long publishFrom)
    {
        ReadOnlySpan<double> close = columns.Close;
        var n = close.Length;
        ReadOnlySpan<double> high = useMidpoint ? columns.High : default, low = useMidpoint ? columns.Low : default;
        Span<double> mamaOut = outputs[0].AsSpan(0, n), famaOut = outputs[1].AsSpan(0, n);
        var first = (int)Math.Min(publishFrom, n);
        mamaOut.Slice(0, first).Clear();
        famaOut.Slice(0, first).Clear();
        DefinedFrom(outputs[2].AsSpan(0, n), publishFrom);
        DefinedFrom(outputs[3].AsSpan(0, n), publishFrom);
        var start = delayed ? 12 : 6;
        var hilbert = new HilbertCore(start, delayed, retainOlder);
        double mama = 0, fama = 0, seed = 0, previousPhase = 0;
        for (var i = 0; i < n; i++)
        {
            var price = useMidpoint ? (high[i] + low[i]) / 2 : close[i];
            hilbert.Update(price);
            if (i < start)
            {
                if (!delayed)
                {
                    seed += price;
                    mama = fama = i == 0 ? price : seed / (i + 1);
                }
            }
            else
            {
                var phase = hilbert.Phase;
                var delta = Math.Max(previousPhase - phase, 1);
                var alpha = delayed && delta <= 1 ? fastLimit : Math.Max(fastLimit / delta, slowLimit);
                mama += alpha * (price - mama);
                fama += .5 * alpha * (mama - fama);
                previousPhase = phase;
            }
            if (i < publishFrom) continue;
            mamaOut[i] = mama;
            famaOut[i] = fama;
        }
    }


    /// <summary>
    /// The dominant cycle period, smoothed, or the in-phase and quadrature parts themselves; published
    /// after the run-in of 32 bars plus the suppression.
    /// </summary>
    internal static void HilbertCycle(ReadOnlySpan<double> close, double[][] outputs, bool periodOnly, long publishFrom)
    {
        var n = close.Length;
        var slots = periodOnly ? 1 : 2;
        for (var slot = 0; slot < slots; slot++)
        {
            outputs[slot].AsSpan(0, n).Clear();
            DefinedFrom(outputs[slots + slot].AsSpan(0, n), publishFrom);
        }
        var hilbert = new HilbertCore(12, true, false);
        double smoothed = 0;
        Span<double> first = outputs[0];
        Span<double> second = periodOnly ? default : outputs[1];
        for (var i = 0; i < n; i++)
        {
            hilbert.Update(close[i]);
            if (i >= 12) smoothed = .33 * hilbert.Period + .67 * smoothed;
            if (i < publishFrom) continue;
            if (periodOnly) first[i] = smoothed;
            else
            {
                first[i] = hilbert.InPhase;
                second[i] = hilbert.Quadrature;
            }
        }
    }


    // The mean of the newest count prices, then a 4-3-2-1 weighting of the last four such means.
    private struct TrendMean
    {
        private double _mean1, _mean2, _mean3;
        private int _cursor;
        internal double Average;

        // prices is a ring of fifty; a count of zero is a mean of zero.
        internal void Update(double[] prices, double price, int count, bool average)
        {
            prices[_cursor] = price;
            if (average)
            {
                double sum = 0;
                var slot = _cursor;
                for (var lag = 0; lag < count; lag++)
                {
                    sum += prices[slot];
                    slot = slot == 0 ? 49 : slot - 1;
                }
                var mean = count == 0 ? 0 : count == 1 ? price : sum / count;
                Average = (4 * mean + 3 * _mean1 + 2 * _mean2 + _mean3) / 10;
                _mean3 = _mean2;
                _mean2 = _mean1;
                _mean1 = mean;
            }
            _cursor = _cursor == 49 ? 0 : _cursor + 1;
        }
    }

    /// <summary>
    /// Instantaneous trendline from bar six: the mean of the price over the dominant cycle, weighted over
    /// its last four values, with the smoothed price and the cycle length. The price itself stands in for
    /// the trendline until bar eleven.
    /// </summary>
    internal static void SeededTrendline(FastColumns columns, double[][] outputs, bool useMidpoint)
    {
        ReadOnlySpan<double> close = columns.Close;
        var n = close.Length;
        ReadOnlySpan<double> high = useMidpoint ? columns.High : default, low = useMidpoint ? columns.Low : default;
        Span<double> trendline = outputs[0].AsSpan(0, n), smooth = outputs[1].AsSpan(0, n), periods = outputs[2].AsSpan(0, n),
            periodsDefined = outputs[5].AsSpan(0, n);
        smooth.Clear();
        periods.Clear();
        periodsDefined.Clear();
        outputs[3].AsSpan(0, n).Fill(1);
        DefinedFrom(outputs[4].AsSpan(0, n), 6);
        var hilbert = new HilbertCore(6, false, false);
        var means = new TrendMean();
        var ring = new double[50];
        double period = 0;
        for (var i = 0; i < n; i++)
        {
            var price = useMidpoint ? (high[i] + low[i]) / 2 : close[i];
            hilbert.Update(price);
            trendline[i] = price;
            if (i < 6)
            {
                means.Update(ring, price, 0, false);
                continue;
            }
            period = .33 * hilbert.Period + .67 * period;
            var count = Math.Min((int)(period + .5), i + 1);
            means.Update(ring, price, count == 0 ? 1 : count, true);
            if (i >= 11) trendline[i] = means.Average;
            smooth[i] = hilbert.SmoothPrice;
            if (count <= 0) continue;
            periods[i] = count;
            periodsDefined[i] = 1;
        }
    }

    /// <summary>
    /// Instantaneous trendline after a 37-bar run-in, published from bar 63 plus the suppression.
    /// </summary>
    internal static void DelayedTrendline(ReadOnlySpan<double> close, Span<double> value, Span<double> defined, long publishFrom)
    {
        var n = close.Length;
        value = value.Slice(0, n);
        value.Clear();
        DefinedFrom(defined.Slice(0, n), publishFrom);
        var hilbert = new HilbertCore(37, true, false);
        var means = new TrendMean();
        var ring = new double[50];
        double period = 0;
        for (var i = 0; i < n; i++)
        {
            var price = close[i];
            hilbert.Update(price);
            if (i < 37)
            {
                means.Update(ring, price, 0, false);
                continue;
            }
            period = .33 * hilbert.Period + .67 * period;
            means.Update(ring, price, (int)(period + .5), true);
            if (i >= publishFrom) value[i] = means.Average;
        }
    }


    // For each window length up to fifty: the sine and cosine of every lag's angle, and what each set
    // sums to. The sums are rounding residue, a few parts in 1e17, but on a flat price they are the whole
    // projection, so they are summed without loss.
    private static readonly double[][] ProjectionSines = new double[51][];
    private static readonly double[][] ProjectionCosines = new double[51][];
    private static readonly double[] SineResidue = new double[51];
    private static readonly double[] CosineResidue = new double[51];

    private static bool BuildProjections()
    {
        for (var count = 0; count <= 50; count++)
        {
            double[] sines = new double[count], cosines = new double[count];
            double sineHigh = 0, sineLow = 0, cosineHigh = 0, cosineLow = 0;
            for (var lag = 0; lag < count; lag++)
            {
                var angle = lag * 2d * Math.PI / count;
                sines[lag] = Math.Sin(angle);
                cosines[lag] = Math.Cos(angle);
                var total = sineHigh + sines[lag];
                var virtualTerm = total - sineHigh;
                sineLow += sineHigh - (total - virtualTerm) + (sines[lag] - virtualTerm);
                sineHigh = total;
                total = cosineHigh + cosines[lag];
                virtualTerm = total - cosineHigh;
                cosineLow += cosineHigh - (total - virtualTerm) + (cosines[lag] - virtualTerm);
                cosineHigh = total;
            }
            ProjectionSines[count] = sines;
            ProjectionCosines[count] = cosines;
            SineResidue[count] = sineHigh + sineLow;
            CosineResidue[count] = cosineHigh + cosineLow;
        }
        return true;
    }

    private static readonly bool ProjectionsBuilt = BuildProjections();

    /// <summary>
    /// Dominant cycle phase, its sine and lead sine, or the trend-versus-cycle mode: the smoothed price
    /// projected onto one cycle of the measured period. Published from bar 63 plus the suppression.
    /// </summary>
    internal static void HilbertSignal(ReadOnlySpan<double> close, double[][] outputs, HilbertCycleSignal kind, long publishFrom)
    {
        var n = close.Length;
        var slots = kind == HilbertCycleSignal.Sine ? 2 : 1;
        for (var slot = 0; slot < slots; slot++)
        {
            outputs[slot].AsSpan(0, n).Clear();
            DefinedFrom(outputs[slots + slot].AsSpan(0, n), publishFrom);
        }
        if (!ProjectionsBuilt) return;
        Span<double> first = outputs[0];
        Span<double> second = kind == HilbertCycleSignal.Sine ? outputs[1] : default;
        var trending = kind == HilbertCycleSignal.Trend;
        var hilbert = new HilbertCore(37, true, false);
        var means = new TrendMean();
        double[] ring = new double[50], smooth = new double[50];
        int cursor = 0, days = 0;
        double period = 0, previousPhase = 0, previousSine = 0, previousLead = 0;
        for (var i = 0; i < n; i++)
        {
            var price = close[i];
            hilbert.Update(price);
            if (i < 37)
            {
                if (trending) means.Update(ring, price, 0, false);
                continue;
            }
            period = .33 * hilbert.Period + .67 * period;
            var count = (int)(period + .5);
            var anchor = hilbert.SmoothPrice;
            smooth[cursor] = anchor;
            // Measured from the newest value, so the sums carry the cycle and not the price level; the
            // level's share is the anchor times what the coefficients sum to.
            double real = 0, imaginary = 0;
            double[] sines = ProjectionSines[count], cosines = ProjectionCosines[count];
            var slot = cursor;
            for (var lag = 0; lag < count; lag++)
            {
                var value = smooth[slot] - anchor;
                real += value * sines[lag];
                imaginary += value * cosines[lag];
                slot = slot == 0 ? 49 : slot - 1;
            }
            real += anchor * SineResidue[count];
            imaginary += anchor * CosineResidue[count];
            var phase = imaginary != 0 ? Math.Atan(real / imaginary) * 180 / Math.PI
                : previousPhase + (real < 0 ? -90 : real > 0 ? 90 : 0);
            phase += 90;
            phase += 360 / period;
            if (imaginary < 0) phase += 180;
            if (phase > 315) phase -= 360;
            var sine = Math.Sin(phase * (Math.PI / 180));
            var lead = Math.Sin((phase + 45) * (Math.PI / 180));
            var trend = 1;
            if (trending)
            {
                means.Update(ring, price, count, true);
                if (sine > lead && previousSine <= previousLead || sine < lead && previousSine >= previousLead)
                {
                    days = 0;
                    trend = 0;
                }
                days = Math.Min(days + 1, 50);
                if (days < .5 * period) trend = 0;
                var delta = phase - previousPhase;
                if (delta > .67 * 90 * 4 / period && delta < 1.5 * 90 * 4 / period) trend = 0;
                var baseline = means.Average;
                if (baseline != 0 && Math.Abs(anchor - baseline) - .015 * Math.Abs(baseline) >= 0) trend = 1;
            }
            previousPhase = phase;
            previousSine = sine;
            previousLead = lead;
            cursor = cursor == 49 ? 0 : cursor + 1;
            if (i < publishFrom) continue;
            switch (kind)
            {
                case HilbertCycleSignal.Sine:
                    first[i] = sine;
                    second[i] = lead;
                    break;
                case HilbertCycleSignal.Phase:
                    first[i] = phase;
                    break;
                default:
                    first[i] = trend;
                    break;
            }
        }
    }
}
