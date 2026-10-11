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
        _imaginary,
        _inPhaseValue,
        _quadratureValue,
        _smoothPriceValue;
    private double _olderPeriod,
        _olderPhase;
    private long _index = -1;
    private int _priceCursor, _filterCursor;
    internal JurikDyadic InPhase => _inPhaseValue;
    internal JurikDyadic Quadrature => _quadratureValue;
    internal JurikDyadic SmoothPrice => _smoothPriceValue;
    internal double Period { get; private set; }
    internal double Phase { get; private set; }

    private static JurikDyadic Product(JurikDyadic value, double weight)
    {
        value.Multiply(weight);
        return value;
    }

    private JurikDyadic Fir(in JurikDyadic value, JurikDyadic[] past, double correction)
    {
        ref readonly var lag2 = ref past[(_filterCursor + 4) % 6];
        ref readonly var lag4 = ref past[(_filterCursor + 2) % 6];
        ref readonly var lag6 = ref past[_filterCursor];
#if !NETFRAMEWORK
        if (value.TryDouble(out double v) && lag2.TryDouble(out double a)
            && lag4.TryDouble(out double b) && lag6.TryDouble(out double c)
            && HilbertRoundedKernel.TryExactSum(v, -c, out double outer)
            && HilbertRoundedKernel.TryExactSum(a, -b, out double inner)
            && HilbertRoundedKernel.TryProductSum(outer, .0962, inner, .5769, out double rounded))
        {
            double corrected = rounded * correction;
            if (double.IsFinite(corrected)) return JurikDyadic.FromDouble(corrected);
        }
#endif
        var sum = Product(value, .0962);
        sum.AddExact(Product(lag2, .5769));
        sum.Subtract(Product(lag4, .5769));
        sum.Subtract(Product(lag6, .0962));
        return Product(sum.Rounded(), correction).Rounded();
    }

    private static JurikDyadic Blend(in JurikDyadic value, in JurikDyadic old)
    {
#if !NETFRAMEWORK
        if (value.TryDouble(out double a) && old.TryDouble(out double b)
            && HilbertRoundedKernel.TryProductSum(a, .2, b, .8, out double rounded))
            return JurikDyadic.FromDouble(rounded);
#endif
        var sum = Product(value, .2);
        sum.AddExact(Product(old, .8));
        return sum.Rounded();
    }

    private static JurikDyadic ProductSum(in JurikDyadic a, in JurikDyadic b,
        in JurikDyadic c, in JurikDyadic d, bool subtract = false)
    {
#if !NETFRAMEWORK
        if (a.TryDouble(out double av) && b.TryDouble(out double bv)
            && c.TryDouble(out double cv) && d.TryDouble(out double dv)
            && HilbertRoundedKernel.TryProductSum(av, bv, subtract ? -cv : cv, dv, out double rounded))
            return JurikDyadic.FromDouble(rounded);
#endif
        var first = a;
        var second = c;
        first.Multiply(b); second.Multiply(d);
        if (subtract) first.Subtract(second); else first.AddExact(second);
        return first.Rounded();
    }

    private JurikDyadic Smooth(in JurikDyadic price, JurikDyadic[] past)
    {
        ref readonly var lag1 = ref past[(_priceCursor + 2) % 3];
        ref readonly var lag2 = ref past[(_priceCursor + 1) % 3];
        ref readonly var lag3 = ref past[_priceCursor];
#if !NETFRAMEWORK
        if (price.TryDouble(out double p) && lag1.TryDouble(out double a)
            && lag2.TryDouble(out double b) && lag3.TryDouble(out double c)
            && HilbertRoundedKernel.TrySmooth(p, a, b, c, out double rounded))
            return JurikDyadic.FromDouble(rounded);
#endif
        var sum = Product(price, 4);
        sum.AddExact(Product(lag1, 3));
        sum.AddExact(Product(lag2, 2));
        sum.AddExact(lag3);
        return sum.Rounded(10);
    }

    internal void Reset()
    {
        foreach (var a in new[] { _prices, _smooth, _detrended, _quadrature, _inphase })
            Array.Clear(a, 0, a.Length);
        _i2 = _q2 = _real = _imaginary = _inPhaseValue = _quadratureValue = _smoothPriceValue = default;
        _olderPeriod = _olderPhase = Period = Phase = 0;
        _index = -1;
        _priceCursor = _filterCursor = 0;
    }

    internal void Update(BigInteger price) => Update(JurikDyadic.FromUnits(price));
    internal void Update(double price) => Update(JurikDyadic.FromDouble(price));

    internal void Update(JurikDyadic price)
    {
        _index++;
        if (_index >= start)
        {
            var correction = .075 * Period + .54;
            _smoothPriceValue = Smooth(price, _prices);
            var detrended = Fir(in _smoothPriceValue, _smooth, correction);
            _quadratureValue = Fir(in detrended, _detrended, correction);
            _inPhaseValue = _detrended[(_filterCursor + 3) % 6];
            var ji = Fir(in _inPhaseValue, _inphase, correction);
            var jq = Fir(in _quadratureValue, _quadrature, correction);
            var inphase = JurikDyadic.SumProduct(in _inPhaseValue, in jq, -1);
            var quadrature = JurikDyadic.SumProduct(in _quadratureValue, in ji, 1);
            var i2 = Blend(inphase, _i2);
            var q2 = Blend(quadrature, _q2);
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
                // Ratio may promote its receiver; preserve the owned field by
                // taking the existing value-property snapshot at this boundary.
                _inPhaseValue.Sign != 0 ? Math.Atan(Quadrature.Ratio(_inPhaseValue)) * 180 / Math.PI
                : retainOlder ? _olderPhase
                : 0;
            _smooth[_filterCursor] = _smoothPriceValue;
            _detrended[_filterCursor] = detrended;
            _quadrature[_filterCursor] = _quadratureValue;
            _inphase[_filterCursor] = _inPhaseValue;
            _filterCursor = (_filterCursor + 1) % 6;
            _i2 = i2;
            _q2 = q2;
            _real = real;
            _imaginary = imaginary;
            _olderPeriod = Period;
            Period = period;
            _olderPhase = Phase;
            Phase = phase;
        }
        _prices[_priceCursor] = price;
        _priceCursor = (_priceCursor + 1) % 3;
    }
}
