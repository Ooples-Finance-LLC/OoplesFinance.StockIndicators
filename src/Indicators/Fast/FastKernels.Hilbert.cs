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
}
