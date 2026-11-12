#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

internal sealed partial class HilbertPhaseState
{
    private readonly double[] _scalarPrices = new double[3],
        _scalarSmooth = new double[6], _scalarDetrended = new double[6],
        _scalarQuadratures = new double[6], _scalarInphases = new double[6];
    private double _scalarI2, _scalarQ2, _scalarReal, _scalarImaginary,
        _scalarInPhase, _scalarQuadrature, _scalarSmoothPrice;
    private bool _scalarState = true;

    private static double Canonical(double value) => value == 0 ? 0 : value;

    private bool TryScalarSmooth(double price, out double rounded)
    {
        double a = _scalarPrices[(_priceCursor + 2) % 3],
            b = _scalarPrices[(_priceCursor + 1) % 3], c = _scalarPrices[_priceCursor];
        if (HilbertRoundedKernel.TrySmooth(price, a, b, c, out rounded)) return true;
        var sum = Product(JurikDyadic.FromDouble(price), 4);
        sum.AddExact(Product(JurikDyadic.FromDouble(a), 3));
        sum.AddExact(Product(JurikDyadic.FromDouble(b), 2));
        sum.AddExact(JurikDyadic.FromDouble(c));
        return sum.Rounded(10).TryDouble(out rounded);
    }

    private bool TryScalarFir(double value, double[] past, double correction, out double rounded)
    {
        double a = past[(_filterCursor + 4) % 6], b = past[(_filterCursor + 2) % 6],
            c = past[_filterCursor];
        if ((HilbertRoundedKernel.TryExactSum(value, -c, out double outer)
             && HilbertRoundedKernel.TryExactSum(a, -b, out double inner)
             && HilbertRoundedKernel.TryProductSum(outer, .0962, inner, .5769, out rounded))
            || HilbertRoundedKernel.TryFir(value, a, b, c, out rounded))
        {
            double corrected = rounded * correction;
            if (double.IsFinite(corrected)) { rounded = Canonical(corrected); return true; }
        }
        var sum = Product(JurikDyadic.FromDouble(value), .0962);
        sum.AddExact(Product(JurikDyadic.FromDouble(a), .5769));
        sum.Subtract(Product(JurikDyadic.FromDouble(b), .5769));
        sum.Subtract(Product(JurikDyadic.FromDouble(c), .0962));
        return Product(sum.Rounded(), correction).Rounded().TryDouble(out rounded);
    }

    private static bool TryScalarBlend(double value, double old, out double rounded)
    {
        if (TryWordBlend(value, old, out rounded)) return true;
        if (HilbertRoundedKernel.TryProductSum(value, .2, old, .8, out rounded)) return true;
        return Blend(JurikDyadic.FromDouble(value), JurikDyadic.FromDouble(old)).TryDouble(out rounded);
    }

    private static bool TryScalarProductSum(double a, double b, double c, double d,
        bool subtract, out double rounded)
    {
        if (HilbertRoundedKernel.TryProductSum(a, b, subtract ? -c : c, d, out rounded)) return true;
        return ProductSum(JurikDyadic.FromDouble(a), JurikDyadic.FromDouble(b),
            JurikDyadic.FromDouble(c), JurikDyadic.FromDouble(d), subtract).TryDouble(out rounded);
    }

    private static bool TryScalarSum(double a, double b, double factor, out double rounded)
    {
        // Multiplication by +/-1 is exact, so this has SumProduct's single
        // rounding. Only an extended result needs the dyadic stage fallback.
        rounded = a + factor * b;
        if (double.IsFinite(rounded)) { rounded = Canonical(rounded); return true; }
        return JurikDyadic.SumProduct(JurikDyadic.FromDouble(a), JurikDyadic.FromDouble(b), factor)
            .TryDouble(out rounded);
    }

    private bool TryUpdateScalar(double price)
    {
        if (!double.IsFinite(price)) return false;
        price = Canonical(price);
        long index = _index + 1;
        if (index >= start)
        {
            double correction = .075 * Period + .54;
            // No owned field or ring changes until every stage is representable.
            // A failed certificate first uses the exact dyadic stage; it does
            // not by itself switch the entire state to the extended path.
            if (!TryScalarSmooth(price, out double smooth)
                || !TryScalarFir(smooth, _scalarSmooth, correction, out double detrended)
                || !TryScalarFir(detrended, _scalarDetrended, correction, out double quadrature)) return false;
            double inphase = _scalarDetrended[(_filterCursor + 3) % 6];
            if (!TryScalarFir(inphase, _scalarInphases, correction, out double ji)
                || !TryScalarFir(quadrature, _scalarQuadratures, correction, out double jq)
                || !TryScalarSum(inphase, jq, -1, out double combinedInphase)
                || !TryScalarSum(quadrature, ji, 1, out double combinedQuadrature)
                || !TryScalarBlend(combinedInphase, _scalarI2, out double i2)
                || !TryScalarBlend(combinedQuadrature, _scalarQ2, out double q2)
                || !TryScalarProductSum(i2, _scalarI2, q2, _scalarQ2, false, out double realProduct)
                || !TryScalarBlend(realProduct, _scalarReal, out double real)
                || !TryScalarProductSum(i2, _scalarQ2, q2, _scalarI2, true, out double imaginaryProduct)
                || !TryScalarBlend(imaginaryProduct, _scalarImaginary, out double imaginary)) return false;
            // These operands are exact binary64 snapshots. Keep the original
            // control-expression order and signed-zero ratio/phase behavior.
            double measured = imaginary != 0 && real != 0 ? 2 * Math.PI / Math.Atan(imaginary / real)
                : retainPeriod ? Period : retainOlder ? _olderPeriod : 0;
            measured = Math.Min(measured, 1.5 * Period);
            measured = Math.Max(measured, .67 * Period);
            measured = Math.Max(6, Math.Min(50, measured));
            double period = .2 * measured + .8 * Period;
            double phase = inphase != 0 ? Math.Atan(quadrature / inphase) * 180 / Math.PI
                : retainOlder ? _olderPhase : 0;
            _scalarSmooth[_filterCursor] = smooth;
            _scalarDetrended[_filterCursor] = detrended;
            _scalarQuadratures[_filterCursor] = quadrature;
            _scalarInphases[_filterCursor] = inphase;
            _filterCursor = (_filterCursor + 1) % 6;
            _scalarSmoothPrice = smooth;
            _scalarInPhase = inphase;
            _scalarQuadrature = quadrature;
            _scalarI2 = i2;
            _scalarQ2 = q2;
            _scalarReal = real;
            _scalarImaginary = imaginary;
            _olderPeriod = Period;
            Period = period;
            _olderPhase = Phase;
            Phase = phase;
        }
        _scalarPrices[_priceCursor] = price;
        _priceCursor = (_priceCursor + 1) % 3;
        _index = index;
        return true;
    }

    private void SynchronizeScalar()
    {
        if (!_scalarState) return;
        Copy(_scalarPrices, _prices);
        Copy(_scalarSmooth, _smooth);
        Copy(_scalarDetrended, _detrended);
        Copy(_scalarQuadratures, _quadrature);
        Copy(_scalarInphases, _inphase);
        _i2 = JurikDyadic.FromDouble(_scalarI2);
        _q2 = JurikDyadic.FromDouble(_scalarQ2);
        _real = JurikDyadic.FromDouble(_scalarReal);
        _imaginary = JurikDyadic.FromDouble(_scalarImaginary);
        _inPhaseValue = JurikDyadic.FromDouble(_scalarInPhase);
        _quadratureValue = JurikDyadic.FromDouble(_scalarQuadrature);
        _smoothPriceValue = JurikDyadic.FromDouble(_scalarSmoothPrice);
        _scalarState = false;
    }

    private static void Copy(double[] source, JurikDyadic[] destination)
    {
        for (int i = 0; i < source.Length; i++) destination[i] = JurikDyadic.FromDouble(source[i]);
    }

    private void ResetScalar()
    {
        Array.Clear(_scalarPrices);
        Array.Clear(_scalarSmooth);
        Array.Clear(_scalarDetrended);
        Array.Clear(_scalarQuadratures);
        Array.Clear(_scalarInphases);
        _scalarI2 = _scalarQ2 = _scalarReal = _scalarImaginary =
            _scalarInPhase = _scalarQuadrature = _scalarSmoothPrice = 0;
        _scalarState = true;
    }
}
#endif
