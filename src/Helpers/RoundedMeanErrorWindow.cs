using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

// Errors relative to the published, once-rounded mean. The signed relative
// errors remain rational until the final average, so opposite large terms cancel.
internal sealed class RoundedMeanErrorWindow(int period)
{
    internal readonly struct Sample
    {
        private readonly BigInteger _units;
        private readonly bool _hasUnits;
        internal Sample(double value)
        {
            Value = value; _units = default; _hasUnits = false;
#if !NETFRAMEWORK
            CertifiedMeanRelativeError.Reciprocal(value, out var floor, out var exact, out var significand, out var exponent);
            NormalSignificand = significand; NormalExponent = exponent;
            ReciprocalFloor = floor; ReciprocalExact = exact;
#endif
        }
        internal Sample(double value, BigInteger units) : this(value) { _units = units; _hasUnits = true; }
        internal double Value { get; }
        internal BigInteger Units => _hasUnits ? _units : ExactVarianceWindow.Units(Value);
#if !NETFRAMEWORK
        internal long NormalSignificand { get; }
        internal int NormalExponent { get; }
        internal UInt128 ReciprocalFloor { get; }
        internal bool ReciprocalExact { get; }
#endif
    }
    private readonly Queue<Sample> _values = new();
    private BigInteger _sum, _squares;
#if !NETFRAMEWORK
    private bool _compact = true, _hasCompactScale;
    private int _compactScale;
    private Int128 _compactSum;
#endif
    internal int Count => _values.Count;
    internal double Mean
    {
        get
        {
            if (Count == 0) return 0;
#if !NETFRAMEWORK
            if (_compact)
            {
                if (_compactSum == 0) return 0;
                UInt128 magnitude = (UInt128)(_compactSum < 0 ? -_compactSum : _compactSum);
                if (CertifiedCompactRatio.TryDivide(magnitude, (UInt128)Count << 64, _compactScale + 64, out double value))
                    return _compactSum < 0 ? -value : value;
                // Subnormal and rounding-boundary cases retain exact rounding
                // without expanding every retained sample onto the 2^-1074 grid.
                return ExactMeanAccumulator.ScaledRatio((BigInteger)_compactSum, Count, _compactScale);
            }
#endif
            return ExactMeanAccumulator.UnitRatio(_sum, Count);
        }
    }

    internal void Reset()
    {
        _values.Clear();
        _sum = _squares = 0;
#if !NETFRAMEWORK
        _compact = true; _hasCompactScale = false; _compactScale = 0; _compactSum = 0;
#endif
    }

    internal void Add(double value)
    {
#if !NETFRAMEWORK
        if (_compact)
        {
            // Validate before eviction, matching the exact path's mutation order.
            bool supported = TryCompactValue(value, out long current);
            if (Count == period)
            {
                var expired = _values.Dequeue();
                if (supported)
                {
                    if (!TryCompactValue(expired.Value, out long old))
                        throw new InvalidOperationException("Compact window lost its exact scale.");
                    _compactSum -= old;
                }
            }
            _values.Enqueue(new Sample(value));
            if (supported) _compactSum += current;
            else MaterializeExact();
            return;
        }
#endif
        var units = ExactVarianceWindow.Units(value);
        if (Count == period)
        {
            var old = _values.Dequeue().Units;
            _sum -= old;
            _squares -= old * old;
        }
        _values.Enqueue(new Sample(value, units));
        _sum += units;
        _squares += units * units;
    }

#if !NETFRAMEWORK
    private bool TryCompactValue(double value, out long integer)
    {
        long bits = BitConverter.DoubleToInt64Bits(value);
        int exponent = (int)((bits >> 52) & 2047);
        if (exponent == 2047) throw new ArgumentOutOfRangeException(nameof(value));
        long significand = bits & ((1L << 52) - 1);
        if (exponent != 0) significand |= 1L << 52;
        if (significand == 0) { integer = 0; return true; }
        int scale = exponent == 0 ? -1074 : exponent - 1075;
        if (!_hasCompactScale) { _compactScale = scale - 3; _hasCompactScale = true; }
        int shift = scale - _compactScale;
        if (shift is < 0 or > 6) { integer = 0; return false; }
        integer = significand << shift;
        if (bits < 0) integer = -integer;
        // Each sample is below 2^59 in magnitude, so an Int32-sized window
        // has an exact signed sum below 2^90, comfortably inside Int128.
        return true;
    }

    private void MaterializeExact()
    {
        if (!_compact) return;
        _sum = _squares = 0;
        int count = Count;
        for (int i = 0; i < count; i++)
        {
            var sample = _values.Dequeue();
            var units = sample.Units;
            _values.Enqueue(new Sample(sample.Value, units));
            _sum += units;
            _squares += units * units;
        }
        _compact = false;
    }
#endif

    internal (double Absolute, double Squared) AbsoluteAndSquaredError(double mean)
    {
#if !NETFRAMEWORK
        if (CertifiedMeanRelativeError.TryErrors(_values, mean, out double absolute, out double squared))
            return (absolute, squared);
#endif
        // Preserve the original independent fallbacks and their evaluation order.
        return (AbsoluteError(mean), SquaredError(mean));
    }

    internal double AbsoluteError(double mean)
    {
#if !NETFRAMEWORK
        if (CertifiedMeanRelativeError.TryError(_values, mean, false, out var certified))
            return certified;
#endif
        var center = ExactVarianceWindow.Units(mean);
        var sum = BigInteger.Zero;
        foreach (var sample in _values)
            sum += BigInteger.Abs(sample.Units - center);
        return ExactMeanAccumulator.UnitRatio(sum, Count);
    }

    internal double SquaredError(double mean)
    {
#if !NETFRAMEWORK
        if (CertifiedMeanRelativeError.TryError(_values, mean, true, out var certified))
            return certified;
        MaterializeExact();
#endif
        var center = ExactVarianceWindow.Units(mean);
        return ExactMeanAccumulator.UnitRatio(
            _squares - 2 * center * _sum + Count * center * center,
            new BigInteger(Count) << 1074
        );
    }

    internal (bool Defined, double Value) SignedRelativeError(double mean)
    {
#if !NETFRAMEWORK
        if (CertifiedMeanRelativeError.TryCompute(_values, mean, out var certified))
            return (true, certified);
#endif
        var center = ExactVarianceWindow.Units(mean);
        var centerZeros = center.IsZero ? int.MaxValue : ExactMeanAccumulator.TrailingBinaryZeros(center);
        var numerator = BigInteger.Zero;
        var denominator = BigInteger.One;
        foreach (var sample in _values)
        {
            var value = sample.Units;
            if (value.IsZero)
                return (false, 0);
            // Cancel the shared binary scale before constructing/reducing the
            // fraction. Ordinary prices then use significand-sized operands;
            // mixed exponents retain exactly the same rational value.
            var shift = Math.Min(centerZeros, ExactMeanAccumulator.TrailingBinaryZeros(value));
            var scaledValue = value >> shift;
            var top = BigInteger.Abs(scaledValue - (center >> shift)) * value.Sign;
            var bottom = BigInteger.Abs(scaledValue);
            var reduce = BigInteger.GreatestCommonDivisor(BigInteger.Abs(top), bottom);
            top /= reduce;
            bottom /= reduce;
            var common = BigInteger.GreatestCommonDivisor(denominator, bottom);
            numerator = numerator * (bottom / common) + top * (denominator / common);
            denominator = denominator / common * bottom;
            // Both addends were reduced. Any factor left in the sum must
            // divide the original denominator GCD, not the growing product.
            reduce = BigInteger.GreatestCommonDivisor(BigInteger.Abs(numerator), common);
            numerator /= reduce;
            denominator /= reduce;
        }
        return (true, ExactMeanAccumulator.UnitRatio(numerator << 1074, denominator * Count));
    }
}
