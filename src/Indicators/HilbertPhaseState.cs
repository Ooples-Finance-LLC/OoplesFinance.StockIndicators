using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

// Exact rounded logical Hilbert stages shared by adaptive averages and cycle readings.
internal sealed class HilbertPhaseState(int start, bool retainPeriod, bool retainOlder)
{
    private readonly JurikDyadic[] _prices = new JurikDyadic[3],
        _smooth = new JurikDyadic[6],
        _detrended = new JurikDyadic[6],
        _quadrature = new JurikDyadic[6],
        _inphase = new JurikDyadic[6];
    private JurikDyadic _i2,
        _q2,
        _real,
        _imaginary;
    private double _olderPeriod,
        _olderPhase;
    private long _index = -1;
    internal JurikDyadic InPhase { get; private set; }
    internal JurikDyadic Quadrature { get; private set; }
    internal JurikDyadic SmoothPrice { get; private set; }
    internal double Period { get; private set; }
    internal double Phase { get; private set; }

    private static JurikDyadic Product(JurikDyadic value, double weight)
    {
        value.Multiply(weight);
        return value;
    }

    private static JurikDyadic Fir(JurikDyadic value, JurikDyadic[] past, double correction)
    {
        var sum = Product(value, .0962);
        sum.AddExact(Product(past[1], .5769));
        sum.Subtract(Product(past[3], .5769));
        sum.Subtract(Product(past[5], .0962));
        return Product(sum.Rounded(), correction).Rounded();
    }

    private static JurikDyadic Blend(JurikDyadic value, JurikDyadic old)
    {
        var sum = Product(value, .2);
        sum.AddExact(Product(old, .8));
        return sum.Rounded();
    }

    private static JurikDyadic ProductSum(JurikDyadic a, JurikDyadic b,
        JurikDyadic c, JurikDyadic d, bool subtract = false)
    {
        a.Multiply(b); c.Multiply(d);
        if (subtract) a.Subtract(c); else a.AddExact(c);
        return a.Rounded();
    }

    private static JurikDyadic Smooth(JurikDyadic price, JurikDyadic[] past)
    {
        var sum = Product(price, 4);
        sum.AddExact(Product(past[0], 3));
        sum.AddExact(Product(past[1], 2));
        sum.AddExact(past[2]);
        return sum.Rounded(10);
    }

    private static void Shift(JurikDyadic[] a, JurikDyadic value)
    {
        for (var i = a.Length - 1; i > 0; i--)
            a[i] = a[i - 1];
        a[0] = value;
    }

    internal void Reset()
    {
        foreach (var a in new[] { _prices, _smooth, _detrended, _quadrature, _inphase })
            Array.Clear(a, 0, a.Length);
        _i2 = _q2 = _real = _imaginary = InPhase = Quadrature = SmoothPrice = default;
        _olderPeriod = _olderPhase = Period = Phase = 0;
        _index = -1;
    }

    internal void Update(BigInteger price) => Update(JurikDyadic.FromUnits(price));
    internal void Update(double price) => Update(JurikDyadic.FromDouble(price));

    internal void Update(JurikDyadic price)
    {
        _index++;
        if (_index >= start)
        {
            var correction = .075 * Period + .54;
            SmoothPrice = Smooth(price, _prices);
            var detrended = Fir(SmoothPrice, _smooth, correction);
            Quadrature = Fir(detrended, _detrended, correction);
            InPhase = _detrended[2];
            var ji = Fir(InPhase, _inphase, correction);
            var jq = Fir(Quadrature, _quadrature, correction);
            var inphase = InPhase; inphase.Subtract(jq);
            var quadrature = Quadrature; quadrature.AddExact(ji);
            var i2 = Blend(inphase.Rounded(), _i2);
            var q2 = Blend(quadrature.Rounded(), _q2);
            var real = Blend(ProductSum(i2, _i2, q2, _q2), _real);
            var imaginary = Blend(ProductSum(i2, _q2, q2, _i2, subtract: true), _imaginary);
            var measured =
                imaginary.Sign != 0 && real.Sign != 0 ? 2 * Math.PI / Math.Atan(imaginary.Ratio(real))
                : retainPeriod ? Period
                : retainOlder ? _olderPeriod
                : 0;
            measured = Math.Min(measured, 1.5 * Period);
            measured = Math.Max(measured, .67 * Period);
            measured = Math.Max(6, Math.Min(50, measured));
            var period = .2 * measured + .8 * Period;
            var phase =
                InPhase.Sign != 0 ? Math.Atan(Quadrature.Ratio(InPhase)) * 180 / Math.PI
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
