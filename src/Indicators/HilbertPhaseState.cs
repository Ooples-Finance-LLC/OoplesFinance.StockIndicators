using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

// Exact rounded logical Hilbert stages shared by adaptive averages and cycle readings.
internal sealed class HilbertPhaseState(int start, bool retainPeriod, bool retainOlder)
{
    private static readonly BigInteger Grid = BigInteger.One << 1074;
    private readonly BigInteger[] _prices = new BigInteger[3],
        _smooth = new BigInteger[6],
        _detrended = new BigInteger[6],
        _quadrature = new BigInteger[6],
        _inphase = new BigInteger[6];
    private BigInteger _i2,
        _q2,
        _real,
        _imaginary;
    private double _olderPeriod,
        _olderPhase;
    private long _index = -1;
    internal BigInteger InPhase { get; private set; }
    internal BigInteger Quadrature { get; private set; }
    internal BigInteger SmoothPrice { get; private set; }
    internal double Period { get; private set; }
    internal double Phase { get; private set; }

    private static BigInteger U(double value) => ExactVarianceWindow.Units(value);

    private static BigInteger R(BigInteger n, BigInteger d) => RocBankValue.RoundUnits(n, d);

    private static BigInteger Fir(BigInteger value, BigInteger[] past, double correction) =>
        R(
            R(U(.0962) * value + U(.5769) * past[1] - U(.5769) * past[3] - U(.0962) * past[5], Grid)
                * U(correction),
            Grid
        );

    private static BigInteger Blend(BigInteger value, BigInteger old) =>
        R(U(.2) * value + U(.8) * old, Grid);

    private static double Ratio(BigInteger a, BigInteger b) =>
        ExactMeanAccumulator.UnitRatio((a * b.Sign) << 1074, BigInteger.Abs(b));

    private static void Shift(BigInteger[] a, BigInteger value)
    {
        for (var i = a.Length - 1; i > 0; i--)
            a[i] = a[i - 1];
        a[0] = value;
    }

    internal void Reset()
    {
        foreach (var a in new[] { _prices, _smooth, _detrended, _quadrature, _inphase })
            Array.Clear(a, 0, a.Length);
        _i2 = _q2 = _real = _imaginary = InPhase = Quadrature = SmoothPrice = 0;
        _olderPeriod = _olderPhase = Period = Phase = 0;
        _index = -1;
    }

    internal void Update(BigInteger price)
    {
        _index++;
        if (_index >= start)
        {
            var correction = .075 * Period + .54;
            SmoothPrice = R(4 * price + 3 * _prices[0] + 2 * _prices[1] + _prices[2], 10);
            var detrended = Fir(SmoothPrice, _smooth, correction);
            Quadrature = Fir(detrended, _detrended, correction);
            InPhase = _detrended[2];
            var ji = Fir(InPhase, _inphase, correction);
            var jq = Fir(Quadrature, _quadrature, correction);
            var i2 = Blend(R(InPhase - jq, 1), _i2);
            var q2 = Blend(R(Quadrature + ji, 1), _q2);
            var real = Blend(R(i2 * _i2 + q2 * _q2, Grid), _real);
            var imaginary = Blend(R(i2 * _q2 - q2 * _i2, Grid), _imaginary);
            var measured =
                !imaginary.IsZero && !real.IsZero ? 2 * Math.PI / Math.Atan(Ratio(imaginary, real))
                : retainPeriod ? Period
                : retainOlder ? _olderPeriod
                : 0;
            measured = Math.Min(measured, 1.5 * Period);
            measured = Math.Max(measured, .67 * Period);
            measured = Math.Max(6, Math.Min(50, measured));
            var period = .2 * measured + .8 * Period;
            var phase =
                !InPhase.IsZero ? Math.Atan(Ratio(Quadrature, InPhase)) * 180 / Math.PI
                : retainOlder ? _olderPhase
                : 0;
            Shift(_smooth, SmoothPrice);
            Shift(_detrended, detrended);
            Shift(_quadrature, Quadrature);
            Shift(_inphase, InPhase);
            _i2 = i2;
            _q2 = q2;
            _real = real;
            _imaginary = imaginary;
            _olderPeriod = Period;
            Period = period;
            _olderPhase = Phase;
            Phase = phase;
        }
        Shift(_prices, price);
    }
}
